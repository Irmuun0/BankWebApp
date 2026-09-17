"""Gemini API дуудлага, fallback болон response боловсруулах логик.

Энэ module database рүү огт хандахгүй. Web app-аас ирсэн sanitized context
дээр gemini_prompts module-оор prompt бэлдэж, Gemini API-аас analysis response авна.
"""

from __future__ import annotations

import json
import re
import socket
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor, TimeoutError as FutureTimeoutError
from decimal import Decimal
from typing import Any

from fastapi import HTTPException

from app.gemini_prompts import (
    _build_structured_prompt,
    _build_narrative_prompt,
    _build_follow_up_prompt,
    _build_bank_info_prompt,
    _build_user_finance_prompt,
    _format_bank_info_history,
    _format_context,
    _format_decimal,
)
from app.config import settings
from app.models import BankInfoChatMessage, GeminiAnalysisContext, GeminiAnalysisResponse


class GeminiClientError(RuntimeError):
    """Gemini API дуудлага амжилтгүй болсон үед ашиглах дотоод exception."""


ALLOWED_MODEL_NAMES = {
    "gemini-3.5-flash-lite",
    "gemini-2.5-flash",
    "gemini-3.5-flash",
}
SAFE_DEFAULT_MODEL = "gemini-3.5-flash-lite"


def check_gemini_readiness() -> dict[str, object | None]:
    """Check Gemini readiness without allowing a slow network call to block health checks."""

    timeout_seconds = min(settings.gemini_timeout_seconds, 8)
    executor = ThreadPoolExecutor(max_workers=1, thread_name_prefix="gemini-health")
    future = executor.submit(_probe_gemini_readiness, timeout_seconds)
    try:
        return future.result(timeout=timeout_seconds + 1)
    except FutureTimeoutError:
        future.cancel()
        return {
            "keyConfigured": bool(settings.gemini_api_key),
            "apiReachable": False,
            "modelAvailable": False,
            "modelName": _resolve_model_name(None),
            "baseUrl": settings.gemini_base_url,
            "message": f"Gemini readiness probe exceeded {timeout_seconds} seconds.",
        }
    finally:
        executor.shutdown(wait=False, cancel_futures=True)


def _probe_gemini_readiness(timeout_seconds: int) -> dict[str, object | None]:
    """Probe model metadata without consuming a content-generation request."""

    selected_model = _resolve_model_name(None)
    if not settings.gemini_api_key:
        return {
            "keyConfigured": False,
            "apiReachable": False,
            "modelAvailable": False,
            "modelName": selected_model,
            "baseUrl": settings.gemini_base_url,
            "message": "GEMINI_API_KEY is not configured.",
        }

    url = f"{settings.gemini_base_url.rstrip('/')}/models/{selected_model}"
    request = urllib.request.Request(
        url,
        headers={"x-goog-api-key": settings.gemini_api_key},
        method="GET",
    )

    try:
        with urllib.request.urlopen(request, timeout=timeout_seconds) as response:
            payload = json.loads(response.read().decode("utf-8"))
        supported_methods = payload.get("supportedGenerationMethods") or []
        model_available = "generateContent" in supported_methods
        return {
            "keyConfigured": True,
            "apiReachable": True,
            "modelAvailable": model_available,
            "modelName": selected_model,
            "baseUrl": settings.gemini_base_url,
            "message": (
                "Gemini model is reachable and supports generateContent."
                if model_available
                else "Configured model does not advertise generateContent support."
            ),
        }
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        return {
            "keyConfigured": True,
            "apiReachable": True,
            "modelAvailable": False,
            "modelName": selected_model,
            "baseUrl": settings.gemini_base_url,
            "message": f"Gemini metadata probe failed: HTTP {exc.code} - {_extract_error_message(body)}",
        }
    except (urllib.error.URLError, TimeoutError, socket.timeout) as exc:
        reason = getattr(exc, "reason", str(exc))
        return {
            "keyConfigured": True,
            "apiReachable": False,
            "modelAvailable": False,
            "modelName": selected_model,
            "baseUrl": settings.gemini_base_url,
            "message": f"Gemini endpoint is unreachable: {reason}",
        }


def analyze_transaction(context: GeminiAnalysisContext, model_name: str | None = None) -> GeminiAnalysisResponse:
    """Нэг гүйлгээг Gemini-ээр structured JSON analysis хийлгэнэ."""

    selected_model = _resolve_model_name(model_name)
    text, used_model = _generate_text(_build_structured_prompt(context), max_output_tokens=900, model_name=selected_model)
    parsed = _parse_json_object(text)
    if parsed is None:
        return GeminiAnalysisResponse(
            explanation=_normalize_text(text),
            modelName=used_model,
        )

    return GeminiAnalysisResponse(
        isSuspicious=_read_bool(parsed.get("isSuspicious")),
        riskScore=_read_score(parsed.get("riskScore")),
        explanation=_normalize_text(str(parsed.get("explanation") or "")),
        recommendedAction=_normalize_optional_text(parsed.get("recommendedAction")),
        modelName=used_model,
    )


def explain_transaction(context: GeminiAnalysisContext) -> str:
    """Rule-based detection үр дүнг narrative хэлбэрээр тайлбарлуулна."""

    text, _ = _generate_text(_build_narrative_prompt(context), max_output_tokens=900)
    return _normalize_text(text)


def answer_follow_up(context: GeminiAnalysisContext, existing_analysis: str, question: str, model_name: str | None = None) -> str:
    """Админы follow-up асуултад зөвхөн өгөгдсөн context дээр тулгуурлан хариулна."""

    if not question.strip():
        raise HTTPException(status_code=400, detail="Асуулт хоосон байна.")

    prompt = _build_follow_up_prompt(context, existing_analysis, question.strip())
    text, _ = _generate_text(prompt, max_output_tokens=900, model_name=_resolve_model_name(model_name))
    return _normalize_text(text)


def answer_bank_info_question(question: str, conversation: list[BankInfoChatMessage] | None = None) -> str:
    """Public Chubi chat-д Phoebe Bank системийн мэдээлэлд суурилсан хариу өгнө."""

    if not question.strip():
        raise HTTPException(status_code=400, detail="Асуулт хоосон байна.")

    prompt = _build_bank_info_prompt(question.strip(), conversation or [])
    text, _ = _generate_text(prompt, max_output_tokens=850, model_name=SAFE_DEFAULT_MODEL)
    return _normalize_text(text)


def answer_user_finance_question(
    question: str,
    context: dict[str, Any],
    conversation: list[BankInfoChatMessage] | None = None,
) -> str:
    """Logged-in Chubi chat-д хэрэглэгчийн sanitized санхүүгийн context дээр хариулна."""

    if not question.strip():
        raise HTTPException(status_code=400, detail="Асуулт хоосон байна.")

    prompt = _build_user_finance_prompt(question.strip(), context, conversation or [])
    text, _ = _generate_text(prompt, max_output_tokens=1100, model_name=SAFE_DEFAULT_MODEL)
    return _normalize_text(text)


def _generate_text(prompt: str, max_output_tokens: int, model_name: str | None = None) -> tuple[str, str]:
    selected_model = _resolve_model_name(model_name)
    default_model = _resolve_model_name(None)
    try:
        return _generate_text_once(prompt, max_output_tokens, selected_model)
    except urllib.error.HTTPError as exc:
        if selected_model == default_model or exc.code not in {404, 429, 503}:
            raise _to_http_exception(exc) from exc

        selected_body = exc.read().decode("utf-8", errors="replace")
        try:
            return _generate_text_once(prompt, max_output_tokens, default_model)
        except urllib.error.HTTPError as fallback_exc:
            fallback_body = fallback_exc.read().decode("utf-8", errors="replace")
            raise HTTPException(
                status_code=502,
                detail=(
                    f"Gemini model {selected_model} unavailable: HTTP {exc.code} - {_extract_error_message(selected_body)}. "
                    f"Fallback {default_model} failed: HTTP {fallback_exc.code} - {_extract_error_message(fallback_body)}"
                ),
            ) from fallback_exc


def _generate_text_once(prompt: str, max_output_tokens: int, selected_model: str) -> tuple[str, str]:
    if not settings.gemini_api_key:
        raise HTTPException(status_code=503, detail="GEMINI_API_KEY environment variable ai_service дээр олдсонгүй.")

    url = f"{settings.gemini_base_url.rstrip('/')}/models/{selected_model}:generateContent"
    payload = {
        "contents": [
            {
                "role": "user",
                "parts": [{"text": prompt}],
            }
        ],
        "generationConfig": {
            "temperature": 0.2,
            "maxOutputTokens": max_output_tokens,
        },
    }

    request = urllib.request.Request(
        url,
        data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
        headers={
            "Content-Type": "application/json; charset=utf-8",
            "x-goog-api-key": settings.gemini_api_key,
        },
        method="POST",
    )

    try:
        with urllib.request.urlopen(request, timeout=settings.gemini_timeout_seconds) as response:
            response_body = response.read().decode("utf-8")
    except urllib.error.HTTPError:
        raise
    except urllib.error.URLError as exc:
        raise HTTPException(status_code=502, detail=f"Gemini API холболтын алдаа: {exc.reason}") from exc
    except (TimeoutError, socket.timeout) as exc:
        raise HTTPException(status_code=504, detail="Gemini API timeout боллоо.") from exc

    try:
        document = json.loads(response_body)
        candidates = document.get("candidates") or []
        parts = candidates[0].get("content", {}).get("parts", []) if candidates else []
        for part in parts:
            text = part.get("text")
            if text:
                return text, selected_model
    except (KeyError, TypeError, ValueError) as exc:
        raise HTTPException(status_code=502, detail="Gemini API response уншихад алдаа гарлаа.") from exc

    raise HTTPException(status_code=502, detail="Gemini API хоосон хариу буцаалаа.")


def _to_http_exception(exc: urllib.error.HTTPError) -> HTTPException:
    body = exc.read().decode("utf-8", errors="replace")
    return HTTPException(
        status_code=502,
        detail=f"Gemini API алдаа: HTTP {exc.code} - {_extract_error_message(body)}",
    )

def _parse_json_object(text: str) -> dict[str, Any] | None:
    start = text.find("{")
    end = text.rfind("}")
    if start < 0 or end <= start:
        return None

    try:
        parsed = json.loads(text[start : end + 1])
        return parsed if isinstance(parsed, dict) else None
    except ValueError:
        return None


def _extract_error_message(response_body: str) -> str:
    try:
        document = json.loads(response_body)
        error = document.get("error") or {}
        status = error.get("status")
        message = error.get("message")
        combined = " / ".join(part for part in [status, message] if part)
        return combined[:500] if combined else response_body[:500]
    except ValueError:
        return response_body[:500]


def _read_bool(value: Any) -> bool | None:
    return value if isinstance(value, bool) else None


def _read_score(value: Any) -> Decimal | None:
    try:
        score = Decimal(str(value))
    except Exception:
        return None

    return max(Decimal("0"), min(Decimal("100"), score))


def _normalize_text(value: str) -> str:
    value = value.strip()
    value = re.sub(r"(\*\*|__|`)", "", value)
    value = re.sub(r"\s+(\d+\.\s)", r"\n\1", value)
    value = re.sub(r"\n{3,}", "\n\n", value)
    return value[:4000]


def _normalize_optional_text(value: Any) -> str | None:
    if value is None:
        return None

    normalized = _normalize_text(str(value))
    return normalized or None


def _resolve_model_name(model_name: str | None) -> str:
    normalized = (model_name or settings.gemini_model).strip()
    if normalized in ALLOWED_MODEL_NAMES:
        return normalized

    configured_default = settings.gemini_model.strip()
    return configured_default if configured_default in ALLOWED_MODEL_NAMES else SAFE_DEFAULT_MODEL
