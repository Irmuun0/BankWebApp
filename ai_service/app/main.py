"""Rule-based detection болон Gemini analysis endpoint-үүдтэй FastAPI сервис."""

from fastapi import FastAPI

from app.config import settings
from app.gemini_client import (
    analyze_transaction,
    answer_bank_info_question,
    answer_follow_up,
    answer_user_finance_question,
    explain_transaction,
)
from app.models import (
    BankInfoChatRequest,
    BankInfoChatResponse,
    GeminiAnalyzeRequest,
    GeminiAnalysisResponse,
    GeminiChatRequest,
    GeminiChatResponse,
    GeminiExplainRequest,
    GeminiExplainResponse,
    HealthResponse,
    SuspiciousDetectionRequest,
    SuspiciousDetectionResponse,
    UserFinanceChatRequest,
    UserFinanceChatResponse,
)
from app.rules import detect_suspicious


app = FastAPI(title="Bank AI Service", version="2.0.0")


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    """Сервис ажиллаж байгаа эсэхийг шалгана."""

    return HealthResponse(status="ok", service=settings.service_name)


@app.post("/detect-suspicious", response_model=SuspiciousDetectionResponse)
def detect_suspicious_endpoint(request: SuspiciousDetectionRequest) -> SuspiciousDetectionResponse:
    """Нэг гүйлгээний context дээр rule-based эрсдэлийн оноо бодно."""

    return detect_suspicious(request)


@app.post("/analyze-transaction", response_model=GeminiAnalysisResponse)
def analyze_transaction_endpoint(request: GeminiAnalyzeRequest) -> GeminiAnalysisResponse:
    """Sanitized transaction context дээр Gemini structured analysis хийлгэнэ."""

    return analyze_transaction(request.context, request.modelName)


@app.post("/chat/explain", response_model=GeminiExplainResponse)
def explain_transaction_endpoint(request: GeminiExplainRequest) -> GeminiExplainResponse:
    """Нэг гүйлгээний AI тайлбарыг narrative байдлаар үүсгэнэ."""

    return GeminiExplainResponse(analysis=explain_transaction(request.context))


@app.post("/chat/ask", response_model=GeminiChatResponse)
def ask_transaction_question_endpoint(request: GeminiChatRequest) -> GeminiChatResponse:
    """Админы follow-up асуултад Gemini-ээр хариулуулна."""

    return GeminiChatResponse(
        answer=answer_follow_up(
            request.context,
            request.existingAnalysis,
            request.question,
            request.modelName,
        )
    )


@app.post("/chat/bank-info", response_model=BankInfoChatResponse)
def ask_public_bank_info_endpoint(request: BankInfoChatRequest) -> BankInfoChatResponse:
    """Public Chubi AI chat: зөвхөн Phoebe Bank системийн талаарх мэдээлэл өгнө."""

    return BankInfoChatResponse(
        answer=answer_bank_info_question(
            request.question,
            request.conversation,
        )
    )


@app.post("/chat/user-finance", response_model=UserFinanceChatResponse)
def ask_user_finance_endpoint(request: UserFinanceChatRequest) -> UserFinanceChatResponse:
    """Logged-in Chubi AI chat: хэрэглэгчийн sanitized санхүүгийн context дээр хариулна."""

    return UserFinanceChatResponse(
        answer=answer_user_finance_question(
            request.question,
            request.context,
            request.conversation,
        )
    )
