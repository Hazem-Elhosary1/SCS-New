using Scs.IntegrationTests.Fixtures;
using Scs.IntegrationTests.Operators;
using static Scs.IntegrationTests.Fixtures.ClinicFixtures;

namespace Scs.IntegrationTests.Clinics;

/// <summary>
/// Spec 015, User Story 1 (FR-002 – FR-006, FR-025 – FR-027; SC-002, SC-003): one «إضافة العيادة»
/// makes the clinic, its plan and trial, its main branch and its invited owner, records it in both
/// trails as the operator, emails the invitation — and a refused add leaves nothing at all.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AddClinicTests(SqlServerFixture sql)
{
    [Fact]
    public async Task A_browser_also_signed_in_to_a_clinic_adds_and_reads_clinics_as_the_operator()
    {
        // Locally both consoles share the host, so the browser carries a clinic's session too. An
        // operators' route takes nothing from it: no clinic is established by it (spec 010, FR-037).
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        var seeded = await sql.Main.SeedClinicAsync();
        var (_, staffEmail) = await world.Factory.CreateAccountAsync(seeded);
        using var browser = await world.Factory.SignInAsAsync(staffEmail);
        var (_, operatorEmail, _) = await world.Factory.CreateOperatorWithAsync(Editor);
        using (var first = await browser.OpsStepOneAsync(operatorEmail))
        using (var second = await browser.OpsStepTwoAsync(world.Factory.Mail.LatestCodeFor(operatorEmail)))
        {
            Assert.True(second.IsSuccessStatusCode, $"Step two answered {(int)second.StatusCode}.");
        }

        var (id, _) = await browser.AddClinicAsync(NewClinic(plan));

        Assert.Equal(id, (await browser.JsonAsync($"{ClinicsPath}/{id}")).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task An_add_makes_the_whole_clinic_and_invites_its_owner()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        var (me, editor) = await world.Factory.SignInEditorAsync();
        using var _ = editor;
        var email = NewEmail();
        var nameEn = $"Al Noor {Guid.NewGuid():N}"[..20];

        var (id, slug) = await editor.AddClinicAsync(NewClinic(plan, nameEn, email));

        Assert.Equal(nameEn.ToLowerInvariant().Replace(' ', '-'), slug);
        Assert.Equal("active", await sql.ValueAsync<string>($"SELECT State FROM tenancy.Clinic WHERE Id = '{id}'"));
        Assert.Equal("123-456-789", await sql.ValueAsync<string>($"SELECT TaxNumber FROM tenancy.Clinic WHERE Id = '{id}'"));
        Assert.Equal("dental", await sql.ValueAsync<string>($"SELECT Specialty FROM tenancy.Clinic WHERE Id = '{id}'"));
        Assert.Equal("ar", await sql.ValueAsync<string>($"SELECT DefaultLanguage FROM tenancy.Clinic WHERE Id = '{id}'"));
        Assert.Equal(me.ToString(), await sql.ValueAsync<string>($"SELECT CreatedBy FROM tenancy.Clinic WHERE Id = '{id}'"));

        // The plan and a trial of the deployment's length: 14 days by default.
        Assert.Equal(plan, await sql.ValueAsync<Guid>($"SELECT PlanId FROM subscriptions.ClinicPlan WHERE ClinicId = '{id}'"));
        var trialDays = await sql.ValueAsync<int>($"SELECT DATEDIFF(hour, SetAtUtc, TrialEndsAtUtc) FROM subscriptions.ClinicPlan WHERE ClinicId = '{id}'");
        Assert.Equal(14 * 24, trialDays);

        // One main branch with the clinic's names and address.
        Assert.Equal(1, await sql.CountAsync("clinicdata.Branch", $"ClinicId = '{id}' AND IsMain = 1 AND NameEn = N'{nameEn}' AND Address = N'14 شارع التحرير، القاهرة'"));

        // The owner: invited, no password, its name and mobile, holding the owner role.
        Assert.Equal(1, await sql.CountAsync(
            "[identity].StaffAccount a JOIN [identity].Role r ON r.Id = a.RoleId",
            $"a.ClinicId = '{id}' AND a.Email = N'{email}' AND a.State = 'invited' AND a.PasswordHash IS NULL AND a.FullName = N'هيثم محمود' AND a.Mobile = '+201001234567' AND r.HoldsEverything = 1"));

        // The invitation, in Arabic, with a 72-hour link.
        var invitation = Assert.Single(world.Factory.Mail.To(email));
        Assert.Contains("/set-password#t=", invitation.TextBody, StringComparison.Ordinal);
        Assert.Contains("72", invitation.TextBody, StringComparison.Ordinal);
        Assert.Contains("عيادة الاختبار", invitation.Subject, StringComparison.Ordinal);

        // Both trails, the operator named in each.
        Assert.Equal(1, await sql.CountAsync("audit.PlatformEntry", $"TargetId = '{id}' AND ActionCode = 'clinic.created' AND ActorKind = 'operator' AND ActorAccountId = '{me}'"));
        Assert.Equal(1, await sql.CountAsync("audit.Entry", $"ClinicId = '{id}' AND ActionCode = 'clinic.created' AND ActorKind = 'operator' AND ActorAccountId = '{me}'"));
        Assert.Equal(1, await sql.CountAsync("audit.Entry", $"ClinicId = '{id}' AND ActionCode = 'subscription.plan-set' AND ActorKind = 'operator'"));
        Assert.DoesNotContain(email, await sql.ValueAsync<string>($"SELECT Facts FROM audit.PlatformEntry WHERE TargetId = '{id}' AND ActionCode = 'clinic.created'") ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_slug_takes_the_next_number_and_the_english_name_falls_back_to_the_arabic()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var nameEn = $"Twin {Guid.NewGuid():N}"[..17];
        var expected = nameEn.ToLowerInvariant().Replace(' ', '-');

        Assert.Equal(expected, (await editor.AddClinicAsync(NewClinic(plan, nameEn))).Slug);
        Assert.Equal($"{expected}-2", (await editor.AddClinicAsync(NewClinic(plan, nameEn))).Slug);

        var arabicOnly = NewClinic(plan).With("nameEn", "  ").With("nameAr", "عيادة الأمل");
        var (id, slug) = await editor.AddClinicAsync(arabicOnly);
        Assert.Matches("^clinic-[a-z0-9]{6}$", slug);
        Assert.Equal("عيادة الأمل", await sql.ValueAsync<string>($"SELECT NameEn FROM tenancy.Clinic WHERE Id = '{id}'"));
    }

    [Fact]
    public async Task A_failed_invitation_email_leaves_the_clinic_made_and_its_invitation_none()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var email = NewEmail();
        world.Factory.Mail.FailingFor[email] = true;

        var (id, _) = await editor.AddClinicAsync(NewClinic(plan, email: email));

        var detail = await editor.JsonAsync($"{ClinicsPath}/{id}");
        Assert.Equal("none", detail.GetProperty("owner").GetProperty("invitation").GetProperty("state").GetString());
        Assert.Equal("active", await sql.ValueAsync<string>($"SELECT State FROM tenancy.Clinic WHERE Id = '{id}'"));
    }

    [Fact]
    public async Task Every_refusal_writes_nothing_at_all()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        var disabled = await world.Factory.PutPlanAsync(disabled: true);
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var taken = NewEmail();
        await world.Factory.CreateAccountAsync(await sql.Main.SeedClinicAsync(), email: taken);

        (Func<string, string, Dictionary<string, object?>> Body, int Status, string Code, string? Field, string? FieldCode)[] refusals =
        [
            ((n, e) => NewClinic(plan, n, e).With("nameAr", " "), 400, "VALIDATION_FAILED", "nameAr", "REQUIRED"),
            ((n, e) => NewClinic(plan, n, e).With("ownerName", ""), 400, "VALIDATION_FAILED", "ownerName", "REQUIRED"),
            ((n, e) => NewClinic(plan, n, e).With("address", new string('ع', 301)), 400, "VALIDATION_FAILED", "address", "TOO_LONG"),
            ((n, e) => NewClinic(plan, n, e).With("taxNumber", "12345"), 400, "VALIDATION_FAILED", "taxNumber", "TAX_NUMBER"),
            ((n, e) => NewClinic(plan, n, e).With("mobile", "12ab"), 400, "VALIDATION_FAILED", "mobile", "MOBILE"),
            // Eastern-Arabic digits are refused at their field, not by the database (FR-003; T076).
            ((n, e) => NewClinic(plan, n, e).With("taxNumber", "١٢٣٤٥٦٧٨٩"), 400, "VALIDATION_FAILED", "taxNumber", "TAX_NUMBER"),
            ((n, e) => NewClinic(plan, n, e).With("mobile", "+٢٠١٠٠١٢٣٤٥٦٧"), 400, "VALIDATION_FAILED", "mobile", "MOBILE"),
            ((n, e) => NewClinic(plan, n, e).With("email", "not-an-email"), 400, "VALIDATION_FAILED", "email", "INVALID_FORMAT"),
            ((n, e) => NewClinic(plan, n, e).With("specialty", "surgery"), 400, "VALIDATION_FAILED", "specialty", "INVALID"),
            ((n, e) => NewClinic(plan, n, e).With("planId", null), 400, "VALIDATION_FAILED", "planId", "REQUIRED"),
            ((n, _) => NewClinic(plan, n, taken), 400, "VALIDATION_FAILED", "email", "TAKEN"),
            ((n, e) => NewClinic(disabled, n, e), 409, "PLAN_DISABLED", null, null),
            ((n, e) => NewClinic(Guid.NewGuid(), n, e), 409, "PLAN_DISABLED", null, null),
        ];

        foreach (var (body, status, code, field, fieldCode) in refusals)
        {
            var email = NewEmail();
            var nameEn = $"Refused {Guid.NewGuid():N}"[..24];
            var sentEmail = body(nameEn, email)["email"]?.ToString() ?? email;
            var before = await sql.LeftoversAsync(sentEmail, nameEn);
            var (got, gotCode, text) = await editor.OpsPostAsync(ClinicsPath, body(nameEn, email)).AnswerAsync();

            Assert.True(got == status && gotCode == code, $"{field ?? code}: answered {got} {gotCode}: {text}");
            if (field is not null)
            {
                Assert.Contains($"\"{field}\":[\"{fieldCode}\"]", text, StringComparison.Ordinal);
            }

            Assert.Equal(before, await sql.LeftoversAsync(sentEmail, nameEn));
        }
    }

    [Fact]
    public async Task A_slow_mail_server_does_not_hold_up_the_adds_behind_it()
    {
        // SC-002: the add's turn is let go before the invitation is sent (T078). Each email takes six
        // seconds; held under the turn, the third add would wait more than its ten.
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        world.Factory.Mail.Delay = TimeSpan.FromSeconds(6);

        var answers = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => editor.OpsPostAsync(ClinicsPath, NewClinic(plan)).AnswerAsync()));

        Assert.All(answers, a => Assert.True(a.Status == 201, $"answered {a.Status} {a.Code}"));
    }

    [Fact]
    public async Task Ten_adds_at_once_with_one_name_get_ten_slugs_and_two_with_one_email_get_one_clinic()
    {
        using var world = await OpsWorld.CreateAsync(sql);
        var plan = await world.Factory.PutPlanAsync();
        using var editor = await world.Factory.SignInWithAsync(Editor);
        var nameEn = $"Crowd {Guid.NewGuid():N}"[..18];

        var all = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => editor.AddClinicAsync(NewClinic(plan, nameEn))));
        Assert.Equal(10, all.Select(a => a.Slug).Distinct().Count());

        var email = NewEmail();
        var twins = new[] { $"Twin A {Guid.NewGuid():N}"[..20], $"Twin B {Guid.NewGuid():N}"[..20] };
        var answers = await Task.WhenAll(twins.Select(n => editor.OpsPostAsync(ClinicsPath, NewClinic(plan, n, email)).AnswerAsync()));

        Assert.Single(answers, a => a.Status == 201);
        var refused = Assert.Single(answers, a => a.Status != 201);
        Assert.Equal((400, "VALIDATION_FAILED"), (refused.Status, refused.Code));
        Assert.Contains("\"email\":[\"TAKEN\"]", refused.Text, StringComparison.Ordinal);
        Assert.Equal(1, await sql.CountAsync("[identity].StaffAccount", $"Email = N'{email}'"));
        Assert.Equal(1, await sql.CountAsync("tenancy.Clinic", $"NameEn IN (N'{twins[0]}', N'{twins[1]}')"));
    }
}
