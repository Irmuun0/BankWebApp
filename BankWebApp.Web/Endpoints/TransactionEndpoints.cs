using static BankWebApp.Web.Helpers.TransactionInput;
using BankWebApp.Web.Services.Interfaces;
using BankWebApp.Web.DTOs.Transactions;
using System.Security.Claims;
using static BankWebApp.Web.Endpoints.EndpointHelpers;

namespace BankWebApp.Web.Endpoints;

internal static class TransactionEndpoints
{
    // Endpoint нь form-ыг DTO болгож service рүү дамжуулна; бизнес дүрэм service-д байна.
    internal static void MapTransactionEndpoints(this WebApplication app)
    {
        app.MapPost("/transactions/create/submit", async (HttpContext context, ITransactionService transactionService, CancellationToken cancellationToken) =>
        {
            var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdValue, out var userId))
            {
                return Results.Redirect("/?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var fromAccountIdValue = form["FromAccountId"].ToString();
            var amountValue = form["Amount"].ToString();
            var transferType = form["TransferType"].ToString();
            var createTransactionPath = string.Equals(transferType, "own", StringComparison.OrdinalIgnoreCase)
                ? "/transactions/create?type=own"
                : "/transactions/create";
            var createTransactionSeparator = createTransactionPath.Contains('?', StringComparison.Ordinal) ? "&" : "?";

            if (!long.TryParse(fromAccountIdValue, out var fromAccountId) || !TryReadTransactionAmount(amountValue, out var amount))
            {
                return Results.Redirect($"{createTransactionPath}{createTransactionSeparator}error={Uri.EscapeDataString("Мөнгөн дүнд зөвхөн тоо, хамгийн ихдээ 2 бутархай орон оруулна уу.")}");
            }

            var toAccountNumber = form["ToAccountNumber"].ToString();
            if (!IsValidAccountNumber(toAccountNumber))
            {
                return Results.Redirect($"{createTransactionPath}{createTransactionSeparator}error={Uri.EscapeDataString("Хүлээн авах дансны дугаар 10 оронтой тоо байх ёстой.")}");
            }

            var dto = new CreateTransactionDto
            {
                FromAccountId = fromAccountId,
                ToAccountNumber = toAccountNumber,
                Amount = amount,
                Description = form["Description"].ToString()
            };

            var result = await transactionService.CreateTransactionAsync(userId, dto, cancellationToken);
            if (!result.Success)
            {
                return Results.Redirect($"{createTransactionPath}{createTransactionSeparator}error={Uri.EscapeDataString(result.ErrorMessage ?? "Гүйлгээ хийх үед алдаа гарлаа.")}");
            }

            return Results.Redirect($"{createTransactionPath}{createTransactionSeparator}success={Uri.EscapeDataString("Гүйлгээ амжилттай хийгдлээ.")}&transactionId={result.TransactionId}");
        }).RequireAuthorization();
    }
}
