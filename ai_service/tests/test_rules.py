"""Rule-based detection-ийн автомат unit test-үүд.

Эдгээр test DB ашиглахгүй. FastAPI service асаах шаардлагагүйгээр
`detect_suspicious()` function-ийг шууд шалгана.
"""

from decimal import Decimal
from pathlib import Path
import sys
import unittest


sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from app.models import SuspiciousDetectionRequest
from app.rules import detect_suspicious


def build_request(**overrides) -> SuspiciousDetectionRequest:
    """Default normal transaction context дээр override хийж test payload үүсгэнэ."""

    payload = {
        "transactionId": 1,
        "senderUserId": 10,
        "amount": Decimal("100000"),
        "sourceCurrency": "MNT",
        "creditedAmount": Decimal("100000"),
        "targetCurrency": "MNT",
        "isCrossCurrency": False,
        "description": "Хэрэглээний төлбөр",
        "createdHour": 14,
        "senderAverageAmountLast30Days": Decimal("80000"),
        "senderMaxAmountLast30Days": Decimal("150000"),
        "senderTransactionCountLast24Hours": 1,
        "smallTransactionCountLast24Hours": 0,
        "smallTransactionTotalLast24Hours": Decimal("0"),
        "distinctReceiverCountLast24Hours": 1,
        "distinctSenderCountToReceiverLast24Hours": 1,
        "recentInboundAmountLast30Minutes": Decimal("0"),
        "senderAccountAgeDays": 90,
        "senderDaysSinceLastTransaction": 1,
    }
    payload.update(overrides)
    return SuspiciousDetectionRequest(**payload)


class RuleDetectionTests(unittest.TestCase):
    def assert_rule_triggered(self, rule_code: str, **overrides) -> None:
        result = detect_suspicious(build_request(**overrides))
        self.assertIn(rule_code, result.triggeredRules)

    def test_normal_transfer_is_not_suspicious(self):
        result = detect_suspicious(build_request())

        self.assertFalse(result.isSuspicious)
        self.assertEqual(result.riskScore, 0)
        self.assertEqual(result.triggeredRules, [])

    def test_high_amount_compared_to_average(self):
        self.assert_rule_triggered(
            "HIGH_AMOUNT_COMPARED_TO_AVERAGE",
            amount=Decimal("500000"),
            senderAverageAmountLast30Days=Decimal("100000"),
        )

    def test_very_high_amount(self):
        result = detect_suspicious(build_request(amount=Decimal("6000000")))

        self.assertIn("VERY_HIGH_AMOUNT", result.triggeredRules)
        self.assertIn("HIGH_AMOUNT_COMPARED_TO_AVERAGE", result.triggeredRules)
        self.assertTrue(result.isSuspicious)

    def test_night_time_transaction(self):
        self.assert_rule_triggered("NIGHT_TIME_TRANSACTION", createdHour=2)

    def test_many_transactions_last_24_hours(self):
        self.assert_rule_triggered("MANY_TRANSACTIONS_LAST_24_HOURS", senderTransactionCountLast24Hours=5)

    def test_high_cross_currency_transaction(self):
        self.assert_rule_triggered(
            "HIGH_CROSS_CURRENCY_TRANSACTION",
            amount=Decimal("1200000"),
            creditedAmount=Decimal("335"),
            targetCurrency="USD",
            isCrossCurrency=True,
        )

    def test_suspicious_keyword(self):
        self.assert_rule_triggered("SUSPICIOUS_DESCRIPTION_KEYWORD", description="USDT coin payment")

    def test_many_small_transactions(self):
        self.assert_rule_triggered("MANY_SMALL_TRANSACTIONS", smallTransactionCountLast24Hours=10)

    def test_structuring_small_split_transfers(self):
        result = detect_suspicious(
            build_request(
                amount=Decimal("500000"),
                smallTransactionCountLast24Hours=10,
                smallTransactionTotalLast24Hours=Decimal("5000000"),
            )
        )

        self.assertIn("STRUCTURING_SMALL_SPLIT_TRANSFERS", result.triggeredRules)
        self.assertTrue(result.isSuspicious)

    def test_rapid_in_out_flow(self):
        self.assert_rule_triggered(
            "RAPID_IN_OUT_FLOW",
            amount=Decimal("900000"),
            recentInboundAmountLast30Minutes=Decimal("1000000"),
        )

    def test_new_account_high_amount(self):
        self.assert_rule_triggered(
            "NEW_ACCOUNT_HIGH_AMOUNT",
            amount=Decimal("1200000"),
            senderAccountAgeDays=3,
        )

    def test_dormant_account_activity(self):
        self.assert_rule_triggered(
            "DORMANT_ACCOUNT_ACTIVITY",
            amount=Decimal("1200000"),
            senderDaysSinceLastTransaction=45,
        )

    def test_many_receivers_short_time(self):
        self.assert_rule_triggered("MANY_RECEIVERS_SHORT_TIME", distinctReceiverCountLast24Hours=5)

    def test_many_senders_to_one_account(self):
        self.assert_rule_triggered("MANY_SENDERS_TO_ONE_ACCOUNT", distinctSenderCountToReceiverLast24Hours=5)

    def test_generic_or_hidden_description(self):
        self.assert_rule_triggered("GENERIC_OR_HIDDEN_DESCRIPTION", description=".")

    def test_description_amount_mismatch(self):
        self.assert_rule_triggered(
            "DESCRIPTION_AMOUNT_MISMATCH",
            amount=Decimal("3000000"),
            description="хоол хүнс",
        )


if __name__ == "__main__":
    unittest.main()
