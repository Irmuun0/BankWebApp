using BankWebApp.Web.DTOs.Admin;
using Microsoft.AspNetCore.Components;

namespace BankWebApp.Web.Components.Pages;

// AdminRegistrationRequests.razor нь дэлгэц; энэ partial class нь төлөв, event, өгөгдөл ачаалах логик.
public partial class AdminRegistrationRequests
{
    [SupplyParameterFromQuery] public string? Search { get; set; }
    [SupplyParameterFromQuery] public string? Status { get; set; }
    [SupplyParameterFromQuery] public DateOnly? CreatedFrom { get; set; }
    [SupplyParameterFromQuery] public DateOnly? CreatedTo { get; set; }
    [SupplyParameterFromQuery] public int Page { get; set; } = 1;
    [SupplyParameterFromQuery] public int PageSize { get; set; } = 20;
    [SupplyParameterFromQuery(Name = "requestId")] public long? SelectedRequestId { get; set; }
    [SupplyParameterFromQuery] public string? Success { get; set; }
    [SupplyParameterFromQuery] public string? Error { get; set; }
    [SupplyParameterFromQuery] public string? CredentialKey { get; set; }

    private bool isLoading = true;
    private AdminPagedResultDto<AdminRegistrationRequestDto> requests = new();
    private AdminRegistrationRequestDto? selectedRequest;
    private OneTimeRegistrationCredentialDto? oneTimeCredential;
    private PersistingComponentStateSubscription? persistingSubscription;

    private int TotalPages => Math.Max(1, (int)Math.Ceiling(requests.TotalItems / (double)Math.Max(requests.PageSize, 1)));
    private string CurrentUrl => Navigation.ToBaseRelativePath(Navigation.Uri).StartsWith("admin/registration-requests", StringComparison.OrdinalIgnoreCase)
        ? "/" + Navigation.ToBaseRelativePath(Navigation.Uri)
        : "/admin/registration-requests";

    protected override void OnInitialized()
    {
        persistingSubscription = ApplicationState.RegisterOnPersisting(PersistOneTimeCredential);
    }

    protected override async Task OnParametersSetAsync()
    {
        isLoading = true;
        ConsumeOneTimeCredential();
        var page = Page <= 0 ? 1 : Page;
        var pageSize = PageSize is 50 or 100 ? PageSize : 20;
        Page = page;
        PageSize = pageSize;
        requests = await RegistrationService.GetRequestsAsync(
            new AdminRegistrationRequestFilterDto
            {
                Search = Search,
                Status = Status,
                CreatedFrom = CreatedFrom,
                CreatedTo = CreatedTo
            },
            page,
            pageSize);

        selectedRequest = SelectedRequestId is long id
            ? await RegistrationService.GetRequestAsync(id)
            : null;

        isLoading = false;
    }

    private void ConsumeOneTimeCredential()
    {
        if (oneTimeCredential is not null)
        {
            return;
        }

        if (ApplicationState.TryTakeFromJson<OneTimeRegistrationCredentialDto>(
                nameof(OneTimeRegistrationCredentialDto),
                out var persistedCredential))
        {
            oneTimeCredential = persistedCredential;
            return;
        }

        if (
            string.IsNullOrWhiteSpace(CredentialKey) ||
            !CredentialKey.StartsWith("registration-credential:", StringComparison.Ordinal))
        {
            return;
        }

        var session = HttpContextAccessor.HttpContext?.Session;
        var serialized = session?.GetString(CredentialKey);
        session?.Remove(CredentialKey);
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return;
        }

        try
        {
            oneTimeCredential = System.Text.Json.JsonSerializer.Deserialize<OneTimeRegistrationCredentialDto>(serialized);
        }
        catch (System.Text.Json.JsonException)
        {
            oneTimeCredential = null;
        }
    }

    private Task PersistOneTimeCredential()
    {
        if (oneTimeCredential is not null)
        {
            ApplicationState.PersistAsJson(nameof(OneTimeRegistrationCredentialDto), oneTimeCredential);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        persistingSubscription?.Dispose();
    }

    private string BuildPageUrl(int? page = null, long? SelectedRequestId = null)
    {
        var query = new Dictionary<string, object?>
        {
            ["search"] = Search,
            ["status"] = Status,
            ["createdFrom"] = FormatDateInput(CreatedFrom),
            ["createdTo"] = FormatDateInput(CreatedTo),
            ["page"] = page ?? Page,
            ["pageSize"] = PageSize,
            ["requestId"] = SelectedRequestId,
            ["credentialKey"] = (string?)null
        };

        return Navigation.GetUriWithQueryParameters("/admin/registration-requests", query);
    }

    private static string? FormatDateInput(DateOnly? value) => value?.ToString("yyyy-MM-dd");

    private static string FormatStatus(string value) => value switch
    {
        "PENDING" => "Хүлээгдэж буй",
        "APPROVED" => "Зөвшөөрсөн",
        "REJECTED" => "Татгалзсан",
        "NEEDS_INFO" => "Нэмэлт мэдээлэл",
        _ => value
    };

    private static string FormatContact(string value) => value == "PHONE" ? "Утас" : "Email";
}
