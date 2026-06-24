using System;
using System.Collections.Generic;
using BankWebApp.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Data;

public partial class BankDbContext : DbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<AdminSuspiciousTransactionView> AdminSuspiciousTransactionViews { get; set; }

    public virtual DbSet<AiTransactionAnalysisLog> AiTransactionAnalysisLogs { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<ChatLog> ChatLogs { get; set; }

    public virtual DbSet<CurrencyRateSetting> CurrencyRateSettings { get; set; }

    public virtual DbSet<CurrencyRateSettingAudit> CurrencyRateSettingAudits { get; set; }

    public virtual DbSet<CurrencyRateOverrideSchedule> CurrencyRateOverrideSchedules { get; set; }

    public virtual DbSet<ExchangeRateLog> ExchangeRateLogs { get; set; }

    public virtual DbSet<FxIncomeLog> FxIncomeLogs { get; set; }

    public virtual DbSet<FraudDetectionSetting> FraudDetectionSettings { get; set; }

    public virtual DbSet<FraudRuleSetting> FraudRuleSettings { get; set; }

    public virtual DbSet<KnowledgeBase> KnowledgeBases { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<SecurityEventLog> SecurityEventLogs { get; set; }

    public virtual DbSet<SuspiciousTransactionDetail> SuspiciousTransactionDetails { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<TransactionDetectionLog> TransactionDetectionLogs { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserTransactionView> UserTransactionViews { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_accounts");

            entity.ToTable("accounts", tb => tb.HasTrigger("trg_accounts_set_updated_at"));

            entity.HasIndex(e => e.Currency, "idx_accounts_currency");

            entity.HasIndex(e => e.IsActive, "idx_accounts_is_active");

            entity.HasIndex(e => e.IsPrimary, "idx_accounts_is_primary");

            entity.HasIndex(e => e.UserId, "idx_accounts_user_id");

            entity.HasIndex(e => e.UserId, "ux_accounts_one_primary_per_user")
                .IsUnique()
                .HasFilter("[is_primary] = 1");

            entity.HasIndex(e => e.AccountNumber, "uq_accounts_account_number").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AccountNumber)
                .HasMaxLength(30)
                .HasColumnName("account_number");
            entity.Property(e => e.AccountType)
                .HasMaxLength(20)
                .HasDefaultValue("CHECKING", "df_accounts_account_type")
                .HasColumnName("account_type");
            entity.Property(e => e.Balance)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("balance");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_accounts_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .HasColumnName("currency");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true, "df_accounts_is_active")
                .HasColumnName("is_active");
            entity.Property(e => e.IsPrimary)
                .HasDefaultValue(false, "df_accounts_is_primary")
                .HasColumnName("is_primary");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_accounts_updated_at")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_accounts_user");
        });

        modelBuilder.Entity<AdminSuspiciousTransactionView>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("admin_suspicious_transaction_view");

            entity.Property(e => e.AiExplanation).HasColumnName("ai_explanation");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreditedAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("credited_amount");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.ExchangeRateValue)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("exchange_rate_value");
            entity.Property(e => e.FromAccountId).HasColumnName("from_account_id");
            entity.Property(e => e.ReviewNote).HasColumnName("review_note");
            entity.Property(e => e.ReviewStatus)
                .HasMaxLength(30)
                .HasColumnName("review_status");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.RiskScore)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("risk_score");
            entity.Property(e => e.SourceCurrency)
                .HasMaxLength(3)
                .HasColumnName("source_currency");
            entity.Property(e => e.Status)
                .HasMaxLength(40)
                .HasColumnName("status");
            entity.Property(e => e.SuspiciousReason).HasColumnName("suspicious_reason");
            entity.Property(e => e.TargetCurrency)
                .HasMaxLength(3)
                .HasColumnName("target_currency");
            entity.Property(e => e.ToAccountId).HasColumnName("to_account_id");
            entity.Property(e => e.TransactionId).HasColumnName("transaction_id");
        });

        modelBuilder.Entity<AiTransactionAnalysisLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_ai_transaction_analysis_logs");

            entity.ToTable("ai_transaction_analysis_logs");

            entity.HasIndex(e => e.CreatedAt, "idx_ai_analysis_created_at");

            entity.HasIndex(e => e.IsSuspicious, "idx_ai_analysis_is_suspicious");

            entity.HasIndex(e => e.TransactionId, "idx_ai_analysis_transaction_id");

            entity.HasIndex(e => new { e.TransactionId, e.CreatedAt }, "idx_ai_analysis_transaction_created");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AnalyzedBy).HasColumnName("analyzed_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(CONVERT([datetime2](0),sysutcdatetime() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time'))", "df_ai_analysis_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.Explanation).HasColumnName("explanation");
            entity.Property(e => e.IsSuspicious).HasColumnName("is_suspicious");
            entity.Property(e => e.ModelName)
                .HasMaxLength(100)
                .HasColumnName("model_name");
            entity.Property(e => e.RecommendedAction)
                .HasMaxLength(1000)
                .HasColumnName("recommended_action");
            entity.Property(e => e.RiskScore)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("risk_score");
            entity.Property(e => e.SourceContextJson).HasColumnName("source_context_json");
            entity.Property(e => e.TransactionId).HasColumnName("transaction_id");

            entity.HasOne(d => d.AnalyzedByNavigation).WithMany(p => p.AiTransactionAnalysisLogs)
                .HasForeignKey(d => d.AnalyzedBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_ai_analysis_admin_user");

            entity.HasOne(d => d.Transaction).WithMany(p => p.AiTransactionAnalysisLogs)
                .HasForeignKey(d => d.TransactionId)
                .HasConstraintName("fk_ai_analysis_transaction");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_audit_logs");

            entity.ToTable("audit_logs");

            entity.HasIndex(e => e.Action, "idx_audit_action");

            entity.HasIndex(e => e.CreatedAt, "idx_audit_created_at");

            entity.HasIndex(e => new { e.TargetType, e.TargetId }, "idx_audit_target");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "idx_audit_user_created");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .HasColumnName("action");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_audit_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.Detail).HasColumnName("detail");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .HasColumnName("ip_address");
            entity.Property(e => e.NewValue).HasColumnName("new_value");
            entity.Property(e => e.OldValue).HasColumnName("old_value");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.TargetType)
                .HasMaxLength(50)
                .HasColumnName("target_type");
            entity.Property(e => e.UserAgent)
                .HasMaxLength(255)
                .HasColumnName("user_agent");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_audit_user");
        });

        modelBuilder.Entity<ChatLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_chat_logs");

            entity.ToTable("chat_logs");

            entity.HasIndex(e => e.IntentType, "idx_chat_intent");

            entity.HasIndex(e => e.RelatedTransactionId, "idx_chat_related_transaction");

            entity.HasIndex(e => e.SessionId, "idx_chat_session");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "idx_chat_user_created");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BotResponse).HasColumnName("bot_response");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_chat_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.IntentType)
                .HasMaxLength(40)
                .HasDefaultValue("UNKNOWN", "df_chat_intent")
                .HasColumnName("intent_type");
            entity.Property(e => e.RelatedTransactionId).HasColumnName("related_transaction_id");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.UsedContextType)
                .HasMaxLength(100)
                .HasColumnName("used_context_type");
            entity.Property(e => e.UsedKnowledgeBaseIds)
                .HasMaxLength(500)
                .HasColumnName("used_knowledge_base_ids");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.UserMessage).HasColumnName("user_message");

            entity.HasOne(d => d.RelatedTransaction).WithMany(p => p.ChatLogs)
                .HasForeignKey(d => d.RelatedTransactionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_chat_related_transaction");

            entity.HasOne(d => d.User).WithMany(p => p.ChatLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_chat_user");
        });

        modelBuilder.Entity<CurrencyRateSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_currency_rate_settings");

            entity.ToTable("currency_rate_settings");

            entity.HasIndex(e => new { e.CurrencyCode, e.BaseCurrency }, "uq_currency_rate_settings_pair").IsUnique();

            entity.HasIndex(e => e.IsManualOverride, "idx_currency_rate_settings_manual");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AlgoBuyRate)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("algo_buy_rate");
            entity.Property(e => e.AlgoBuyMarginPercent)
                .HasColumnType("decimal(9, 6)")
                .HasDefaultValue(0.000500m, "df_currency_rate_settings_buy_margin")
                .HasColumnName("algo_buy_margin_percent");
            entity.Property(e => e.AlgoSellMarginPercent)
                .HasColumnType("decimal(9, 6)")
                .HasDefaultValue(0.001000m, "df_currency_rate_settings_sell_margin")
                .HasColumnName("algo_sell_margin_percent");
            entity.Property(e => e.AlgoSellRate)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("algo_sell_rate");
            entity.Property(e => e.BaseCurrency)
                .HasMaxLength(3)
                .HasDefaultValue("MNT", "df_currency_rate_settings_base_currency")
                .HasColumnName("base_currency");
            entity.Property(e => e.BaseRate)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("base_rate");
            entity.Property(e => e.CurrencyCode)
                .HasMaxLength(3)
                .HasColumnName("currency_code");
            entity.Property(e => e.FetchedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_currency_rate_settings_fetched_at")
                .HasColumnName("fetched_at");
            entity.Property(e => e.IsManualOverride)
                .HasDefaultValue(false, "df_currency_rate_settings_manual")
                .HasColumnName("is_manual_override");
            entity.Property(e => e.ManualBuyRate)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("manual_buy_rate");
            entity.Property(e => e.ManualExpiresAt).HasColumnName("manual_expires_at");
            entity.Property(e => e.ManualSellRate)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("manual_sell_rate");
            entity.Property(e => e.RateDate).HasColumnName("rate_date");
            entity.Property(e => e.Source)
                .HasMaxLength(100)
                .HasDefaultValue("MNB_API", "df_currency_rate_settings_source")
                .HasColumnName("source");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_currency_rate_settings_updated_at")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany()
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_currency_rate_settings_updated_by");
        });

        modelBuilder.Entity<CurrencyRateOverrideSchedule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_currency_rate_override_schedules");

            entity.ToTable("currency_rate_override_schedules", tb => tb.HasTrigger("trg_currency_rate_override_schedules_prevent_overlap"));

            entity.HasIndex(e => new { e.CurrencyRateSettingId, e.StartsAt, e.EndsAt }, "idx_currency_rate_override_schedules_setting_time");

            entity.HasIndex(e => e.Status, "idx_currency_rate_override_schedules_status");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at");
            entity.Property(e => e.CancelledBy).HasColumnName("cancelled_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(CONVERT([datetime2](0),sysutcdatetime() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time'))", "df_currency_rate_override_schedules_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CurrencyRateSettingId).HasColumnName("currency_rate_setting_id");
            entity.Property(e => e.EndsAt).HasColumnName("ends_at");
            entity.Property(e => e.ManualBuyRate)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("manual_buy_rate");
            entity.Property(e => e.ManualSellRate)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("manual_sell_rate");
            entity.Property(e => e.Note)
                .HasMaxLength(500)
                .HasColumnName("note");
            entity.Property(e => e.StartsAt).HasColumnName("starts_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("SCHEDULED", "df_currency_rate_override_schedules_status")
                .HasColumnName("status");

            entity.HasOne(d => d.CancelledByNavigation).WithMany()
                .HasForeignKey(d => d.CancelledBy)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("fk_currency_rate_override_schedules_cancelled_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_currency_rate_override_schedules_created_by");

            entity.HasOne(d => d.CurrencyRateSetting).WithMany(p => p.CurrencyRateOverrideSchedules)
                .HasForeignKey(d => d.CurrencyRateSettingId)
                .HasConstraintName("fk_currency_rate_override_schedules_setting");
        });

        modelBuilder.Entity<CurrencyRateSettingAudit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_currency_rate_setting_audits");

            entity.ToTable("currency_rate_setting_audits");

            entity.HasIndex(e => new { e.ChangedBy, e.ChangedAt }, "idx_currency_rate_setting_audits_changed_by");

            entity.HasIndex(e => new { e.CurrencyRateSettingId, e.ChangedAt }, "idx_currency_rate_setting_audits_setting");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(50)
                .HasColumnName("action");
            entity.Property(e => e.ChangedAt)
                .HasDefaultValueSql("(CONVERT([datetime2](0),sysutcdatetime() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time'))", "df_currency_rate_setting_audits_changed_at")
                .HasColumnName("changed_at");
            entity.Property(e => e.ChangedBy).HasColumnName("changed_by");
            entity.Property(e => e.CurrencyRateSettingId).HasColumnName("currency_rate_setting_id");
            entity.Property(e => e.NewBuyRate)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("new_buy_rate");
            entity.Property(e => e.NewIsManualOverride).HasColumnName("new_is_manual_override");
            entity.Property(e => e.NewManualExpiresAt).HasColumnName("new_manual_expires_at");
            entity.Property(e => e.NewSellRate)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("new_sell_rate");
            entity.Property(e => e.Note)
                .HasMaxLength(500)
                .HasColumnName("note");
            entity.Property(e => e.OldBuyRate)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("old_buy_rate");
            entity.Property(e => e.OldIsManualOverride).HasColumnName("old_is_manual_override");
            entity.Property(e => e.OldManualExpiresAt).HasColumnName("old_manual_expires_at");
            entity.Property(e => e.OldSellRate)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("old_sell_rate");

            entity.HasOne(d => d.ChangedByNavigation).WithMany()
                .HasForeignKey(d => d.ChangedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_currency_rate_setting_audits_changed_by");

            entity.HasOne(d => d.CurrencyRateSetting).WithMany(p => p.CurrencyRateSettingAudits)
                .HasForeignKey(d => d.CurrencyRateSettingId)
                .HasConstraintName("fk_currency_rate_setting_audits_setting");
        });

        modelBuilder.Entity<ExchangeRateLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_exchange_rate_logs");

            entity.ToTable("exchange_rate_logs");

            entity.HasIndex(e => e.FetchedAt, "idx_exchange_fetched_at");

            entity.HasIndex(e => new { e.FromCurrency, e.ToCurrency }, "idx_exchange_pair");

            entity.HasIndex(e => e.RateDate, "idx_exchange_rate_date");

            entity.HasIndex(e => new { e.FromCurrency, e.ToCurrency, e.RateDate, e.Source }, "uq_exchange_rate_pair_date").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FetchedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_exchange_fetched_at")
                .HasColumnName("fetched_at");
            entity.Property(e => e.FromCurrency)
                .HasMaxLength(3)
                .HasColumnName("from_currency");
            entity.Property(e => e.Rate)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("rate");
            entity.Property(e => e.RateDate).HasColumnName("rate_date");
            entity.Property(e => e.Source)
                .HasMaxLength(100)
                .HasDefaultValue("MNB_API", "df_exchange_source")
                .HasColumnName("source");
            entity.Property(e => e.ToCurrency)
                .HasMaxLength(3)
                .HasColumnName("to_currency");
        });

        modelBuilder.Entity<FxIncomeLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_fx_income_logs");

            entity.ToTable("fx_income_logs");

            entity.HasIndex(e => e.CreatedAt, "idx_fx_income_created_at");

            entity.HasIndex(e => e.IncomeType, "idx_fx_income_type");

            entity.HasIndex(e => e.RateDate, "idx_fx_income_rate_date");

            entity.HasIndex(e => e.TransactionId, "uq_fx_income_transaction").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_fx_income_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.CreditedAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("credited_amount");
            entity.Property(e => e.CustomerRateMntPerUsd)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("customer_rate_mnt_per_usd");
            entity.Property(e => e.FromCurrency)
                .HasMaxLength(3)
                .HasColumnName("from_currency");
            entity.Property(e => e.IncomeAmountMnt)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("income_amount_mnt");
            entity.Property(e => e.IncomeType)
                .HasMaxLength(30)
                .HasColumnName("income_type");
            entity.Property(e => e.OfficialRateMntPerUsd)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("official_rate_mnt_per_usd");
            entity.Property(e => e.RateDate).HasColumnName("rate_date");
            entity.Property(e => e.Source)
                .HasMaxLength(100)
                .HasColumnName("source");
            entity.Property(e => e.SourceAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("source_amount");
            entity.Property(e => e.SpreadMarginMntPerUsd)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("spread_margin_mnt_per_usd");
            entity.Property(e => e.ToCurrency)
                .HasMaxLength(3)
                .HasColumnName("to_currency");
            entity.Property(e => e.TransactionId).HasColumnName("transaction_id");

            entity.HasOne(d => d.Transaction).WithOne(p => p.FxIncomeLog)
                .HasForeignKey<FxIncomeLog>(d => d.TransactionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_fx_income_transaction");
        });

        modelBuilder.Entity<FraudDetectionSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_fraud_detection_settings");

            entity.ToTable("fraud_detection_settings");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.SuspiciousThreshold)
                .HasDefaultValue(60, "df_fraud_detection_settings_threshold")
                .HasColumnName("suspicious_threshold");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(CONVERT([datetime2](0),sysutcdatetime() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time'))", "df_fraud_detection_settings_updated_at")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany()
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_fraud_detection_settings_updated_by");
        });

        modelBuilder.Entity<FraudRuleSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_fraud_rule_settings");

            entity.ToTable("fraud_rule_settings");

            entity.HasIndex(e => e.IsEnabled, "idx_fraud_rule_settings_enabled");

            entity.HasIndex(e => e.RuleCode, "uq_fraud_rule_settings_rule_code").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AmountThresholdMnt)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("amount_threshold_mnt");
            entity.Property(e => e.AmountThresholdUsd)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("amount_threshold_usd");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(160)
                .HasColumnName("display_name");
            entity.Property(e => e.IsEnabled)
                .HasDefaultValue(true, "df_fraud_rule_settings_enabled")
                .HasColumnName("is_enabled");
            entity.Property(e => e.NumericThreshold)
                .HasColumnType("decimal(18, 4)")
                .HasColumnName("numeric_threshold");
            entity.Property(e => e.RuleCode)
                .HasMaxLength(80)
                .HasColumnName("rule_code");
            entity.Property(e => e.Score).HasColumnName("score");
            entity.Property(e => e.SuspiciousThreshold)
                .HasDefaultValue(60, "df_fraud_rule_settings_suspicious_threshold")
                .HasColumnName("suspicious_threshold");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(CONVERT([datetime2](0),sysutcdatetime() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time'))", "df_fraud_rule_settings_updated_at")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany()
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_fraud_rule_settings_updated_by");
        });

        modelBuilder.Entity<KnowledgeBase>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_knowledge_base");

            entity.ToTable("knowledge_base", tb => tb.HasTrigger("trg_knowledge_set_updated_at"));

            entity.HasIndex(e => new { e.Category, e.IsActive }, "idx_knowledge_category_active");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Category)
                .HasMaxLength(40)
                .HasColumnName("category");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_knowledge_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true, "df_knowledge_is_active")
                .HasColumnName("is_active");
            entity.Property(e => e.Keywords)
                .HasMaxLength(500)
                .HasColumnName("keywords");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_knowledge_updated_at")
                .HasColumnName("updated_at");
            entity.Property(e => e.Version)
                .HasMaxLength(20)
                .HasDefaultValue("1.0", "df_knowledge_version")
                .HasColumnName("version");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_notifications");

            entity.ToTable("notifications");

            entity.HasIndex(e => e.CreatedAt, "idx_notifications_created_at");

            entity.HasIndex(e => e.TransactionId, "idx_notifications_transaction");

            entity.HasIndex(e => e.UserId, "idx_notifications_user_id");

            entity.HasIndex(e => new { e.UserId, e.IsRead }, "idx_notifications_user_read");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_notifications_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRead).HasColumnName("is_read");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.NotificationType)
                .HasMaxLength(40)
                .HasColumnName("notification_type");
            entity.Property(e => e.ReadAt).HasColumnName("read_at");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.TransactionId).HasColumnName("transaction_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Transaction).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.TransactionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_notifications_transaction");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_notifications_user");
        });

        modelBuilder.Entity<SecurityEventLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_security_event_logs");

            entity.ToTable("security_event_logs");

            entity.HasIndex(e => e.CreatedAt, "idx_security_event_logs_created");

            entity.HasIndex(e => e.CreatedAtUtc, "idx_security_event_logs_created_at_utc");

            entity.HasIndex(e => new { e.EventType, e.CreatedAt }, "idx_security_event_logs_event_created");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "idx_security_event_logs_user_created");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(CONVERT([datetime2](0),sysutcdatetime() AT TIME ZONE 'UTC' AT TIME ZONE 'Ulaanbaatar Standard Time'))", "df_security_event_logs_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedAtUtc)
                .HasDefaultValueSql("(sysutcdatetime())", "df_security_event_logs_created_at_utc")
                .HasColumnName("created_at_utc");
            entity.Property(e => e.EventType)
                .HasMaxLength(80)
                .HasColumnName("event_type");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .HasColumnName("ip_address");
            entity.Property(e => e.Message)
                .HasMaxLength(500)
                .HasColumnName("message");
            entity.Property(e => e.Success).HasColumnName("success");
            entity.Property(e => e.UserAgent)
                .HasMaxLength(255)
                .HasColumnName("user_agent");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.UsernameOrEmail)
                .HasMaxLength(100)
                .HasColumnName("username_or_email");

            entity.HasOne(d => d.User).WithMany(p => p.SecurityEventLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_security_event_logs_user");
        });

        modelBuilder.Entity<SuspiciousTransactionDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_suspicious_transaction_details");

            entity.ToTable("suspicious_transaction_details", tb => tb.HasTrigger("trg_suspicious_set_updated_at"));

            entity.HasIndex(e => e.CreatedAt, "idx_suspicious_created_at");

            entity.HasIndex(e => e.ReviewStatus, "idx_suspicious_review_status");

            entity.HasIndex(e => e.ReviewedBy, "idx_suspicious_reviewed_by");

            entity.HasIndex(e => e.RiskScore, "idx_suspicious_risk_score");

            entity.HasIndex(e => e.TransactionId, "uq_suspicious_transaction").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AiExplanation).HasColumnName("ai_explanation");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_suspicious_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.ReviewNote).HasColumnName("review_note");
            entity.Property(e => e.ReviewStatus)
                .HasMaxLength(30)
                .HasDefaultValue("PENDING", "df_suspicious_review_status")
                .HasColumnName("review_status");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.RiskScore)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("risk_score");
            entity.Property(e => e.SuspiciousReason).HasColumnName("suspicious_reason");
            entity.Property(e => e.TransactionId).HasColumnName("transaction_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_suspicious_updated_at")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.SuspiciousTransactionDetails)
                .HasForeignKey(d => d.ReviewedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_suspicious_reviewed_by");

            entity.HasOne(d => d.Transaction).WithOne(p => p.SuspiciousTransactionDetail)
                .HasForeignKey<SuspiciousTransactionDetail>(d => d.TransactionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_suspicious_transaction");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_transactions");

            entity.ToTable("transactions", tb => tb.HasTrigger("trg_transactions_prevent_core_update"));

            entity.HasIndex(e => e.CreatedAt, "idx_transactions_created_at");

            entity.HasIndex(e => e.ExchangeRateLogId, "idx_transactions_exchange_rate_log");

            entity.HasIndex(e => e.FromAccountId, "idx_transactions_from_account");

            entity.HasIndex(e => new { e.FromAccountId, e.CreatedAt }, "idx_transactions_from_created");

            entity.HasIndex(e => e.IsSuspicious, "idx_transactions_is_suspicious");

            entity.HasIndex(e => e.Status, "idx_transactions_status");

            entity.HasIndex(e => e.ToAccountId, "idx_transactions_to_account");

            entity.HasIndex(e => new { e.ToAccountId, e.CreatedAt }, "idx_transactions_to_created");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_transactions_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.CreditedAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("credited_amount");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.DetectionCheckedAt).HasColumnName("detection_checked_at");
            entity.Property(e => e.ExchangeRateLogId).HasColumnName("exchange_rate_log_id");
            entity.Property(e => e.ExchangeRateValue)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("exchange_rate_value");
            entity.Property(e => e.FailureReason)
                .HasMaxLength(255)
                .HasColumnName("failure_reason");
            entity.Property(e => e.FromAccountId).HasColumnName("from_account_id");
            entity.Property(e => e.IsSuspicious).HasColumnName("is_suspicious");
            entity.Property(e => e.RoundingDifference)
                .HasColumnType("decimal(18, 4)")
                .HasColumnName("rounding_difference");
            entity.Property(e => e.SourceCurrency)
                .HasMaxLength(3)
                .HasColumnName("source_currency");
            entity.Property(e => e.Status)
                .HasMaxLength(40)
                .HasDefaultValue("PENDING", "df_transactions_status")
                .HasColumnName("status");
            entity.Property(e => e.TargetCurrency)
                .HasMaxLength(3)
                .HasColumnName("target_currency");
            entity.Property(e => e.ToAccountId).HasColumnName("to_account_id");

            entity.HasOne(d => d.ExchangeRateLog).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.ExchangeRateLogId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_transactions_exchange_rate_log");

            entity.HasOne(d => d.FromAccount).WithMany(p => p.TransactionFromAccounts)
                .HasForeignKey(d => d.FromAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_transactions_from_account");

            entity.HasOne(d => d.ToAccount).WithMany(p => p.TransactionToAccounts)
                .HasForeignKey(d => d.ToAccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_transactions_to_account");
        });

        modelBuilder.Entity<TransactionDetectionLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_transaction_detection_logs");

            entity.ToTable("transaction_detection_logs");

            entity.HasIndex(e => e.CreatedAt, "idx_transaction_detection_created_at");

            entity.HasIndex(e => e.ServiceStatus, "idx_transaction_detection_status");

            entity.HasIndex(e => e.TransactionId, "idx_transaction_detection_transaction");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_transaction_detection_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.IsSuspicious).HasColumnName("is_suspicious");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.RiskScore)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("risk_score");
            entity.Property(e => e.ServiceStatus)
                .HasMaxLength(30)
                .HasColumnName("service_status");
            entity.Property(e => e.Source)
                .HasMaxLength(100)
                .HasDefaultValue("FASTAPI_RULES", "df_transaction_detection_source")
                .HasColumnName("source");
            entity.Property(e => e.TransactionId).HasColumnName("transaction_id");
            entity.Property(e => e.TriggeredRules).HasColumnName("triggered_rules");

            entity.HasOne(d => d.Transaction).WithMany(p => p.TransactionDetectionLogs)
                .HasForeignKey(d => d.TransactionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_transaction_detection_transaction");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_users");

            entity.ToTable("users", tb => tb.HasTrigger("trg_users_set_updated_at"));

            entity.HasIndex(e => e.IsActive, "idx_users_is_active");

            entity.HasIndex(e => e.LockedUntil, "idx_users_locked_until");

            entity.HasIndex(e => e.LockedUntilUtc, "idx_users_locked_until_utc");

            entity.HasIndex(e => e.PhoneNumber, "idx_users_phone_number");

            entity.HasIndex(e => e.Role, "idx_users_role");

            entity.HasIndex(e => e.Email, "uq_users_email").IsUnique();

            entity.HasIndex(e => e.NationalId, "uq_users_national_id").IsUnique();

            entity.HasIndex(e => e.Username, "uq_users_username").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_users_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.EmergencyPhoneNumber)
                .HasMaxLength(20)
                .HasColumnName("emergency_phone_number");
            entity.Property(e => e.FirstName)
                .HasMaxLength(50)
                .HasColumnName("first_name");
            entity.Property(e => e.FailedLoginCount)
                .HasDefaultValue(0, "df_users_failed_login_count")
                .HasColumnName("failed_login_count");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true, "df_users_is_active")
                .HasColumnName("is_active");
            entity.Property(e => e.LastFailedLoginAt).HasColumnName("last_failed_login_at");
            entity.Property(e => e.LastFailedLoginAtUtc).HasColumnName("last_failed_login_at_utc");
            entity.Property(e => e.LastFailedLoginServerTick).HasColumnName("last_failed_login_server_tick");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.LastLoginAtUtc).HasColumnName("last_login_at_utc");
            entity.Property(e => e.LastName)
                .HasMaxLength(50)
                .HasColumnName("last_name");
            entity.Property(e => e.LockedUntil).HasColumnName("locked_until");
            entity.Property(e => e.LockedUntilServerTick).HasColumnName("locked_until_server_tick");
            entity.Property(e => e.LockedUntilUtc).HasColumnName("locked_until_utc");
            entity.Property(e => e.NationalId)
                .HasMaxLength(20)
                .HasColumnName("national_id");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.PasswordChangedAt).HasColumnName("password_changed_at");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .HasColumnName("phone_number");
            entity.Property(e => e.Role)
                .HasMaxLength(10)
                .HasDefaultValue("USER", "df_users_role")
                .HasColumnName("role");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysutcdatetime())", "df_users_updated_at")
                .HasColumnName("updated_at");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");
        });

        modelBuilder.Entity<UserTransactionView>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("user_transaction_view");

            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreditedAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("credited_amount");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.ExchangeRateValue)
                .HasColumnType("decimal(18, 8)")
                .HasColumnName("exchange_rate_value");
            entity.Property(e => e.FromAccountId).HasColumnName("from_account_id");
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("id");
            entity.Property(e => e.RoundingDifference)
                .HasColumnType("decimal(18, 4)")
                .HasColumnName("rounding_difference");
            entity.Property(e => e.SourceCurrency)
                .HasMaxLength(3)
                .HasColumnName("source_currency");
            entity.Property(e => e.Status)
                .HasMaxLength(40)
                .HasColumnName("status");
            entity.Property(e => e.TargetCurrency)
                .HasMaxLength(3)
                .HasColumnName("target_currency");
            entity.Property(e => e.ToAccountId).HasColumnName("to_account_id");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
