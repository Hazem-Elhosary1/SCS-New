using Scs.Tenancy.Domain;

namespace Scs.UnitTests.Tenancy;

/// <summary>
/// Spec 015 (data-model.md §1): a clinic as an operator adds it and corrects it — the fields'
/// rules, the English name falling back to the Arabic, the tax number kept, removed or left as it
/// was stored, and the states of an add.
/// </summary>
public class ClinicProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private static Clinic Registered(string? nameEn = "Al Noor", string? tax = "123-456-789") =>
        Clinic.Register(Guid.NewGuid(), "عيادة النور", nameEn, "dental", "14 شارع التحرير", tax, "operator-1", Now).Clinic!;

    [Fact]
    public void An_added_clinic_is_being_set_up_in_arabic_with_its_profile()
    {
        var clinic = Registered(tax: "123 456 789");

        Assert.Equal(Clinic.SettingUp, clinic.State);
        Assert.Equal("ar", clinic.DefaultLanguage);
        Assert.Equal("123-456-789", clinic.TaxNumber);
        Assert.Equal("dental", clinic.Specialty);
        Assert.Equal("14 شارع التحرير", clinic.Address);
        Assert.Equal("operator-1", clinic.CreatedBy);
        Assert.Equal(clinic.Id, clinic.ClinicId);
    }

    [Fact]
    public void Without_an_english_name_the_arabic_one_is_kept_for_both()
    {
        Assert.Equal("عيادة النور", Registered(nameEn: "  ").NameEn);
        Assert.Equal("عيادة النور", Registered(nameEn: null).NameEn);
    }

    [Fact]
    public void Each_field_of_an_add_is_checked_and_reported_together()
    {
        var (clinic, errors) = Clinic.Register(Guid.NewGuid(), " ", new string('e', 201), "surgery", "", "12345", "operator-1", Now);

        Assert.Null(clinic);
        Assert.Equal(["REQUIRED"], errors["nameAr"]);
        Assert.Equal(["TOO_LONG"], errors["nameEn"]);
        Assert.Equal(["INVALID"], errors["specialty"]);
        Assert.Equal(["REQUIRED"], errors["address"]);
        Assert.Equal(["TAX_NUMBER"], errors["taxNumber"]);
        Assert.Equal(["REQUIRED"], Clinic.Register(Guid.NewGuid(), "ع", null, null, "ع", null, "o", Now).Errors["specialty"]);
    }

    [Fact]
    public void An_edit_returns_each_changed_field_before_and_after()
    {
        var clinic = Registered();
        var errors = new Dictionary<string, string[]>();

        var changes = clinic.Edit("عيادة النور", "Al Noor", "عنوان جديد", TaxNumberEdit.Keep, "dental", "en", errors);

        Assert.Empty(errors);
        Assert.Equal([("address", "14 شارع التحرير", "عنوان جديد"), ("defaultLanguage", "ar", "en")], changes);
        Assert.Empty(clinic.Edit("عيادة النور", "Al Noor", "عنوان جديد", TaxNumberEdit.Keep, "dental", "en", errors));
    }

    [Fact]
    public void The_tax_number_is_kept_when_not_sent_removed_when_sent_empty_and_changed_when_sent()
    {
        var clinic = Registered();
        var errors = new Dictionary<string, string[]>();

        Assert.Empty(clinic.Edit("عيادة النور", "Al Noor", "14 شارع التحرير", TaxNumberEdit.Keep, "dental", "ar", errors));
        Assert.Equal("123-456-789", clinic.TaxNumber);

        clinic.Edit("عيادة النور", "Al Noor", "14 شارع التحرير", new TaxNumberEdit(true, "987654321"), "dental", "ar", errors);
        Assert.Equal("987-654-321", clinic.TaxNumber);

        clinic.Edit("عيادة النور", "Al Noor", "14 شارع التحرير", new TaxNumberEdit(true, ""), "dental", "ar", errors);
        Assert.Null(clinic.TaxNumber);
        Assert.Empty(errors);
    }

    [Fact]
    public void A_wrong_edit_changes_nothing()
    {
        var clinic = Registered();
        var errors = new Dictionary<string, string[]>();

        var changes = clinic.Edit("", "Al Noor", "عنوان جديد", new TaxNumberEdit(true, "12"), "x", "fr", errors);

        Assert.Empty(changes);
        Assert.Equal(["REQUIRED"], errors["nameAr"]);
        Assert.Equal(["TAX_NUMBER"], errors["taxNumber"]);
        Assert.Equal(["INVALID"], errors["specialty"]);
        Assert.Equal(["INVALID"], errors["defaultLanguage"]);
        Assert.Equal("14 شارع التحرير", clinic.Address);
    }

    [Fact]
    public void Editing_may_leave_the_specialty_unrecorded()
    {
        var clinic = Registered();
        var changes = clinic.Edit("عيادة النور", "Al Noor", "14 شارع التحرير", TaxNumberEdit.Keep, null, "ar", []);

        Assert.Equal([("specialty", "dental", (string?)null)], changes);
        Assert.Null(clinic.Specialty);
    }

    [Fact]
    public void Only_a_clinic_being_set_up_is_activated_or_discarded()
    {
        var clinic = Registered();
        clinic.Activate();
        Assert.Equal(Clinic.Active, clinic.State);
        Assert.Throws<InvalidOperationException>(clinic.Activate);
        Assert.Throws<InvalidOperationException>(clinic.Discard);

        var other = Registered();
        other.Discard();
        Assert.Equal(Clinic.Discarded, other.State);
    }

    [Fact]
    public void The_account_manager_is_assigned_once_and_cleared()
    {
        var clinic = Registered();
        var manager = Guid.NewGuid();

        Assert.True(clinic.AssignAccountManager(manager));
        Assert.False(clinic.AssignAccountManager(manager));
        Assert.True(clinic.AssignAccountManager(null));
        Assert.Null(clinic.AccountManagerId);
    }
}
