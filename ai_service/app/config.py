"""FastAPI сервист ашиглах үндсэн тохиргоо."""

import os

from dotenv import load_dotenv
from pydantic import BaseModel

load_dotenv()


class Settings(BaseModel):
    """Rule-based болон Gemini AI engine-ийн тохиргоо."""

    service_name: str = os.getenv("BANK_AI_SERVICE_NAME", "bank-ai-service")
    suspicious_threshold: int = 60
    gemini_base_url: str = os.getenv("GEMINI_BASE_URL", "https://generativelanguage.googleapis.com/v1beta")
    gemini_model: str = os.getenv("GEMINI_MODEL", "gemini-3.1-flash-lite")
    gemini_api_key: str | None = os.getenv("GEMINI_API_KEY")
    gemini_timeout_seconds: int = int(os.getenv("GEMINI_TIMEOUT_SECONDS", "120"))


settings = Settings()
