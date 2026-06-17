"""FastAPI request/response model-ууд."""

from decimal import Decimal
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


class SuspiciousDetectionResponse(BaseModel):
    """Rule-based шалгалтын үр дүн."""

    isSuspicious: bool
    riskScore: int
    reason: str
    triggeredRules: list[str]
