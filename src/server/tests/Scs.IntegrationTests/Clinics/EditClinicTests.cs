using System.Text.Json;
using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using static Scs.IntegrationTests.Fixtures.ClinicFixtures;

namespace Scs.IntegrationTests.Clinics;

/// <summary>
/// Spec 015, User Story 4 (FR-020 – FR-022, FR-021a): «تعديل العيادة» corrects the clinic and its
/// owner — each part recorded with its fields before and after, nothing recorded for nothing, a
/// stale form changing nothing — and a changed owner email ends the old address's way in.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class EditClinicTests(SqlServerFixture sql)
{
    private static async Task<Dictionary<string, object?>> BodyAsync(HttpClient editor, Guid id)
    {
        var read = await editor.JsonAsync($"{ClinicsPath}/{id}");
        string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        var owner = read.GetProperty("owner");
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["version"] = Text(read, "version"),
            ["nameAr"] = Text(read, "nameAr"),
            ["nameEn"] = Text(read, "nameEn"),
            ["ownerName"] = Text(owner, "name"),
            ["email"] = Text(owner, "email"),
            ["mobile"] = Text(owner, "mobile"),
            ["address"] = Text(read, "address"),
            ["specialty"] = Text(read, "specialty"),
            ["defaultLanguage"] = Text(read, "defaultLanguage"),
        };
    }

    private static async Task<(OpsWorld World, HttpClient Editor, Guid Id, string Email)> AddedAsync(SqlServerFixture sql)
    {
        var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        var editor = await world.Factory.SignInWithAsync(Editor);
        var email = NewEmail();
        var (id, _) = await editor.AddClinicAsync(NewClinic(plan, email: email));
        return (world, editor, id, email);
    }

    [Fact]
    public async Task Each_part_is_recorded_with_its_fields_and_the_note_and_nothing_changed_records_nothing()
    {
        var (world, editor, id, _) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            Assert.Equal(204, (await editor.OpsPutAsync($"{ClinicsPath}/{id}", await BodyAsync(editor, id)).RefusalAsync()).Status);
            Assert.Equal(0, await sql.CountAsync("audit.PlatformEntry", $"TargetId = '{id}' AND ActionCode IN ('clinic.updated', 'clinic.owner-updated')"));

            var body = (await BodyAsync(editor, id))
                .With("address", "عنوان جديد").With("defaultLanguage", "en").With("specialty", "")
                .With("ownerName", "رانيا فؤاد").With("mobile", "+966 50 123 4567").With("note", "بناءً على طلب العيادة");
            Assert.Equal(204, (await editor.OpsPutAsync($"{ClinicsPath}/{id}", body).RefusalAsync()).Status);

            var detail = await editor.JsonAsync($"{ClinicsPath}/{id}");
            Assert.Equal("عنوان جديد", detail.GetProperty("address").GetString());
            Assert.Equal("en", detail.GetProperty("defaultLanguage").GetString());
            Assert.Equal(JsonValueKind.Null, detail.GetProperty("specialty").ValueKind);
            Assert.Equal("+966501234567", detail.GetProperty("owner").GetProperty("mobile").GetString());
            Assert.Equal("123-456-789", detail.GetProperty("taxNumber").GetString());

            var clinicPart = await sql.ValueAsync<string>($"SELECT Changes FROM audit.PlatformEntry WHERE TargetId = '{id}' AND ActionCode = 'clinic.updated'");
            Assert.Contains("\"address\"", clinicPart, StringComparison.Ordinal);
            Assert.Contains("عنوان جديد", clinicPart, StringComparison.Ordinal);
            Assert.DoesNotContain("taxNumber", clinicPart, StringComparison.Ordinal);
            Assert.Equal(1, await sql.CountAsync("audit.PlatformEntry", $"TargetId = '{id}' AND ActionCode = 'clinic.owner-updated' AND Facts LIKE N'%بناءً على طلب العيادة%'"));
            Assert.Equal(1, await sql.CountAsync("audit.Entry", $"ClinicId = '{id}' AND ActionCode = 'clinic.updated' AND ActorKind = 'operator'"));
        }
    }

    [Fact]
    public async Task The_tax_number_is_kept_when_left_out_removed_when_cleared_and_checked_when_changed()
    {
        var (world, editor, id, _) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            var tax = $"SELECT TaxNumber FROM tenancy.Clinic WHERE Id = '{id}'";

            await editor.OpsPutAsync($"{ClinicsPath}/{id}", (await BodyAsync(editor, id)).With("address", "تغيير"));
            Assert.Equal("123-456-789", await sql.ValueAsync<string>(tax));

            Assert.Equal((400, "VALIDATION_FAILED"), await editor.OpsPutAsync($"{ClinicsPath}/{id}", (await BodyAsync(editor, id)).With("taxNumber", "12")).RefusalAsync());

            await editor.OpsPutAsync($"{ClinicsPath}/{id}", (await BodyAsync(editor, id)).With("taxNumber", "987 654 321"));
            Assert.Equal("987-654-321", await sql.ValueAsync<string>(tax));

            await editor.OpsPutAsync($"{ClinicsPath}/{id}", (await BodyAsync(editor, id)).With("taxNumber", ""));
            Assert.Null(await sql.ValueAsync<string>(tax));
        }
    }

    [Fact]
    public async Task A_stale_form_and_a_wrong_field_change_nothing_the_owner_included()
    {
        var (world, editor, id, email) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            var stale = await BodyAsync(editor, id);
            await editor.OpsPutAsync($"{ClinicsPath}/{id}", (await BodyAsync(editor, id)).With("address", "أحدث"));

            Assert.Equal((409, "CLINIC_CHANGED_ELSEWHERE"), await editor.OpsPutAsync($"{ClinicsPath}/{id}", stale.With("ownerName", "اسم من نموذج قديم")).RefusalAsync());
            Assert.Equal("هيثم محمود", await sql.ValueAsync<string>($"SELECT FullName FROM [identity].StaffAccount WHERE Email = N'{email}'"));

            foreach (var (field, value, code) in new[] { ("mobile", "abc", "MOBILE"), ("nameAr", "", "REQUIRED"), ("email", "x", "INVALID_FORMAT") })
            {
                var (status, _, text) = await editor.OpsPutAsync($"{ClinicsPath}/{id}", (await BodyAsync(editor, id)).With(field, value)).AnswerAsync();
                Assert.Equal(400, status);
                Assert.Contains($"\"{code}\"", text, StringComparison.Ordinal);
            }

            var taken = NewEmail();
            await world.Factory.CreateAccountAsync(await sql.Main.SeedClinicAsync(), email: taken);
            var (takenStatus, _, takenText) = await editor.OpsPutAsync($"{ClinicsPath}/{id}", (await BodyAsync(editor, id)).With("email", taken)).AnswerAsync();
            Assert.Equal(400, takenStatus);
            Assert.Contains("\"email\":[\"TAKEN\"]", takenText, StringComparison.Ordinal);
            Assert.Equal(1, await sql.CountAsync("[identity].StaffAccount", $"Email = N'{email}'"));
        }
    }

    [Fact]
    public async Task A_refused_clinic_field_leaves_the_owner_unchanged_and_both_parts_errors_come_back()
    {
        // FR-021: a save the server refuses writes nothing, the owner's part included (T073).
        var (world, editor, id, email) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            var body = (await BodyAsync(editor, id)).With("ownerName", "اسم لا يُحفظ").With("defaultLanguage", "fr");
            var (status, _, text) = await editor.OpsPutAsync($"{ClinicsPath}/{id}", body).AnswerAsync();

            Assert.Equal(400, status);
            Assert.Contains("\"defaultLanguage\"", text, StringComparison.Ordinal);
            Assert.Equal("هيثم محمود", await sql.ValueAsync<string>($"SELECT FullName FROM [identity].StaffAccount WHERE Email = N'{email}'"));
            Assert.Equal(0, await sql.CountAsync("audit.PlatformEntry", $"TargetId = '{id}' AND ActionCode = 'clinic.owner-updated'"));

            var both = (await BodyAsync(editor, id)).With("mobile", "abc").With("nameAr", "");
            var (_, _, bothText) = await editor.OpsPutAsync($"{ClinicsPath}/{id}", both).AnswerAsync();
            Assert.Contains("\"mobile\"", bothText, StringComparison.Ordinal);
            Assert.Contains("\"nameAr\"", bothText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task An_owner_only_change_moves_the_version_so_an_older_form_is_refused()
    {
        // FR-021: the form's version covers the owner's part too (T075).
        var (world, editor, id, _) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            var older = await BodyAsync(editor, id);
            Assert.Equal(204, (await editor.OpsPutAsync($"{ClinicsPath}/{id}", (await BodyAsync(editor, id)).With("mobile", "+20 111 222 3333")).RefusalAsync()).Status);
            Assert.NotEqual(older["version"], (await BodyAsync(editor, id))["version"]);

            Assert.Equal((409, "CLINIC_CHANGED_ELSEWHERE"), await editor.OpsPutAsync($"{ClinicsPath}/{id}", older).RefusalAsync());
            Assert.Equal("+201112223333", await sql.ValueAsync<string>($"SELECT Mobile FROM [identity].StaffAccount WHERE ClinicId = '{id}'"));
        }
    }

    [Fact]
    public async Task The_email_changed_notice_is_in_the_clinic_s_language()
    {
        // US4 scenario 6: the notice to the old address is in the clinic's language, as saved (T079).
        var (world, editor, id, email) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            using var browser = world.Factory.Browser();
            await browser.SetWithLinkAsync(world.Factory.Mail.LatestLinkTokenFor(email), SignIn.Password);
            var moved = NewEmail();

            var body = (await BodyAsync(editor, id)).With("email", moved).With("defaultLanguage", "en");
            Assert.Equal(204, (await editor.OpsPutAsync($"{ClinicsPath}/{id}", body).RefusalAsync()).Status);

            var notice = world.Factory.Mail.To(email)[^1];
            Assert.StartsWith("The sign-in email of the", notice.Subject, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_taken_email_comes_back_with_the_other_refused_fields()
    {
        // FR-021: every refusal of an edit at once, the taken email included (T089).
        var (world, editor, id, _) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            var taken = NewEmail();
            await world.Factory.CreateAccountAsync(await sql.Main.SeedClinicAsync(), email: taken);
            var body = (await BodyAsync(editor, id)).With("email", taken).With("nameAr", "");
            var (status, _, text) = await editor.OpsPutAsync($"{ClinicsPath}/{id}", body).AnswerAsync();

            Assert.Equal(400, status);
            Assert.Contains("\"email\":[\"TAKEN\"]", text, StringComparison.Ordinal);
            Assert.Contains("\"nameAr\"", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_invitation_goes_to_a_new_address_at_once_after_the_email_changed()
    {
        // FR-018, FR-021a: the minute between invitations counts only links still live (T088).
        var (world, editor, id, _) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            var moved = NewEmail();
            Assert.Equal(204, (await editor.OpsPutAsync($"{ClinicsPath}/{id}", (await BodyAsync(editor, id)).With("email", moved)).RefusalAsync()).Status);

            var sent = await editor.OpsPostAsync($"{ClinicsPath}/{id}/invitation").AnswerAsync();
            Assert.Equal(200, sent.Status);
            Assert.Equal(1, world.Factory.Mail.LinksSentTo(moved));
        }
    }

    [Fact]
    public async Task An_invited_owner_s_new_email_ends_the_old_invitation()
    {
        var (world, editor, id, email) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            var oldToken = world.Factory.Mail.LatestLinkTokenFor(email);
            var moved = NewEmail();

            Assert.Equal(204, (await editor.OpsPutAsync($"{ClinicsPath}/{id}", (await BodyAsync(editor, id)).With("email", moved)).RefusalAsync()).Status);

            using var browser = world.Factory.Browser();
            Assert.NotEqual(200, (int)(await browser.InspectLinkAsync(oldToken)).StatusCode);
            Assert.Equal("none", (await editor.JsonAsync($"{ClinicsPath}/{id}")).GetProperty("owner").GetProperty("invitation").GetProperty("state").GetString());
        }
    }

    [Fact]
    public async Task A_signed_in_owner_s_new_email_signs_in_the_old_one_does_not_and_the_old_address_is_told()
    {
        var (world, editor, id, email) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            using var browser = world.Factory.Browser();
            Assert.True((await browser.SetWithLinkAsync(world.Factory.Mail.LatestLinkTokenFor(email), SignIn.Password)).IsSuccessStatusCode);
            using var owner = await world.Factory.SignInAsAsync(email);
            Assert.NotNull(await sql.ValueAsync<DateTimeOffset?>($"SELECT LastSignInAtUtc FROM [identity].StaffAccount WHERE Email = N'{email}'"));

            var moved = NewEmail();
            Assert.Equal(204, (await editor.OpsPutAsync($"{ClinicsPath}/{id}", (await BodyAsync(editor, id)).With("email", moved)).RefusalAsync()).Status);

            // Every session ended; the old address told, with the new one masked.
            Assert.Equal(0, await sql.CountAsync("[identity].Session s JOIN [identity].StaffAccount a ON a.Id = s.AccountId", $"a.Email = N'{moved}' AND s.EndedAtUtc IS NULL"));
            Assert.Equal(1, await sql.CountAsync("[identity].Session s JOIN [identity].StaffAccount a ON a.Id = s.AccountId", $"a.Email = N'{moved}' AND s.EndReason = 'email-changed'"));
            var notice = world.Factory.Mail.To(email)[^1];
            Assert.Contains($"{moved[0]}•••@clinic.example", notice.TextBody, StringComparison.Ordinal);
            Assert.DoesNotContain(moved, notice.TextBody, StringComparison.Ordinal);

            // The new address signs in; the old one is as unknown as any.
            using var newBrowser = world.Factory.Browser();
            Assert.True((await newBrowser.StepOneAsync(moved, SignIn.Password)).IsSuccessStatusCode);
            using var oldBrowser = world.Factory.Browser();
            var refused = await oldBrowser.StepOneAsync(email, SignIn.Password).AnswerAsync();
            Assert.Equal("INVALID_CREDENTIALS", refused.Code);

            var change = await sql.ValueAsync<string>($"SELECT Changes FROM audit.PlatformEntry WHERE TargetId = '{id}' AND ActionCode = 'clinic.owner-updated'");
            Assert.Contains(email, change, StringComparison.Ordinal);
            Assert.Contains(moved, change, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_code_sent_to_the_old_address_before_the_change_no_longer_signs_in()
    {
        // FR-021a, US4 scenario 6: the old address no longer gets in, not even through a sign-in it
        // had started (T094).
        var (world, editor, id, email) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            using var browser = world.Factory.Browser();
            Assert.True((await browser.SetWithLinkAsync(world.Factory.Mail.LatestLinkTokenFor(email), SignIn.Password)).IsSuccessStatusCode);
            using var started = world.Factory.Browser();
            Assert.True((await started.StepOneAsync(email, SignIn.Password)).IsSuccessStatusCode);
            var code = world.Factory.Mail.LatestCodeFor(email);

            var moved = NewEmail();
            Assert.Equal(204, (await editor.OpsPutAsync($"{ClinicsPath}/{id}", (await BodyAsync(editor, id)).With("email", moved)).RefusalAsync()).Status);

            using var second = await started.StepTwoAsync(code);
            Assert.False(second.IsSuccessStatusCode);
            Assert.Equal(0, await sql.CountAsync("[identity].Session s JOIN [identity].StaffAccount a ON a.Id = s.AccountId", $"a.Email = N'{moved}' AND s.EndedAtUtc IS NULL"));
        }
    }

    [Fact]
    public async Task A_failed_notice_leaves_the_change_in_place()
    {
        var (world, editor, id, email) = await AddedAsync(sql);
        using (world)
        using (editor)
        {
            using var browser = world.Factory.Browser();
            await browser.SetWithLinkAsync(world.Factory.Mail.LatestLinkTokenFor(email), SignIn.Password);
            world.Factory.Mail.FailingFor[email] = true;
            var moved = NewEmail();

            Assert.Equal(204, (await editor.OpsPutAsync($"{ClinicsPath}/{id}", (await BodyAsync(editor, id)).With("email", moved)).RefusalAsync()).Status);
            Assert.Equal(1, await sql.CountAsync("[identity].StaffAccount", $"Email = N'{moved}'"));
        }
    }
}
