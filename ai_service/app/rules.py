"""Сэжигтэй гүйлгээний rule-based оноо бодох хэсэг."""

from decimal import Decimal
from app.config import settings
from app.models import SuspiciousDetectionRequest, SuspiciousDetectionResponse, SuspiciousDetectionRuleSetting


DEFAULT_RULE_SETTINGS: dict[str, dict[str, Decimal | int | bool | None]] = {
    "HIGH_AMOUNT_COMPARED_TO_AVERAGE": {"enabled": True, "score": 35, "numeric": Decimal("3")},
    "VERY_HIGH_AMOUNT": {"enabled": True, "score": 30, "mnt": Decimal("5000000"), "usd": Decimal("1500")},
    "NIGHT_TIME_TRANSACTION": {"enabled": True, "score": 15, "numeric": Decimal("6")},
    "MANY_TRANSACTIONS_LAST_24_HOURS": {"enabled": True, "score": 20, "numeric": Decimal("5")},
    "HIGH_CROSS_CURRENCY_TRANSACTION": {"enabled": True, "score": 15, "mnt": Decimal("1000000"), "usd": Decimal("300")},
    "SUSPICIOUS_DESCRIPTION_KEYWORD": {"enabled": True, "score": 10},
    "MANY_SMALL_TRANSACTIONS": {"enabled": True, "score": 20, "numeric": Decimal("10")},
    "STRUCTURING_SMALL_SPLIT_TRANSFERS": {"enabled": True, "score": 35, "mnt": Decimal("5000000"), "usd": Decimal("1500")},
    "RAPID_IN_OUT_FLOW": {"enabled": True, "score": 30, "numeric": Decimal("0.8")},
    "NEW_ACCOUNT_HIGH_AMOUNT": {"enabled": True, "score": 25, "numeric": Decimal("7"), "mnt": Decimal("1000000"), "usd": Decimal("300")},
    "DORMANT_ACCOUNT_ACTIVITY": {"enabled": True, "score": 25, "numeric": Decimal("30"), "mnt": Decimal("1000000"), "usd": Decimal("300")},
    "MANY_RECEIVERS_SHORT_TIME": {"enabled": True, "score": 25, "numeric": Decimal("5")},
    "MANY_SENDERS_TO_ONE_ACCOUNT": {"enabled": True, "score": 30, "numeric": Decimal("5")},
    "GENERIC_OR_HIDDEN_DESCRIPTION": {"enabled": True, "score": 10},
    "DESCRIPTION_AMOUNT_MISMATCH": {"enabled": True, "score": 20, "mnt": Decimal("3000000"), "usd": Decimal("1000")},
}


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
    rule_settings = _build_rule_settings(request)

    # Хэрэглэгчийн өмнөх дундаж дүнгээс огцом өндөр эсэх.
    rule_code = "HIGH_AMOUNT_COMPARED_TO_AVERAGE"
    if _is_enabled(rule_settings, rule_code) and request.senderAverageAmountLast30Days > 0 and request.amount >= request.senderAverageAmountLast30Days * _numeric(rule_settings, rule_code):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Гүйлгээний дүн хэрэглэгчийн сүүлийн 30 хоногийн дундаж гүйлгээнээс өндөр байна.")

    # Валют бүрийн маш өндөр дүнгийн босго.
    rule_code = "VERY_HIGH_AMOUNT"
    if _is_enabled(rule_settings, rule_code) and _is_amount_at_least(rule_settings, rule_code, source_currency, request.amount):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Гүйлгээний дүн өндөр дүнтэй ангилалд орж байна.")

    # Шөнийн цагт хийсэн эсэх.
    rule_code = "NIGHT_TIME_TRANSACTION"
    if _is_enabled(rule_settings, rule_code) and 0 <= request.createdHour < int(_numeric(rule_settings, rule_code)):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Гүйлгээ шөнийн цагаар хийгдсэн байна.")

    # 24 цагийн дотор олон гүйлгээ хийсэн эсэх.
    rule_code = "MANY_TRANSACTIONS_LAST_24_HOURS"
    if _is_enabled(rule_settings, rule_code) and request.senderTransactionCountLast24Hours >= int(_numeric(rule_settings, rule_code)):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Сүүлийн 24 цагт олон гүйлгээ хийсэн байна.")

    # Өндөр дүнтэй валют хөрвүүлэлт.
    rule_code = "HIGH_CROSS_CURRENCY_TRANSACTION"
    if _is_enabled(rule_settings, rule_code) and request.isCrossCurrency and _is_amount_at_least(rule_settings, rule_code, source_currency, request.amount):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Өндөр дүнтэй валют хөрвүүлсэн гүйлгээ байна.")

    # Гүйлгээний утгад эрсдэлтэй түлхүүр үг байгаа эсэх.
    rule_code = "SUSPICIOUS_DESCRIPTION_KEYWORD"
    if _is_enabled(rule_settings, rule_code) and _has_suspicious_keyword(request.description):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Гүйлгээний утгад анхаарах түлхүүр үг илэрсэн байна.")

    # Олон жижиг дүнтэй гүйлгээ.
    rule_code = "MANY_SMALL_TRANSACTIONS"
    if _is_enabled(rule_settings, rule_code) and request.smallTransactionCountLast24Hours >= int(_numeric(rule_settings, rule_code)):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Сүүлийн 24 цагт олон жижиг дүнтэй гүйлгээ хийсэн байна.")

    # Жижиглэж шилжүүлсэн нийт дүн өндөр эсэх.
    rule_code = "STRUCTURING_SMALL_SPLIT_TRANSFERS"
    if _is_enabled(rule_settings, rule_code) and _is_amount_at_least(rule_settings, rule_code, source_currency, request.smallTransactionTotalLast24Hours):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Том дүнг жижиглэн олон удаа шилжүүлсэн байж болзошгүй байна.")

    # Орж ирсэн мөнгийг богино хугацаанд гаргасан эсэх.
    rule_code = "RAPID_IN_OUT_FLOW"
    if _is_enabled(rule_settings, rule_code) and request.recentInboundAmountLast30Minutes > 0 and request.amount >= request.recentInboundAmountLast30Minutes * _numeric(rule_settings, rule_code):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Дансанд орсон мөнгийг богино хугацаанд дахин гаргасан шинж илэрсэн байна.")

    # Шинэ данснаас өндөр дүн гарч байгаа эсэх.
    rule_code = "NEW_ACCOUNT_HIGH_AMOUNT"
    if _is_enabled(rule_settings, rule_code) and request.senderAccountAgeDays <= int(_numeric(rule_settings, rule_code)) and _is_amount_at_least(rule_settings, rule_code, source_currency, request.amount):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Шинэ данснаас өндөр дүнтэй гүйлгээ хийсэн байна.")

    # Удаан хөдөлгөөнгүй байсан данс өндөр дүн гаргаж байгаа эсэх.
    rule_code = "DORMANT_ACCOUNT_ACTIVITY"
    if _is_enabled(rule_settings, rule_code) and request.senderDaysSinceLastTransaction is not None and request.senderDaysSinceLastTransaction >= int(_numeric(rule_settings, rule_code)) and _is_amount_at_least(rule_settings, rule_code, source_currency, request.amount):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Удаан хугацаанд гүйлгээгүй данснаас өндөр дүнтэй гүйлгээ хийсэн байна.")

    # Богино хугацаанд олон хүлээн авагч руу тараасан эсэх.
    rule_code = "MANY_RECEIVERS_SHORT_TIME"
    if _is_enabled(rule_settings, rule_code) and request.distinctReceiverCountLast24Hours >= int(_numeric(rule_settings, rule_code)):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Богино хугацаанд олон өөр хүлээн авагч руу мөнгө шилжүүлсэн байна.")

    # Нэг данс руу олон өөр хэрэглэгчээс мөнгө төвлөрсөн эсэх.
    rule_code = "MANY_SENDERS_TO_ONE_ACCOUNT"
    if _is_enabled(rule_settings, rule_code) and request.distinctSenderCountToReceiverLast24Hours >= int(_numeric(rule_settings, rule_code)):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Нэг хүлээн авагч данс руу богино хугацаанд олон өөр хэрэглэгчээс мөнгө орсон байна.")

    # Хэт ерөнхий, нуусан мэт гүйлгээний утга.
    rule_code = "GENERIC_OR_HIDDEN_DESCRIPTION"
    if _is_enabled(rule_settings, rule_code) and _has_generic_or_hidden_description(request.description):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Гүйлгээний утга хэт ерөнхий эсвэл санаатай нуусан мэт байна.")

    # Гүйлгээний утга бага хэрэглээ мэт боловч дүн хэт өндөр эсэх.
    rule_code = "DESCRIPTION_AMOUNT_MISMATCH"
    if _is_enabled(rule_settings, rule_code) and _description_amount_mismatch(rule_settings, rule_code, source_currency, request.amount, request.description):
        score += _score(rule_settings, rule_code)
        triggered_rules.append(rule_code)
        reasons.append("Гүйлгээний утга болон мөнгөн дүн хоорондоо нийцэхгүй байна.")

    score = min(score, 100)
    if not reasons:
        reasons.append("Rule-based шалгалтаар өндөр эрсдэл илрээгүй.")

    return SuspiciousDetectionResponse(
        isSuspicious=score >= _suspicious_threshold(request),
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


def _description_amount_mismatch(
    rule_settings: dict[str, SuspiciousDetectionRuleSetting],
    rule_code: str,
    source_currency: str,
    amount: Decimal,
    description: str | None
) -> bool:
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

    return _is_amount_at_least(rule_settings, rule_code, source_currency, amount)


def _build_rule_settings(request: SuspiciousDetectionRequest) -> dict[str, SuspiciousDetectionRuleSetting]:
    """Request-ээр ирсэн admin тохиргоог default утгатай нэгтгэнэ."""

    configured = {
        item.ruleCode.upper(): item
        for item in (request.detectionSettings.rules if request.detectionSettings else [])
    }
    merged: dict[str, SuspiciousDetectionRuleSetting] = {}

    for rule_code, defaults in DEFAULT_RULE_SETTINGS.items():
        item = configured.get(rule_code)
        merged[rule_code] = SuspiciousDetectionRuleSetting(
            ruleCode=rule_code,
            isEnabled=item.isEnabled if item is not None else bool(defaults.get("enabled", True)),
            score=item.score if item is not None else int(defaults.get("score", 0)),
            numericThreshold=item.numericThreshold if item is not None and item.numericThreshold is not None else defaults.get("numeric"),
            amountThresholdMnt=item.amountThresholdMnt if item is not None and item.amountThresholdMnt is not None else defaults.get("mnt"),
            amountThresholdUsd=item.amountThresholdUsd if item is not None and item.amountThresholdUsd is not None else defaults.get("usd"),
        )

    return merged


def _suspicious_threshold(request: SuspiciousDetectionRequest) -> int:
    if request.detectionSettings is not None:
        return request.detectionSettings.suspiciousThreshold
    return settings.suspicious_threshold


def _is_enabled(rule_settings: dict[str, SuspiciousDetectionRuleSetting], rule_code: str) -> bool:
    return rule_settings[rule_code].isEnabled


def _score(rule_settings: dict[str, SuspiciousDetectionRuleSetting], rule_code: str) -> int:
    return rule_settings[rule_code].score


def _numeric(rule_settings: dict[str, SuspiciousDetectionRuleSetting], rule_code: str) -> Decimal:
    value = rule_settings[rule_code].numericThreshold
    default_value = DEFAULT_RULE_SETTINGS[rule_code].get("numeric")
    return value if value is not None else Decimal(default_value or 0)


def _amount_threshold(rule_settings: dict[str, SuspiciousDetectionRuleSetting], rule_code: str, source_currency: str) -> Decimal:
    rule = rule_settings[rule_code]
    if source_currency == "MNT":
        return rule.amountThresholdMnt or Decimal(DEFAULT_RULE_SETTINGS[rule_code].get("mnt") or 0)
    return rule.amountThresholdUsd or Decimal(DEFAULT_RULE_SETTINGS[rule_code].get("usd") or 0)


def _is_amount_at_least(
    rule_settings: dict[str, SuspiciousDetectionRuleSetting],
    rule_code: str,
    source_currency: str,
    amount: Decimal
) -> bool:
    return amount >= _amount_threshold(rule_settings, rule_code, source_currency)
