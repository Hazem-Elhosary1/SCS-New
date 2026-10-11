using System.Text.RegularExpressions;
using Scs.Audit.Infrastructure;
using Scs.BuildingBlocks.Audit;
using Scs.SharedKernel.Access;
using Scs.SharedKernel.Audit;

namespace Scs.UnitTests.Audit;

/// <summary>
/// Spec 007, FR-035a to FR-037: an entry reads in the language of whoever reads it, in the
/// words of specs/007-audit-trail/contracts/audit-trail.md §7.
/// </summary>
public partial class SentencesTests
{
    private const string Ar = "ar";
    private const string En = "en";

    private static readonly string[] Four =
        [Permissions.BookingsView, Permissions.BookingsManage, Permissions.PatientsView, Permissions.MoneyView];

    private static AuditAction Action(string code) => AuditActions.Find(code)!;

    private static string NameAr(string code) => PermissionCatalogue.Require(code).NameAr;

    private static string NameEn(string code) => PermissionCatalogue.Require(code).NameEn;

    private static string Say(string code, string language, string? target, params AuditChange[] changes) =>
        Sentences.Render(Action(code), new SentenceParts(target, changes), language);

    private static string Run(string language, int attempts, bool locked) =>
        Sentences.Render(
            Action(AuditActions.SignInFailedRun), new SentenceParts("sara@noor.example", [], attempts, locked, 15), language);

    [Theory]
    [InlineData("on", "off", "غُيِّر بريد «تذكرة دعم جديدة» من «يعمل» إلى «متوقف»", "Email for “New support ticket” was changed from “on” to “off”")]
    [InlineData("off", "on", "غُيِّر بريد «تذكرة دعم جديدة» من «متوقف» إلى «يعمل»", "Email for “New support ticket” was changed from “off” to “on”")]
    public void Switching_an_email_reads_as_what_it_was_and_what_it_became(string before, string after, string arabic, string english)
    {
        var change = new AuditChange("enabled", before, after);

        Assert.Equal(arabic, Say(AuditActions.SettingsNotificationEmailChanged, Ar, "تذكرة دعم جديدة", change));
        Assert.Equal(english, Say(AuditActions.SettingsNotificationEmailChanged, En, "New support ticket", change));
    }

    [GeneratedRegex(@"\p{IsArabic}")]
    private static partial Regex ArabicLetter();

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex LatinLetter();

    [GeneratedRegex("[0-9]")]
    private static partial Regex LatinDigit();

    [Theory]
    [InlineData(AuditActions.SignInFailedRun, "محاولة دخول فاشلة", "Failed sign-in")]
    [InlineData(AuditActions.SignInSucceeded, "تسجيل دخول", "Sign-in")]
    [InlineData(AuditActions.SessionSignedOut, "تسجيل خروج", "Sign-out")]
    [InlineData(AuditActions.SessionsEnded, "إنهاء جلسات", "Sessions ended")]
    [InlineData(AuditActions.PasswordResetCompleted, "إعادة تعيين كلمة المرور", "Password reset")]
    [InlineData(AuditActions.PasswordTemporaryReplaced, "استبدال كلمة المرور المؤقتة", "Temporary password replaced")]
    [InlineData(AuditActions.InvitationAccepted, "قبول دعوة", "Invitation accepted")]
    [InlineData(AuditActions.AccountAccessReissued, "إعادة إصدار وسيلة الدخول", "Access re-issued")]
    [InlineData(AuditActions.AccountRoleChanged, "تغيير دور مستخدم", "User’s role changed")]
    [InlineData(AuditActions.RoleCreated, "إنشاء دور", "Role created")]
    [InlineData(AuditActions.RoleUpdated, "تعديل دور", "Role edited")]
    [InlineData(AuditActions.RoleDeleted, "حذف دور", "Role deleted")]
    [InlineData(AuditActions.SettingsNotificationEmailChanged, "تغيير بريد تنبيه", "Notification email changed")]
    public void An_action_is_named_by_a_noun_in_each_language(string code, string arabic, string english)
    {
        Assert.Equal(arabic, Sentences.NameOf(Action(code), Ar));
        Assert.Equal(english, Sentences.NameOf(Action(code), En));
    }

    [Theory]
    [InlineData(AuditActions.SignInSucceeded, "سُجِّل الدخول إلى حساب sara@noor.example", "sara@noor.example’s account was signed in to")]
    [InlineData(AuditActions.SessionSignedOut, "سُجِّل الخروج من حساب sara@noor.example", "sara@noor.example’s account was signed out")]
    [InlineData(AuditActions.SessionsEnded, "أُنهيت جلسات sara@noor.example", "sara@noor.example’s sessions were ended")]
    [InlineData(AuditActions.PasswordResetCompleted, "أُعيد تعيين كلمة مرور sara@noor.example", "sara@noor.example’s password was reset")]
    [InlineData(
        AuditActions.PasswordTemporaryReplaced,
        "استُبدلت كلمة المرور المؤقتة لحساب sara@noor.example",
        "The temporary password of sara@noor.example’s account was replaced")]
    [InlineData(AuditActions.InvitationAccepted, "قُبلت دعوة sara@noor.example", "sara@noor.example’s invitation was accepted")]
    [InlineData(
        AuditActions.AccountAccessReissued,
        "أُعيد إصدار وسيلة الدخول لحساب sara@noor.example",
        "A way in was re-issued for sara@noor.example’s account")]
    public void An_entry_about_an_account_is_one_sentence_in_the_passive(string code, string arabic, string english)
    {
        Assert.Equal(arabic, Say(code, Ar, "sara@noor.example"));
        Assert.Equal(english, Say(code, En, "sara@noor.example"));
    }

    [Theory]
    [InlineData(1, "محاولة واحدة على حساب sara@noor.example", "1 attempt on sara@noor.example’s account")]
    [InlineData(2, "محاولتان على حساب sara@noor.example", "2 attempts on sara@noor.example’s account")]
    [InlineData(3, "٣ محاولات متتالية على حساب sara@noor.example", "3 attempts in a row on sara@noor.example’s account")]
    [InlineData(10, "١٠ محاولات متتالية على حساب sara@noor.example", "10 attempts in a row on sara@noor.example’s account")]
    [InlineData(11, "١١ محاولة متتالية على حساب sara@noor.example", "11 attempts in a row on sara@noor.example’s account")]
    public void A_run_of_failed_attempts_follows_its_number(int attempts, string arabic, string english)
    {
        Assert.Equal(arabic, Run(Ar, attempts, locked: false));
        Assert.Equal(english, Run(En, attempts, locked: false));
    }

    [Fact]
    public void A_run_that_ended_in_a_lock_says_so_with_its_minutes_and_no_other_does()
    {
        // The row the design draws (frames 5632:44831 and 6589:934565).
        Assert.Equal("٥ محاولات متتالية على حساب sara@noor.example، فقُفل ١٥ دقيقة", Run(Ar, 5, locked: true));
        Assert.Equal("5 attempts in a row on sara@noor.example’s account; locked for 15 minutes", Run(En, 5, locked: true));

        Assert.DoesNotContain("قُفل", Run(Ar, 5, locked: false), StringComparison.Ordinal);
        Assert.DoesNotContain("locked", Run(En, 5, locked: false), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, "أُنشئ الدور «الاستقبال» بـصلاحية واحدة", "Role “Front desk” was created with 1 permission")]
    [InlineData(2, "أُنشئ الدور «الاستقبال» بـصلاحيتين", "Role “Front desk” was created with 2 permissions")]
    [InlineData(3, "أُنشئ الدور «الاستقبال» بـ٣ صلاحيات", "Role “Front desk” was created with 3 permissions")]
    [InlineData(10, "أُنشئ الدور «الاستقبال» بـ١٠ صلاحيات", "Role “Front desk” was created with 10 permissions")]
    [InlineData(11, "أُنشئ الدور «الاستقبال» بـ١١ صلاحية", "Role “Front desk” was created with 11 permissions")]
    public void A_created_role_says_how_many_permissions_it_holds(int count, string arabic, string english)
    {
        var held = AuditChange.OfSet("permissions", PermissionCatalogue.All.Take(count).Select(p => p.Code), []);

        Assert.Equal(arabic, Say(AuditActions.RoleCreated, Ar, "الاستقبال", held));
        Assert.Equal(english, Say(AuditActions.RoleCreated, En, "Front desk", held));
    }

    [Fact]
    public void An_edited_role_says_what_was_added()
    {
        var change = AuditChange.OfSet("permissions", Four.Take(2), []);

        Assert.Equal(
            $"عُدِّل الدور «الاستقبال»: أُضيف {NameAr(Four[0])}، {NameAr(Four[1])}",
            Say(AuditActions.RoleUpdated, Ar, "الاستقبال", change));
        Assert.Equal(
            $"Role “Front desk” was edited: added {NameEn(Four[0])}, {NameEn(Four[1])}",
            Say(AuditActions.RoleUpdated, En, "Front desk", change));
    }

    [Fact]
    public void An_edited_role_says_what_was_removed()
    {
        var change = AuditChange.OfSet("permissions", [], Four.Take(1));

        Assert.Equal($"عُدِّل الدور «الاستقبال»: نُزع {NameAr(Four[0])}", Say(AuditActions.RoleUpdated, Ar, "الاستقبال", change));
        Assert.Equal($"Role “Front desk” was edited: removed {NameEn(Four[0])}", Say(AuditActions.RoleUpdated, En, "Front desk", change));
    }

    [Fact]
    public void An_edited_role_says_both_when_both_happened()
    {
        var change = AuditChange.OfSet("permissions", Four.Take(1), Four.Skip(1).Take(1));

        Assert.Equal(
            $"عُدِّل الدور «الاستقبال»: أُضيف {NameAr(Four[0])}، ونُزع {NameAr(Four[1])}",
            Say(AuditActions.RoleUpdated, Ar, "الاستقبال", change));
        Assert.Equal(
            $"Role “Front desk” was edited: added {NameEn(Four[0])}; removed {NameEn(Four[1])}",
            Say(AuditActions.RoleUpdated, En, "Front desk", change));
    }

    [Fact]
    public void An_edited_role_says_what_it_was_called_before()
    {
        var renamed = AuditChange.Of("name", "المواعيد", "الاستقبال");

        Assert.Equal("عُدِّل الدور «الاستقبال»: غُيِّر اسمه من «المواعيد»", Say(AuditActions.RoleUpdated, Ar, "الاستقبال", renamed));
        Assert.Equal(
            "Role “Front desk” was edited: renamed from “Bookings”",
            Say(AuditActions.RoleUpdated, En, "Front desk", AuditChange.Of("name", "Bookings", "Front desk")));
    }

    [Fact]
    public void An_edit_of_the_description_alone_is_only_an_edit()
    {
        var described = AuditChange.Of("description", "قديم", "جديد");

        Assert.Equal("عُدِّل الدور «الاستقبال»", Say(AuditActions.RoleUpdated, Ar, "الاستقبال", described));
        Assert.Equal("Role “Front desk” was edited", Say(AuditActions.RoleUpdated, En, "Front desk", described));
    }

    [Fact]
    public void A_list_longer_than_three_names_three_and_counts_the_rest()
    {
        var change = AuditChange.OfSet("permissions", Four, []);

        Assert.Equal(
            $"عُدِّل الدور «الاستقبال»: أُضيف {NameAr(Four[0])}، {NameAr(Four[1])}، {NameAr(Four[2])} و١ أخرى",
            Say(AuditActions.RoleUpdated, Ar, "الاستقبال", change));
        Assert.Equal(
            $"Role “Front desk” was edited: added {NameEn(Four[0])}, {NameEn(Four[1])}, {NameEn(Four[2])} and 1 more",
            Say(AuditActions.RoleUpdated, En, "Front desk", change));
    }

    [Fact]
    public void A_deleted_role_is_named()
    {
        Assert.Equal("حُذف الدور «الاستقبال»", Say(AuditActions.RoleDeleted, Ar, "الاستقبال"));
        Assert.Equal("Role “Front desk” was deleted", Say(AuditActions.RoleDeleted, En, "Front desk"));
    }

    [Fact]
    public void A_changed_role_of_a_person_names_the_role_before_and_after()
    {
        var change = new AuditChange("role", "طبيب", "الاستقبال", BeforeKey: "doctor");

        Assert.Equal(
            "غُيِّر دور sara@noor.example من «طبيب» إلى «الاستقبال»",
            Say(AuditActions.AccountRoleChanged, Ar, "sara@noor.example", change));
        Assert.Equal(
            "sara@noor.example’s role was changed from “Doctor” to “الاستقبال”",
            Say(AuditActions.AccountRoleChanged, En, "sara@noor.example", change));
    }

    [Fact]
    public void A_first_role_is_a_change_from_no_role()
    {
        var change = new AuditChange("role", null, "طبيب", AfterKey: "doctor");

        Assert.Equal(
            "غُيِّر دور sara@noor.example من «بلا دور» إلى «طبيب»",
            Say(AuditActions.AccountRoleChanged, Ar, "sara@noor.example", change));
        Assert.Equal(
            "sara@noor.example’s role was changed from “no role” to “Doctor”",
            Say(AuditActions.AccountRoleChanged, En, "sara@noor.example", change));
    }

    [Theory]
    [InlineData("owner", "مالك العيادة", "Clinic owner")]
    [InlineData("manager", "مدير العيادة", "Clinic manager")]
    [InlineData("doctor", "طبيب", "Doctor")]
    public void A_ready_role_under_its_own_name_is_named_in_the_readers_language(string key, string arabic, string english)
    {
        Assert.Equal(arabic, Sentences.RoleName(arabic, key, Ar));
        Assert.Equal(english, Sentences.RoleName(arabic, key, En));
    }

    [Fact]
    public void A_clinics_own_role_is_named_as_it_was_written()
    {
        Assert.Equal("فريق العيادة", Sentences.RoleName("فريق العيادة", null, En));
        Assert.Equal("Night shift", Sentences.RoleName("Night shift", null, Ar));
    }

    [Fact]
    public void An_account_that_is_gone_and_an_actor_nobody_knows_have_their_words()
    {
        Assert.Equal("حساب محذوف", Sentences.DeletedAccount(Ar));
        Assert.Equal("deleted account", Sentences.DeletedAccount(En));
        Assert.Equal("غير معروف", Sentences.UnknownActor(Ar));
        Assert.Equal("Unknown", Sentences.UnknownActor(En));
    }

    [Fact]
    public void An_english_sentence_has_no_arabic_in_it_and_an_arabic_one_no_latin_digits()
    {
        var changes = new[]
        {
            new AuditChange("role", "طبيب", "مدير العيادة", BeforeKey: "doctor", AfterKey: "manager"),
            AuditChange.Of("name", "Old", "New"),
            AuditChange.OfSet("permissions", PermissionCatalogue.All.Take(12).Select(p => p.Code), Four),
            new AuditChange("enabled", "on", "off"),
        };

        foreach (var action in AuditActions.All)
        {
            var english = Sentences.Render(action, new SentenceParts("Front desk", changes, 12, true, 15), En);
            var arabic = Sentences.Render(action, new SentenceParts("الاستقبال", changes, 12, true, 15), Ar);

            Assert.False(ArabicLetter().IsMatch(english), $"{action.Code}: {english}");
            Assert.False(LatinDigit().IsMatch(arabic), $"{action.Code}: {arabic}");
            Assert.DoesNotContain("{", english, StringComparison.Ordinal);
            Assert.DoesNotContain("{", arabic, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void An_arabic_sentence_about_an_arabic_name_has_no_latin_in_it()
    {
        foreach (var action in AuditActions.All)
        {
            var arabic = Sentences.Render(
                action,
                new SentenceParts(
                    "الاستقبال",
                    [
                        new AuditChange("role", "طبيب", "مدير العيادة", BeforeKey: "doctor", AfterKey: "manager"),
                        new AuditChange("enabled", "on", "off"),
                    ],
                    5,
                    true,
                    15),
                Ar);

            Assert.False(LatinLetter().IsMatch(arabic), $"{action.Code}: {arabic}");
        }
    }

    [Fact]
    public void A_number_is_written_as_the_language_writes_it()
    {
        Assert.Equal("١٥", Sentences.Number(15, english: false));
        Assert.Equal("15", Sentences.Number(15, english: true));
        Assert.Equal("٢٠٣", Sentences.Number(203, english: false));
    }
}
