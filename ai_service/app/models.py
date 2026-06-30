"""FastAPI request/response model-ууд."""

from decimal import Decimal
from typing import Any
from pydantic import BaseModel, Field


class HealthResponse(BaseModel):
    """Health check response."""

    status: str
    service: str


class SuspiciousDetectionRequest(BaseModel):
    """ASP.NET app-аас ирэх гүйлгээний шалгалтын context.

    Хувийн эмзэг мэдээлэл буюу password, register/national id, phone, email
    зэрэг өгөгдөл энэ сервис рүү явуулахгүй.
    """

    transactionId: int
    senderUserId: int
    amount: Decimal = Field(ge=0)
    sourceCurrency: str
    creditedAmount: Decimal = Field(ge=0)
    targetCurrency: str
    isCrossCurrency: bool
    description: str | None = None
    createdHour: int = Field(ge=0, le=23)

    senderAverageAmountLast30Days: Decimal = Field(default=Decimal("0"), ge=0)
    senderMaxAmountLast30Days: Decimal = Field(default=Decimal("0"), ge=0)
    senderTransactionCountLast24Hours: int = Field(default=0, ge=0)

    smallTransactionCountLast24Hours: int = Field(default=0, ge=0)
    smallTransactionTotalLast24Hours: Decimal = Field(default=Decimal("0"), ge=0)
    distinctReceiverCountLast24Hours: int = Field(default=0, ge=0)
    distinctSenderCountToReceiverLast24Hours: int = Field(default=0, ge=0)
    recentInboundAmountLast30Minutes: Decimal = Field(default=Decimal("0"), ge=0)
    senderAccountAgeDays: int = Field(default=0, ge=0)
    senderDaysSinceLastTransaction: int | None = Field(default=None, ge=0)
    detectionSettings: "SuspiciousDetectionSettings | None" = None


class SuspiciousDetectionRuleSetting(BaseModel):
    """Admin-аас ирэх нэг rule-ийн тохиргоо."""

    ruleCode: str
    isEnabled: bool = True
    score: int = Field(default=0, ge=0, le=100)
    numericThreshold: Decimal | None = Field(default=None, ge=0)
    amountThresholdMnt: Decimal | None = Field(default=None, ge=0)
    amountThresholdUsd: Decimal | None = Field(default=None, ge=0)


class SuspiciousDetectionSettings(BaseModel):
    """Admin UI дээр хадгалсан rule-based detection тохиргоо."""

    suspiciousThreshold: int = Field(default=60, ge=1, le=100)
    rules: list[SuspiciousDetectionRuleSetting] = Field(default_factory=list)


class SuspiciousDetectionResponse(BaseModel):
    """Rule-based шалгалтын үр дүн."""

    isSuspicious: bool
    riskScore: int
    reason: str
    triggeredRules: list[str]


class GeminiAnalysisContext(BaseModel):
    """Blazor app-аас ирэх, нууцлал хамгаалсан AI analysis context."""

    transactionId: int
    createdAt: str
    amount: Decimal = Field(ge=0)
    sourceCurrency: str
    creditedAmount: Decimal = Field(ge=0)
    targetCurrency: str
    riskScore: Decimal = Field(default=Decimal("0"), ge=0)
    suspiciousReason: str | None = None
    reviewStatus: str | None = None
    fromAccountMasked: str
    toAccountMasked: str
    description: str | None = None
    isCrossCurrency: bool
    exchangeRateValue: Decimal | None = Field(default=None, ge=0)
    detectionCheckedAt: str | None = None


class GeminiAnalysisResponse(BaseModel):
    """Gemini-ээс гарсан structured analysis."""

    isSuspicious: bool | None = None
    riskScore: Decimal | None = Field(default=None, ge=0, le=100)
    explanation: str
    recommendedAction: str | None = None
    modelName: str


class GeminiAnalyzeRequest(BaseModel):
    """Structured Gemini analysis request with optional model override."""

    context: GeminiAnalysisContext
    modelName: str | None = None


class GeminiExplainRequest(BaseModel):
    """Нэг гүйлгээний narrative explanation хүсэлт."""

    context: GeminiAnalysisContext


class GeminiExplainResponse(BaseModel):
    """Narrative explanation response."""

    analysis: str


class GeminiChatRequest(BaseModel):
    """Admin follow-up chat request."""

    context: GeminiAnalysisContext
    existingAnalysis: str
    question: str
    modelName: str | None = None


class GeminiChatResponse(BaseModel):
    """Admin follow-up chat response."""

    answer: str


class UserFinanceChatRequest(BaseModel):
    """Logged-in user finance chat request."""

    question: str
    context: dict[str, Any] = Field(default_factory=dict)
    conversation: list["BankInfoChatMessage"] = Field(default_factory=list)


class UserFinanceChatResponse(BaseModel):
    """Logged-in user finance chat response."""

    answer: str


class BankInfoChatMessage(BaseModel):
    """Public Chubi chat-ийн өмнөх богино ярианы мөр."""

    role: str
    content: str


class BankInfoChatRequest(BaseModel):
    """Нэвтрээгүй хэрэглэгчийн Phoebe Bank-ийн талаарх асуулт."""

    question: str
    conversation: list[BankInfoChatMessage] = Field(default_factory=list)


class BankInfoChatResponse(BaseModel):
    """Public Chubi chat-ийн хариу."""

    answer: str
