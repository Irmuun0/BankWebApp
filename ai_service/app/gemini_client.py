"""Gemini API prompt болон HTTP client logic.

Энэ module database рүү огт хандахгүй. Web app-аас ирсэн sanitized context
дээр prompt бэлдэж, Gemini API-аас analysis response авна.
"""

from __future__ import annotations

import json
import re
import socket
import urllib.error
import urllib.request
from decimal import Decimal
from typing import Any

from fastapi import HTTPException

from app.config import settings
from app.models import BankInfoChatMessage, GeminiAnalysisContext, GeminiAnalysisResponse


class GeminiClientError(RuntimeError):
    """Gemini API дуудлага амжилтгүй болсон үед ашиглах дотоод exception."""


ALLOWED_MODEL_NAMES = {
    "gemini-3.1-flash-lite",
    "gemini-2.5-flash",
    "gemini-3.5-flash",
}
SAFE_DEFAULT_MODEL = "gemini-3.1-flash-lite"


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

def _build_structured_prompt(context: GeminiAnalysisContext) -> str:
    return f"""
Output formatting rules:
- Do not use Markdown syntax. Do not use **bold**, tables, code blocks, or bullet symbols.
- If you need a list, use plain numbered lines and put each item on a new line.
- The exchangeRateValue is the bank's official applied customer exchange rate for this transaction.
- Do not call the exchange rate suspicious by itself.
- Do not compare it with market rates unless market-rate data is explicitly provided in the context.

Чи банкны админд туслах fraud/risk analyst.
Доорх гүйлгээний sanitized context болон rule-based detection үр дүн дээр үндэслээд сэжигтэй эсэхийг дахин шинжил.

Чухал дүрэм:
- Эцсийн шийдвэрийг чи гаргахгүй. Зөвхөн админд туслах шинжилгээ өг.
- Хариуг зөвхөн JSON object хэлбэрээр буцаа. Markdown, code block, нэмэлт тайлбар битгий нэм.
- Монгол кириллээр тайлбарла.
- Дансны дугаарыг зөвхөн mask хэлбэрээр ашигла.
- Хэрэглэгч рүү шууд илгээх мэдэгдэл битгий бич.

JSON schema:
{{
  "isSuspicious": true эсвэл false,
  "riskScore": 0-100 хооронд тоо,
  "explanation": "Монгол тайлбар",
  "recommendedAction": "Админд санал болгох дараагийн алхам"
}}

Transaction data:
{_format_context(context)}
""".strip()


def _build_narrative_prompt(context: GeminiAnalysisContext) -> str:
    return f"""
Output formatting rules:
- Do not use Markdown syntax. Do not use **bold**, tables, code blocks, or bullet symbols.
- Use short plain-text sections.
- Put every numbered item on a separate line.
- The exchangeRateValue is the bank's official applied customer exchange rate for this transaction.
- Do not call the exchange rate suspicious by itself.
- Do not compare it with market rates unless market-rate data is explicitly provided in the context.

Чи банкны админд туслах fraud/risk analyst.
Доорх rule-based detection үр дүнг Монгол кириллээр богино, ойлгомжтой тайлбарла.

Хязгаарлалт:
- Эцсийн шийдвэрийг чи гаргахгүй. "админ шалгах ёстой" гэж бич.
- Хэрэглэгч рүү шууд илгээх мэдэгдэл битгий бич.
- Зөвхөн өгөгдөл дээр тулгуурлаж тайлбарла.
- Дансны дугаарыг зөвхөн өгөгдсөн mask хэлбэрээр дурд.

Дараах бүтэцтэй 5 хэсгээр хариул:
1. Ерөнхий дүгнэлт
2. Яагаад сэжигтэй гэж оноо авсан бэ
3. Админ нэмж шалгах зүйлс
4. Эрсдэлийн түвшин
5. Санал болгож буй дараагийн алхам

Transaction data:
{_format_context(context)}
""".strip()


def _build_follow_up_prompt(context: GeminiAnalysisContext, existing_analysis: str, question: str) -> str:
    return f"""
Output formatting rules:
- Do not use Markdown syntax. Do not use **bold**, tables, code blocks, or bullet symbols.
- Put every numbered item on a separate line.
- The exchangeRateValue is the bank's official applied customer exchange rate for this transaction.
- Do not call the exchange rate suspicious by itself.
- If the admin clarifies that a previously suspected factor is normal, accept that clarification and reassess only the remaining risk factors.

Чи банкны админд туслах fraud/risk analyst.
Админы follow-up асуултад зөвхөн доорх өгөгдөл болон өмнөх AI шинжилгээнд тулгуурлаж Монгол кириллээр хариул.

Хязгаарлалт:
- Эцсийн шийдвэрийг чи гаргахгүй.
- Хэрэглэгч рүү шууд илгээх мэдэгдэл битгий бич.
- Өгөгдөл хүрэлцэхгүй бол "энэ өгөгдлөөр батлах боломжгүй" гэж хэл.

Transaction summary:
{_format_context(context)}

Өмнөх AI шинжилгээ:
{existing_analysis}

Админы асуулт:
{question}
""".strip()


def _build_bank_info_prompt(question: str, conversation: list[BankInfoChatMessage]) -> str:
    history = _format_bank_info_history(conversation[-8:])
    return f"""
You are Chubi AI, Phoebe Bank's own public information assistant.
Answer by question's language.
Do not use Markdown tables, code blocks, or bold syntax.
Use short, clear paragraphs or a compact numbered list.
You must answer only from the Phoebe Bank system knowledge below.
If the user asks for personal balance, account-specific data, password, PIN, national ID, phone, email, or a real banking operation, do not ask for sensitive data. Tell them to log in or contact the bank/admin.
If the question is unrelated to Phoebe Bank services, politely say you can answer only about Phoebe Bank.
Do not invent fees, legal policy, branch locations, phone numbers, or products not listed here.
Tone rules:
- Speak as Phoebe Bank's assistant, not as an outside narrator.
- Prefer phrases like "Манай банк", "Phoebe Bank таны", "манай систем", "та манай вебээр".
- Do not repeatedly say "энэ төсөл", "demo system", or "системийн мэдээлэл" to the customer unless the user asks a technical question.
- Keep the answer service-oriented, concise, and confident.
- If explaining a limitation, phrase it politely as Phoebe Bank guidance.

Phoebe Bank system knowledge:
1. Phoebe Bank is the demo bank web system used in this project.
2. Public users can see the home page, exchange-rate information, and this Chubi AI information chat before login.
3. There are separate user and admin login flows. The public chat cannot log users in or perform transactions.
4. User dashboard shows selected account, balances, account status summary, and recent transactions.
5. Users can open checking accounts in MNT or USD. The database account type CHECKING is shown to users as "Харилцах".
6. Users can view "Миний дансууд", account details, account status, primary account, opened date, last transaction date, and account owner.
7. Each user can choose one primary account. When another account becomes primary, the previous primary account is unset.
8. Users can activate or deactivate their own accounts from account settings.
9. Transactions support transfers between own accounts and to another user's account. The sender account must be active, and the receiver account is validated.
10. Sending from and to the same account is blocked.
11. Transaction description, shown as "Гүйлгээний утга", is required.
12. A successful transaction shows amount, date/time, remaining balance, receiver name, receiver account, and transaction description.
13. MNT/USD currency conversion uses Phoebe Bank's customer buy/sell rates based on the latest MongolBank official rate plus bank settings.
14. USD to MNT uses the bank buy rate. MNT to USD uses the bank sell rate.
15. Converted money is truncated to two decimal places, not rounded. If the received converted amount would be less than 1.00 in the target currency, the transfer is blocked.
16. Public exchange-rate cards show MongolBank rates. USD also shows Phoebe Bank buy and sell prices.
17. Every account has a daily outgoing transaction limit measured in MNT. The default daily limit is 50,000,000 MNT. There is no unlimited account and no separate single-transfer limit.
18. The daily limit is calculated per account by summing that account's outgoing transactions for the current day in MNT equivalent.
19. Security: 5 failed login attempts lock the account for 15 minutes using server/database time. User sessions automatically log out after 30 minutes.
20. Rule-based suspicious transaction detection can check transactions. Admin reviews suspicious transactions and may notify users when action is needed.
21. Admin features include users, accounts, all transactions, AI Detection, suspicious transaction review, detection reports, fraud rule settings, exchange-rate settings, FX income report, and audit log.

Previous chat:
{history}

User question:
{question}
""".strip()


def _build_user_finance_prompt(question: str, context: dict[str, Any], conversation: list[BankInfoChatMessage]) -> str:
    history = _format_bank_info_history(conversation[-8:])
    context_json = json.dumps(context, ensure_ascii=False, default=str, indent=2)
    return f"""
You are Chubi AI, Phoebe Bank's own logged-in customer finance assistant.
Answer only in Mongolian Cyrillic.
Do not use Markdown tables, code blocks, or bold syntax.
Use short, clear paragraphs and compact numbered lists when useful.
Speak as Phoebe Bank's assistant using phrases like "манай банк", "Phoebe Bank таны", and "таны дансны мэдээллээс харахад".

Privacy and safety rules:
- Use only the sanitized context below.
- Never ask for password, PIN, register/national ID, phone number, email, card number, or full account number.
- Account numbers in the context are already masked. Do not try to reconstruct them.
- Do not claim you completed a banking operation. You can only explain, summarize, and guide.
- Do not give investment, tax, or legal advice. If the user asks for that, give a careful general explanation and suggest contacting a professional.
- If the answer cannot be determined from the provided data, say "энэ өгөгдлөөр баттай хэлэх боломжгүй".
- Distinguish clearly between income, expense, net cashflow, balance, and daily transfer limit.
- If transaction history is empty, explain what information will become available after transactions exist.

What you can help with:
1. Сүүлийн 30 хоногийн орлого, зарлага, цэвэр мөнгөн урсгалын товч дүгнэлт.
2. Сүүлийн 15 хоногийн өөрчлөлт.
3. Сүүлийн 30 гүйлгээний давтамж, гол утга, валютын хэрэглээ.
4. Дансны үлдэгдэл, идэвхтэй эсэх, үндсэн данс, өдрийн лимитийн ойлгомжтой тайлбар.
5. Хэрэглэгчид дараагийн алхам санал болгох: гүйлгээний түүхээ шалгах, дансны тохиргоо харах, лимитээ админаар өөрчлүүлэх гэх мэт.

Previous chat:
{history}

Sanitized user finance context:
{context_json}

User question:
{question}
""".strip()


def _format_bank_info_history(conversation: list[BankInfoChatMessage]) -> str:
    if not conversation:
        return "-"

    lines: list[str] = []
    for message in conversation:
        role = "Assistant" if message.role.lower() == "assistant" else "User"
        content = re.sub(r"\s+", " ", message.content.strip())
        if content:
            lines.append(f"{role}: {content[:700]}")

    return "\n".join(lines) if lines else "-"


def _format_context(context: GeminiAnalysisContext) -> str:
    exchange_rate = _format_decimal(context.exchangeRateValue) if context.exchangeRateValue is not None else "-"
    return "\n".join(
        [
            f"- Transaction ID: {context.transactionId}",
            f"- Огноо цаг: {context.createdAt}",
            f"- Илгээх данс: {context.fromAccountMasked}",
            f"- Хүлээн авах данс: {context.toAccountMasked}",
            f"- Илгээсэн дүн: {_format_decimal(context.amount)} {context.sourceCurrency}",
            f"- Хүлээн авсан дүн: {_format_decimal(context.creditedAmount)} {context.targetCurrency}",
            f"- Валют хөрвүүлэлттэй эсэх: {'Тийм' if context.isCrossCurrency else 'Үгүй'}",
            f"- Ханш: {exchange_rate}",
            f"- Гүйлгээний утга: {context.description or '-'}",
            "- Exchange rate note: This is the bank's official applied customer exchange rate for this transaction. It is not suspicious by itself.",
            f"- Rule-based risk score: {_format_decimal(context.riskScore)} / 100",
            f"- Review status: {context.reviewStatus or '-'}",
            f"- Rule-based шалтгаан: {context.suspiciousReason or '-'}",
            f"- Detection checked at: {context.detectionCheckedAt or '-'}",
        ]
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


def _format_decimal(value: Decimal | int | float | str) -> str:
    return f"{Decimal(str(value)):f}"
