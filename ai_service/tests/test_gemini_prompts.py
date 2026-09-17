"""Prompt refactor-ийн regression: API key, DB, сүлжээ шаардлагагүй."""
from decimal import Decimal
from pathlib import Path
import hashlib
import json
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from fastapi import HTTPException
from app import gemini_client, gemini_prompts
from app.models import BankInfoChatMessage, GeminiAnalysisContext


def sample_context():
    return GeminiAnalysisContext(
        transactionId=42, createdAt="2026-06-01T12:30:00",
        amount=Decimal("10.25"), sourceCurrency="USD",
        creditedAmount=Decimal("35000.99"), targetCurrency="MNT",
        riskScore=Decimal("60"), suspiciousReason="Шалгах шаардлагатай",
        reviewStatus="PENDING", fromAccountMasked="******1234",
        toAccountMasked="******5678", description="Хоол", isCrossCurrency=True,
        exchangeRateValue=Decimal("3414.73"), detectionCheckedAt="2026-06-01T12:30:01",
    )


def prompt_samples(module):
    context = sample_context()
    history = [BankInfoChatMessage(role="user", content="  Сайн   байна уу? "), BankInfoChatMessage(role="assistant", content="Сайн байна уу.")]
    return {
        "structured": module._build_structured_prompt(context),
        "narrative": module._build_narrative_prompt(context),
        "follow_up": module._build_follow_up_prompt(context, "Өмнөх дүгнэлт", "Яагаад?"),
        "public": module._build_bank_info_prompt("Данс нээх үү?", history),
        "finance": module._build_user_finance_prompt("Үлдэгдэл?", {"balance": "1200.50", "currency": "MNT"}, history),
    }


class GeminiPromptTests(unittest.TestCase):
    def test_prompt_text_matches_pre_refactor_snapshots(self):
        expected = json.loads(Path(__file__).with_name("prompt_hashes.json").read_text(encoding="utf-8"))
        for name, prompt in prompt_samples(gemini_prompts).items():
            with self.subTest(prompt=name):
                self.assertEqual(hashlib.sha256(prompt.encode("utf-8")).hexdigest(), expected[name])

    def test_history_normalizes_whitespace_and_limits_each_message(self):
        history = [BankInfoChatMessage(role="assistant", content=" a \n b "), BankInfoChatMessage(role="user", content="x" * 800)]
        self.assertEqual(gemini_prompts._format_bank_info_history(history), "Assistant: a b\nUser: " + "x" * 700)
        self.assertEqual(gemini_prompts._format_bank_info_history([]), "-")

    def test_money_keeps_decimal_precision(self):
        self.assertEqual(gemini_prompts._format_decimal(Decimal("9999999999999999.99")), "9999999999999999.99")

    @patch("app.gemini_client._generate_text")
    def test_structured_response_keeps_normalization_and_score_clamping(self, generate):
        generate.return_value = ('{"isSuspicious":true,"riskScore":120,"explanation":"**Шалгах**","recommendedAction":"  Review  "}', "model")
        response = gemini_client.analyze_transaction(sample_context())
        self.assertTrue(response.isSuspicious)
        self.assertEqual(response.riskScore, Decimal("100"))
        self.assertEqual(response.explanation, "Шалгах")
        self.assertEqual(response.recommendedAction, "Review")
        self.assertEqual(response.modelName, "model")
        self.assertEqual(generate.call_args.args[0], gemini_prompts._build_structured_prompt(sample_context()))

    @patch("app.gemini_client._generate_text")
    def test_non_json_analysis_remains_plain_text_fallback(self, generate):
        generate.return_value = ("**Тайлбар**", "model")
        response = gemini_client.analyze_transaction(sample_context())
        self.assertIsNone(response.isSuspicious)
        self.assertIsNone(response.riskScore)
        self.assertEqual(response.explanation, "Тайлбар")

    @patch("app.gemini_client._generate_text")
    def test_blank_questions_rejected_without_calling_gemini(self, generate):
        calls = [lambda: gemini_client.answer_follow_up(sample_context(), "", " "), lambda: gemini_client.answer_bank_info_question("\n"), lambda: gemini_client.answer_user_finance_question("", {})]
        for call in calls:
            with self.assertRaises(HTTPException) as error:
                call()
            self.assertEqual(error.exception.status_code, 400)
        generate.assert_not_called()

    @patch("app.gemini_client._generate_text")
    def test_public_and_finance_chat_keep_prompt_and_token_limits(self, generate):
        generate.return_value = ("**Хариу**", "model")
        self.assertEqual(gemini_client.answer_bank_info_question(" Данс? "), "Хариу")
        self.assertEqual(generate.call_args.args[0], gemini_prompts._build_bank_info_prompt("Данс?", []))
        self.assertEqual(generate.call_args.kwargs["max_output_tokens"], 850)
        self.assertEqual(gemini_client.answer_user_finance_question(" Үлдэгдэл? ", {}), "Хариу")
        self.assertEqual(generate.call_args.args[0], gemini_prompts._build_user_finance_prompt("Үлдэгдэл?", {}, []))
        self.assertEqual(generate.call_args.kwargs["max_output_tokens"], 1100)


if __name__ == "__main__":
    unittest.main()
