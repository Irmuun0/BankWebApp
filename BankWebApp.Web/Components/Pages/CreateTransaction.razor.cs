using static BankWebApp.Web.Helpers.Money;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace BankWebApp.Web.Components.Pages;

// CreateTransaction.razor нь дэлгэц; энэ partial class нь төлөв, event, өгөгдөл ачаалах логик.
public partial class CreateTransaction
{
    private readonly string[] descriptionOptions = ["Хэрэглэгчээс", "Худалдан авалт", "Хэрэглээний төлбөр", "Хоол, хүнс"];

    private List<BankWebApp.Web.DTOs.Accounts.AccountDto> activeAccounts = [];
    private BankWebApp.Web.DTOs.Transactions.ReceiverAccountPreviewDto? receiverPreview;
    private BankWebApp.Web.DTOs.Transactions.TransactionReceiptDto? receipt;
    private bool isLoading = true;
    private bool showReceipt;
    private long? currentUserId;
    private string? loadErrorMessage;
    private string? receiverPreviewMessage;
    private string toAccountNumber = string.Empty;
    private string amountWhole = string.Empty;
    private string amountCents = "00";
    private bool hasInvalidAmountWhole;
    private bool hasInvalidAmountCents;
    private bool hasInvalidReceiverAccountNumber;
    private long? selectedFromAccountId;
    private string? selectedToOwnAccountNumber;
    private string? selectedFromAccountCurrency;
    private string transactionDescription = string.Empty;

    [SupplyParameterFromQuery]
    public string? Error { get; set; }

    [SupplyParameterFromQuery]
    public string? Success { get; set; }

    [SupplyParameterFromQuery]
    public long? TransactionId { get; set; }

    [SupplyParameterFromQuery(Name = "type")]
    private string? TransferType { get; set; }

    private bool IsOwnTransfer => string.Equals(TransferType, "own", StringComparison.OrdinalIgnoreCase);
    private string ComposedAmount => $"{NormalizeWholeAmount(amountWhole)}.{NormalizeCentAmount(amountCents)}";
    private decimal EnteredAmount => decimal.TryParse(ComposedAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
        ? amount
        : 0m;
    private decimal ConvertedReceiveAmount => exchangeRateQuote is null
        ? EnteredAmount
        : TruncateMoney(EnteredAmount * exchangeRateQuote.Rate);
    private bool IsConvertedReceiveAmountTooSmall => exchangeRateQuote is not null && EnteredAmount > 0 && ConvertedReceiveAmount < 1m;
    private bool IsTransferLimitExceeded =>
        SelectedFromAccount is not null &&
        string.Equals(SelectedFromAccount.Currency, "MNT", StringComparison.OrdinalIgnoreCase) &&
        EnteredAmount > SelectedFromAccount.DailyTransactionLimitMnt;
    private bool ShowDescriptionWarning => string.IsNullOrWhiteSpace(transactionDescription);
    private bool HasInvalidAmountInput => hasInvalidAmountWhole || hasInvalidAmountCents;
    private bool HasInvalidReceiverAccountNumber =>
        !IsOwnTransfer && (hasInvalidReceiverAccountNumber || toAccountNumber.Length != 10);
    private string SelectedFromAccountCurrency => string.IsNullOrWhiteSpace(selectedFromAccountCurrency) ? "Валют" : selectedFromAccountCurrency;
    private string SelectedFromAccountValue => selectedFromAccountId?.ToString() ?? string.Empty;
    private BankWebApp.Web.DTOs.Accounts.AccountDto? SelectedFromAccount => selectedFromAccountId is null
        ? null
        : activeAccounts.FirstOrDefault(account => account.Id == selectedFromAccountId.Value);
    private BankWebApp.Web.DTOs.ExchangeRates.ExchangeRateQuoteDto? exchangeRateQuote;
    private string? exchangeRateMessage;
    private IEnumerable<BankWebApp.Web.DTOs.Accounts.AccountDto> ReceiverOwnAccounts => activeAccounts
        .Where(account => selectedFromAccountId is null || account.Id != selectedFromAccountId.Value);
    private BankWebApp.Web.DTOs.Accounts.AccountDto? SelectedOwnReceiverAccount => string.IsNullOrWhiteSpace(selectedToOwnAccountNumber)
        ? null
        : activeAccounts.FirstOrDefault(account => account.AccountNumber == selectedToOwnAccountNumber);

    protected override async Task OnInitializedAsync()
    {
        currentUserId = await ResolveCurrentUserIdAsync();
        if (currentUserId is null)
        {
            loadErrorMessage = "Та нэвтрээгүй байна.";
            isLoading = false;
            return;
        }

        descriptionOptions[0] = await ResolveSenderDescriptionOptionAsync();

        var accounts = await AccountService.GetMyAccountsAsync(currentUserId.Value);
        activeAccounts = accounts
            .Where(account => account.IsActive)
            .OrderBy(account => account.AccountNumber)
            .ToList();
        ApplyDefaultAccounts();
        await RefreshExchangeRateQuoteAsync();

        if (TransactionId is not null)
        {
            receipt = await TransactionService.GetMyTransactionReceiptAsync(currentUserId.Value, TransactionId.Value);
            showReceipt = receipt is not null;
        }

        isLoading = false;
    }

    private async Task OnReceiverAccountInput(ChangeEventArgs args)
    {
        var rawValue = args.Value?.ToString() ?? string.Empty;
        var digits = KeepDigits(rawValue);
        hasInvalidReceiverAccountNumber = rawValue != digits || digits.Length > 10;
        toAccountNumber = digits.Length > 10 ? digits[..10] : digits;
        receiverPreview = null;
        receiverPreviewMessage = null;

        if (hasInvalidReceiverAccountNumber)
        {
            await RefreshExchangeRateQuoteAsync();
            return;
        }

        var accountNumber = toAccountNumber.Trim();
        if (accountNumber.Length != 10 || currentUserId is null)
        {
            await RefreshExchangeRateQuoteAsync();
            return;
        }

        receiverPreview = await TransactionService.GetReceiverAccountPreviewAsync(currentUserId.Value, accountNumber);
        receiverPreviewMessage = receiverPreview is null ? "Идэвхтэй данс олдсонгүй." : null;
        await RefreshExchangeRateQuoteAsync();
    }

    private async Task OnFromAccountChanged(ChangeEventArgs args)
    {
        var value = args.Value?.ToString();
        if (!long.TryParse(value, out var accountId))
        {
            selectedFromAccountId = null;
            selectedFromAccountCurrency = null;
            selectedToOwnAccountNumber = ReceiverOwnAccounts.FirstOrDefault()?.AccountNumber;
            await RefreshExchangeRateQuoteAsync();
            return;
        }

        selectedFromAccountId = accountId;
        selectedFromAccountCurrency = SelectedFromAccount?.Currency;

        if (IsOwnTransfer && SelectedOwnReceiverAccount?.Id == selectedFromAccountId)
        {
            selectedToOwnAccountNumber = ReceiverOwnAccounts.FirstOrDefault()?.AccountNumber;
        }

        await RefreshExchangeRateQuoteAsync();
    }

    private async Task OnOwnReceiverChanged(ChangeEventArgs args)
    {
        selectedToOwnAccountNumber = args.Value?.ToString();
        if (SelectedOwnReceiverAccount?.Id == selectedFromAccountId)
        {
            selectedToOwnAccountNumber = ReceiverOwnAccounts.FirstOrDefault()?.AccountNumber;
        }

        await RefreshExchangeRateQuoteAsync();
    }

    private void OnAmountWholeInput(ChangeEventArgs args)
    {
        var rawValue = args.Value?.ToString() ?? string.Empty;
        var digits = KeepDigits(rawValue);
        hasInvalidAmountWhole = rawValue != digits || digits.Length > 16;
        amountWhole = digits.Length > 16 ? digits[..16] : digits;
    }

    private void OnAmountCentsInput(ChangeEventArgs args)
    {
        var rawValue = args.Value?.ToString() ?? string.Empty;
        amountCents = KeepDigits(rawValue);
        hasInvalidAmountCents = rawValue != amountCents || amountCents.Length > 2;
        if (amountCents.Length > 2)
        {
            amountCents = amountCents[..2];
        }
    }

    private void AddDescriptionOption(string option)
    {
        transactionDescription = string.IsNullOrWhiteSpace(transactionDescription)
            ? option
            : $"{transactionDescription.Trim()} {option}";
    }

    private void DismissReceipt()
    {
        showReceipt = false;
    }

    private void CloseReceipt()
    {
        Navigation.NavigateTo("/dashboard");
    }

    private static string KeepDigits(string value)
    {
        return new string(value.Where(character => character is >= '0' and <= '9').ToArray());
    }

    private static string NormalizeWholeAmount(string value)
    {
        var digits = KeepDigits(value);
        return string.IsNullOrWhiteSpace(digits) ? "0" : digits;
    }

    private static string NormalizeCentAmount(string value)
    {
        var digits = KeepDigits(value);
        return string.IsNullOrWhiteSpace(digits) ? "00" : digits.PadRight(2, '0')[..2];
    }

    private void ApplyDefaultAccounts()
    {
        var defaultSender = activeAccounts.FirstOrDefault(account => account.Currency == "MNT")
            ?? activeAccounts.FirstOrDefault();

        selectedFromAccountId = defaultSender?.Id;
        selectedFromAccountCurrency = defaultSender?.Currency;
        selectedToOwnAccountNumber = ReceiverOwnAccounts.FirstOrDefault()?.AccountNumber;
    }

    // Энэ нь зөвхөн дэлгэцийн урьдчилсан тооцоо; илгээх үед backend ханш, лимитийг дахин шалгана.
    private async Task RefreshExchangeRateQuoteAsync()
    {
        exchangeRateQuote = null;
        exchangeRateMessage = null;

        var fromCurrency = SelectedFromAccount?.Currency;
        var toCurrency = GetSelectedReceiverCurrency();
        if (string.IsNullOrWhiteSpace(fromCurrency) ||
            string.IsNullOrWhiteSpace(toCurrency) ||
            string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        exchangeRateQuote = await ExchangeRateService.GetExchangeRateQuoteAsync(fromCurrency, toCurrency);
        if (exchangeRateQuote is null)
        {
            exchangeRateMessage = $"{fromCurrency}-{toCurrency} валютын ханш олдсонгүй.";
        }
    }

    private string? GetSelectedReceiverCurrency()
    {
        return IsOwnTransfer
            ? SelectedOwnReceiverAccount?.Currency
            : receiverPreview?.Currency;
    }

    private static string FormatAccountOption(BankWebApp.Web.DTOs.Accounts.AccountDto account)
    {
        return $"{account.AccountNumber} - Харилцах/{account.Currency}";
    }

    private async Task<long?> ResolveCurrentUserIdAsync()
    {
        if (CurrentUser.UserId is not null)
        {
            return CurrentUser.UserId;
        }

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var value = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(value, out var userId) ? userId : null;
    }

    private async Task<string> ResolveSenderDescriptionOptionAsync()
    {
        var fullName = CurrentUser.FullName;
        if (string.IsNullOrWhiteSpace(fullName))
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            fullName = authState.User.FindFirst("FullName")?.Value;
        }

        var firstName = ExtractFirstName(fullName);
        return string.IsNullOrWhiteSpace(firstName) ? "Хэрэглэгчээс" : $"{firstName}-с";
    }

    private static string? ExtractFirstName(string? fullName)
    {
        var normalizedName = fullName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        const string patronymicSeparator = "-ийн ";
        var separatorIndex = normalizedName.LastIndexOf(patronymicSeparator, StringComparison.Ordinal);
        if (separatorIndex >= 0)
        {
            return normalizedName[(separatorIndex + patronymicSeparator.Length)..].Trim();
        }

        return normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }
}
