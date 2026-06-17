"""FastAPI сервисийн үндсэн тохиргоо."""

from pydantic import BaseModel


class Settings(BaseModel):
    """Rule-based шалгалтын ерөнхий тохиргоо."""

    service_name: str = "bank-ai-service"
    suspicious_threshold: int = 60


settings = Settings()
