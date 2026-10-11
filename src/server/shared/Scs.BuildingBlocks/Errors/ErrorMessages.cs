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
        [ErrorCodes.ClinicRequired] = ("يلزم تحديد العيادة", "A clinic must be specified"),
        [ErrorCodes.ClinicNotAvailable] = ("هذه الصفحة غير متاحة", "This page is not available"),
        [ErrorCodes.ClinicMismatch] = ("الطلب لا يخصّ هذه العيادة", "The request does not belong to this clinic"),
        [ErrorCodes.InvalidCredentials] = ("البريد الإلكتروني أو كلمة المرور غير صحيحة.", "The email or password is incorrect."),
        [ErrorCodes.CodeInvalid] = ("الرمز غير صحيح أو انتهت صلاحيته. يمكنك طلب رمز جديد.", "The code is incorrect or has expired. A new code can be requested."),
        [ErrorCodes.AccessExpired] = ("يلزم تجديد الجلسة", "The session needs renewing"),
        [ErrorCodes.SessionEnded] = ("انتهت الجلسة. الدخول من جديد للمتابعة.", "The session has ended. Sign in again to continue."),
        [ErrorCodes.AccountLocked] = ("أُقفل الحساب بعد محاولات دخول فاشلة", "The account was locked after failed sign-in attempts"),
        [ErrorCodes.AccountDisabled] = ("لا يمكن الدخول بهذا الحساب: هو معطَّل", "This account cannot sign in: it is disabled"),
        [ErrorCodes.ClinicSuspended] = ("اشتراك العيادة موقوف، ولا يمكن الدخول الآن", "The clinic is suspended; signing in is not possible now"),
        [ErrorCodes.NetworkBlocked] = ("الدخول من هذه الشبكة محظور الآن. راجِع فريق الدعم لمعرفة السبب ورفع الحظر.", "Signing in from this network is blocked now. Contact support to learn why and have it lifted."),
        [ErrorCodes.ResendTooEarly] = ("لم يحن وقت إعادة الإرسال بعد", "It is too early to send the code again"),
        [ErrorCodes.ResendLimitReached] = ("لا يمكن إرسال رمز آخر في هذا الدخول", "No more codes can be sent for this sign-in"),
        [ErrorCodes.SignInCodeNotSent] = ("لم نستطع إرسال رمز التحقق إلى بريدك. حاول مرة أخرى بعد قليل.", "The verification code could not be sent to the email. Try again shortly."),
        [ErrorCodes.SignInCodeNotResent] = ("لم نستطع إرسال رمز جديد. الرمز السابق يبقى صالحًا حتى تنتهي مدّته.", "A new code could not be sent. The earlier code stays valid until it expires."),
        [ErrorCodes.LinkInvalid] = ("انتهت صلاحية الرابط أو استُعمل", "The link has expired or was already used"),
        [ErrorCodes.PasswordChangeRequired] = ("يلزم تغيير كلمة المرور المؤقتة أولًا", "The temporary password must be changed first"),
        [ErrorCodes.PasswordChangeNotRequired] = ("كلمة المرور ليست مؤقتة", "The password is not a temporary one"),
        [ErrorCodes.PermissionRequired] = ("هذه الصفحة ليست في دورك", "This page is not in your role"),
        [ErrorCodes.RoleFixed] = ("هذا الدور ثابت ولا يُعدَّل ولا يُحذف", "This role is fixed; it is not edited or deleted"),
        [ErrorCodes.RoleInUse] = ("لا يُحذف دور يحمله مستخدمون", "A role that users hold is not deleted"),
        [ErrorCodes.LastOwner] = ("لا تبقى العيادة بلا مالك", "A clinic is not left without an owner"),
        [ErrorCodes.OwnRole] = ("لا يغيّر أحد دور حسابه", "Nobody changes the role of their own account"),
        [ErrorCodes.FileEmpty] = ("الملف فارغ", "The file is empty"),
        [ErrorCodes.FileTooLarge] = ("حجم الملف أكبر من الحد المسموح", "The file is larger than the limit"),
        [ErrorCodes.FileTypeNotAccepted] = ("نوع الملف غير مدعوم", "This type of file is not accepted"),
        [ErrorCodes.FileNotStored] = ("تعذّر حفظ الملف. تُرجى المحاولة مرة أخرى.", "The file could not be stored. Try again."),
        [ErrorCodes.FileLimitReached] = ("ملفات كثيرة في وقت قصير. تُرجى المحاولة بعد قليل.", "Too many files in a short time. Try again shortly."),
        [ErrorCodes.FileCountReached] = ("بلغ عدد الملفات الحد المسموح", "The limit on the number of files is reached"),
        [ErrorCodes.FileNotAttachable] = ("تعذّر إرفاق الملف", "The file could not be attached"),
        [ErrorCodes.FileBeingChecked] = ("الملف قيد الفحص", "The file is still being checked"),
        [ErrorCodes.FileRefused] = ("الملف مرفوض: لم يجتز الفحص الأمني", "The file was refused: it did not pass the security check"),
        [ErrorCodes.FileNotKept] = ("الملف لم يعد محفوظًا", "The file is no longer kept"),
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
