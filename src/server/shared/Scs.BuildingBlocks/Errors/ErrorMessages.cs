using Microsoft.AspNetCore.Http;
using Scs.SharedKernel;

namespace Scs.BuildingBlocks.Errors;

/// <summary>
/// The display text of each error code in Arabic and English. Arabic is the default
/// (contracts/conventions.md §2). Wording follows SH-098: formal, no gendered address.
/// </summary>
public static class ErrorMessages
{
    public const string Arabic = "ar";
    public const string English = "en";

    private static readonly Dictionary<string, (string Ar, string En)> Titles = new(StringComparer.Ordinal)
    {
        [ErrorCodes.ValidationFailed] = ("البيانات المدخلة غير صحيحة", "The submitted data is not valid"),
        [ErrorCodes.MalformedRequest] = ("تعذّرت قراءة الطلب", "The request could not be read"),
        [ErrorCodes.Unauthenticated] = ("يلزم تسجيل الدخول", "Sign-in is required"),
        [ErrorCodes.Forbidden] = ("لا تتوفر صلاحية لهذا الإجراء", "Permission for this action is missing"),
        [ErrorCodes.NotFound] = ("العنصر المطلوب غير موجود", "The requested item was not found"),
        [ErrorCodes.PayloadTooLarge] = ("حجم الطلب أكبر من المسموح", "The request is larger than allowed"),
        [ErrorCodes.RateLimited] = ("طلبات كثيرة، تُرجى المحاولة بعد قليل", "Too many requests; try again shortly"),
        [ErrorCodes.InternalError] = ("حدث خطأ غير متوقع", "An unexpected error occurred"),
        [ErrorCodes.ServiceUnavailable] = ("الخدمة غير متاحة مؤقتًا", "The service is temporarily unavailable"),
    };

    public static string Title(string code, string language)
    {
        var (ar, en) = Titles.TryGetValue(code, out var found) ? found : Titles[ErrorCodes.InternalError];
        return language == English ? en : ar;
    }

    /// <summary><c>en</c> only when the caller asks for English first; anything else is Arabic.</summary>
    public static string ResolveLanguage(HttpRequest request)
    {
        var first = request.GetTypedHeaders().AcceptLanguage
            .OrderByDescending(l => l.Quality ?? 1)
            .Select(l => l.Value.Value)
            .FirstOrDefault();

        return first is not null && first.StartsWith(English, StringComparison.OrdinalIgnoreCase)
            ? English
            : Arabic;
    }
}
