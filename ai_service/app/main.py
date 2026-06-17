"""Сэжигтэй гүйлгээг rule-based аргаар шалгах FastAPI сервис."""

from fastapi import FastAPI
from app.config import settings
from app.models import HealthResponse, SuspiciousDetectionRequest, SuspiciousDetectionResponse
from app.rules import detect_suspicious


app = FastAPI(title="Bank AI Service", version="1.0.0")


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    """Сервис ажиллаж байгаа эсэхийг шалгана."""

    return HealthResponse(status="ok", service=settings.service_name)


@app.post("/detect-suspicious", response_model=SuspiciousDetectionResponse)
def detect_suspicious_endpoint(request: SuspiciousDetectionRequest) -> SuspiciousDetectionResponse:
    """Нэг гүйлгээний context дээр эрсдэлийн оноо бодно."""

    return detect_suspicious(request)
