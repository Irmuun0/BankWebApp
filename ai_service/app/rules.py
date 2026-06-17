"""Сэжигтэй гүйлгээний rule-based оноо бодох хэсэг."""

from decimal import Decimal
from app.config import settings
from app.models import SuspiciousDetectionRequest, SuspiciousDetectionResponse


# Гүйлгээний утганд орвол эрсдэлийн нэмэлт оноо өгөх түлхүүр үгс.
SUSPICIOUS_KEYWORDS = (
    # Яаралтай, дарамттай утга
    "urgent", "asap", "immediately", "emergency", "right now",
    "яаралтай", "даруй", "одоо шууд", "маш яаралтай",

    # Крипто, виртуал хөрөнгө
    "crypto", "bitcoin", "btc", "ethereum", "eth", "usdt", "coin", "token",
    "крипто", "биткойн", "койн", "токен", "usdt", "крипто арилжаа",

    # Бооцоо, мөрийтэй тоглоом
    "bet", "betting", "gambling", "casino", "poker", "lottery",
    "бооцоо", "бооцоот", "мөрий", "мөрийтэй тоглоом", "казино", "покер", "сугалаа",

    # Хар зээл, мөнгө хүүлэлт
    "loan shark", "black loan", "illegal loan", "high interest loan",
    "ломбард", "хүүтэй мөнгө", "өдрийн зээл", "хар зээл", "зээлийн хүү", "мөнгө хүүлэлт",

    # Заналхийлэл, сүрдүүлэг
    "threat", "kill", "murder", "attack", "revenge", "blackmail", "extortion",
    "ална", "алуулах", "зодно", "заналхийлэл", "сүрдүүлэг", "өшөө", "дээрэм",
    "барьцаа", "шантаж", "нууцыг чинь дэлгэнэ", "бие бэлдээд хүлээж бай",

    # Авлига, нууц төлбөр
    "bribe", "kickback", "under the table", "secret payment",
    "хахууль", "авилга", "гарын мөнгө", "арын хаалга", "нууц төлбөр",

    # Мөнгө угаах шинжтэй үгс
    "launder", "money laundering", "clean money", "cash out", "split payment",
    "мөнгө угаах", "цэвэр мөнгө", "бэлэн болгох", "задалж шилжүүлэх",
    "олон хувааж", "дамжуулж өг", "хүний данс руу",

    # Залилан
    "scam", "fraud", "fake document", "fake account", "stolen card",
    "залилан", "луйвар", "хуурамч", "хуурамч бичиг", "хулгайлсан карт",
    "хүний карт", "хүний данс",

    # Хууль бус бараа, үйлчилгээ
    "weapon", "gun", "drug", "narcotics", "stolen goods", "dark web",
    "зэвсэг", "буу", "хар тамхи", "мансууруулах", "хулгайн бараа", "хууль бус бараа",
    "darkweb", "дарк веб"
)


def detect_suspicious(request: SuspiciousDetectionRequest) -> SuspiciousDetectionResponse:
    """Ирсэн context дээр rule бүрийг шалгаад 0-100 оноо буцаана."""

    score = 0
    reasons: list[str] = []
    triggered_rules: list[str] = []
    source_currency = request.sourceCurrency.upper()

    # Хэрэглэгчийн өмнөх дундаж дүнгээс огцом өндөр эсэх.
    if request.senderAverageAmountLast30Days > 0 and request.amount >= request.senderAverageAmountLast30Days * Decimal("3"):
        score += 35
        triggered_rules.append("HIGH_AMOUNT_COMPARED_TO_AVERAGE")
        reasons.append("Гүйлгээний дүн хэрэглэгчийн сүүлийн 30 хоногийн дундаж гүйлгээнээс өндөр байна.")

    # Валют бүрийн маш өндөр дүнгийн босго.
    if _is_very_high_amount(source_currency, request.amount):
        score += 30
        triggered_rules.append("VERY_HIGH_AMOUNT")
        reasons.append("Гүйлгээний дүн өндөр дүнтэй ангилалд орж байна.")

    # Шөнийн цагт хийсэн эсэх.
    if 0 <= request.createdHour < 6:
        score += 15
        triggered_rules.append("NIGHT_TIME_TRANSACTION")
        reasons.append("Гүйлгээ шөнийн цагаар хийгдсэн байна.")

    # 24 цагийн дотор олон гүйлгээ хийсэн эсэх.
    if request.senderTransactionCountLast24Hours >= 5:
        score += 20
        triggered_rules.append("MANY_TRANSACTIONS_LAST_24_HOURS")
        reasons.append("Сүүлийн 24 цагт олон гүйлгээ хийсэн байна.")

    # Өндөр дүнтэй валют хөрвүүлэлт.
    if request.isCrossCurrency and _is_high_cross_currency_amount(source_currency, request.amount):
        score += 15
        triggered_rules.append("HIGH_CROSS_CURRENCY_TRANSACTION")
        reasons.append("Өндөр дүнтэй валют хөрвүүлсэн гүйлгээ байна.")

    # Гүйлгээний утгад эрсдэлтэй түлхүүр үг байгаа эсэх.
    if _has_suspicious_keyword(request.description):
        score += 10
        triggered_rules.append("SUSPICIOUS_DESCRIPTION_KEYWORD")
        reasons.append("Гүйлгээний утгад анхаарах түлхүүр үг илэрсэн байна.")

    # Олон жижиг дүнтэй гүйлгээ.
    if request.smallTransactionCountLast24Hours >= 10:
        score += 20
        triggered_rules.append("MANY_SMALL_TRANSACTIONS")
        reasons.append("Сүүлийн 24 цагт олон жижиг дүнтэй гүйлгээ хийсэн байна.")

    # Жижиглэж шилжүүлсэн нийт дүн өндөр эсэх.
    if _looks_like_structuring(source_currency, request.smallTransactionTotalLast24Hours):
        score += 35
        triggered_rules.append("STRUCTURING_SMALL_SPLIT_TRANSFERS")
        reasons.append("Том дүнг жижиглэн олон удаа шилжүүлсэн байж болзошгүй байна.")

    # Орж ирсэн мөнгийг богино хугацаанд гаргасан эсэх.
    if request.recentInboundAmountLast30Minutes > 0 and request.amount >= request.recentInboundAmountLast30Minutes * Decimal("0.8"):
        score += 30
        triggered_rules.append("RAPID_IN_OUT_FLOW")
        reasons.append("Дансанд орсон мөнгийг богино хугацаанд дахин гаргасан шинж илэрсэн байна.")

    # Шинэ данснаас өндөр дүн гарч байгаа эсэх.
    if request.senderAccountAgeDays <= 7 and _is_high_amount(source_currency, request.amount):
        score += 25
        triggered_rules.append("NEW_ACCOUNT_HIGH_AMOUNT")
        reasons.append("Шинэ данснаас өндөр дүнтэй гүйлгээ хийсэн байна.")

    # Удаан хөдөлгөөнгүй байсан данс өндөр дүн гаргаж байгаа эсэх.
    if request.senderDaysSinceLastTransaction is not None and request.senderDaysSinceLastTransaction >= 30 and _is_high_amount(source_currency, request.amount):
        score += 25
        triggered_rules.append("DORMANT_ACCOUNT_ACTIVITY")
        reasons.append("Удаан хугацаанд гүйлгээгүй данснаас өндөр дүнтэй гүйлгээ хийсэн байна.")

    # Богино хугацаанд олон хүлээн авагч руу тараасан эсэх.
    if request.distinctReceiverCountLast24Hours >= 5:
        score += 25
        triggered_rules.append("MANY_RECEIVERS_SHORT_TIME")
        reasons.append("Богино хугацаанд олон өөр хүлээн авагч руу мөнгө шилжүүлсэн байна.")

    # Нэг данс руу олон өөр хэрэглэгчээс мөнгө төвлөрсөн эсэх.
    if request.distinctSenderCountToReceiverLast24Hours >= 5:
        score += 30
        triggered_rules.append("MANY_SENDERS_TO_ONE_ACCOUNT")
        reasons.append("Нэг хүлээн авагч данс руу богино хугацаанд олон өөр хэрэглэгчээс мөнгө орсон байна.")

    # Хэт ерөнхий, нуусан мэт гүйлгээний утга.
    if _has_generic_or_hidden_description(request.description):
        score += 10
        triggered_rules.append("GENERIC_OR_HIDDEN_DESCRIPTION")
        reasons.append("Гүйлгээний утга хэт ерөнхий эсвэл санаатай нуусан мэт байна.")

    # Гүйлгээний утга бага хэрэглээ мэт боловч дүн хэт өндөр эсэх.
    if _description_amount_mismatch(source_currency, request.amount, request.description):
        score += 20
        triggered_rules.append("DESCRIPTION_AMOUNT_MISMATCH")
        reasons.append("Гүйлгээний утга болон мөнгөн дүн хоорондоо нийцэхгүй байна.")

    score = min(score, 100)
    if not reasons:
        reasons.append("Rule-based шалгалтаар өндөр эрсдэл илрээгүй.")

    return SuspiciousDetectionResponse(
        isSuspicious=score >= settings.suspicious_threshold,
        riskScore=score,
        reason=" ".join(reasons),
        triggeredRules=triggered_rules,
    )


def _is_very_high_amount(source_currency: str, amount: Decimal) -> bool:
    """Маш өндөр дүнгийн босго."""

    if source_currency == "MNT":
        return amount >= Decimal("5000000")
    if source_currency == "USD":
        return amount >= Decimal("1500")
    return amount >= Decimal("1500")


def _is_high_cross_currency_amount(source_currency: str, amount: Decimal) -> bool:
    """Валют хөрвүүлэлтийн өндөр дүнгийн босго."""

    if source_currency == "MNT":
        return amount >= Decimal("1000000")
    if source_currency == "USD":
        return amount >= Decimal("300")
    return amount >= Decimal("300")


def _is_high_amount(source_currency: str, amount: Decimal) -> bool:
    """Context rule-д ашиглах дунд-өндөр дүнгийн босго."""

    if source_currency == "MNT":
        return amount >= Decimal("1000000")
    if source_currency == "USD":
        return amount >= Decimal("300")
    return amount >= Decimal("300")


def _looks_like_structuring(source_currency: str, small_transaction_total: Decimal) -> bool:
    """Олон жижиг гүйлгээний нийлбэр өндөр эсэх."""

    if source_currency == "MNT":
        return small_transaction_total >= Decimal("5000000")
    if source_currency == "USD":
        return small_transaction_total >= Decimal("1500")
    return small_transaction_total >= Decimal("1500")


def _has_suspicious_keyword(description: str | None) -> bool:
    """Гүйлгээний утганд эрсдэлтэй үг байгаа эсэх."""

    if not description:
        return False

    normalized = description.lower()
    return any(keyword in normalized for keyword in SUSPICIOUS_KEYWORDS)


def _has_generic_or_hidden_description(description: str | None) -> bool:
    """Гүйлгээний утга хэт ерөнхий эсэх."""

    if not description:
        return True

    normalized = description.strip().lower()
    generic_values = {
        ".",
        "..",
        "...",
        "aaa",
        "test",
        "payment",
        "transfer",
        "other",
        "бусад",
        "юм",
        "шилжүүлэг",
        "гүйлгээ",
        "төлбөр",
    }
    return normalized in generic_values or len(normalized) <= 2


def _description_amount_mismatch(source_currency: str, amount: Decimal, description: str | None) -> bool:
    """Бага хэрэглээний утгатай боловч өндөр дүнтэй эсэх."""

    if not description:
        return False

    normalized = description.lower()
    low_value_keywords = (
        "хоол", "хүнс", "кофе", "унаа", "такси", "автобус", "цай",
        "food", "coffee", "taxi", "bus", "snack"
    )
    if not any(keyword in normalized for keyword in low_value_keywords):
        return False

    if source_currency == "MNT":
        return amount >= Decimal("3000000")
    if source_currency == "USD":
        return amount >= Decimal("1000")
    return amount >= Decimal("1000")
