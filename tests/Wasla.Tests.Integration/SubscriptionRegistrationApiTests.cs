using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Subscriptions;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class SubscriptionRegistrationApiTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Registration_without_selection_and_approval_grant_eligibility_without_starting_trial(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var anonymous = app.CreateClient();
        using var form = Registration("missing");
        using var registered = await anonymous.PostAsync("/api/v1/auth/doctors/register", form, TestContext.Current.CancellationToken);
        Assert.True(registered.StatusCode == HttpStatusCode.Created, await registered.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var response = await JsonAsync(registered); var doctorId = response.GetProperty("doctorId").GetGuid();
        Assert.Equal(Guid.Empty, response.GetProperty("subscriptionId").GetGuid());
        app.Clock.UtcNow = app.Clock.UtcNow.AddDays(4); using var admin = await app.ClientAsync("root");
        var details = await GetAsync(admin, "/api/v1/admin/doctors/" + doctorId);
        using var approved = await PostAsync(admin, $"/api/v1/admin/doctors/{doctorId}/approve", new { nationalId = "29902021234987", rowVersion = details.GetProperty("rowVersion").GetString() });
        Assert.True(approved.StatusCode == HttpStatusCode.OK, await approved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await app.WithDbAsync(async db =>
        {
            Assert.NotNull((await db.Doctors.SingleAsync(d => d.Id == doctorId)).ApprovedOnUtc);
            Assert.Empty(await db.DoctorSubscriptions.Where(s => s.DoctorId == doctorId).ToArrayAsync());
            Assert.True(await db.EmailOutboxMessages.AnyAsync(e => e.IdempotencyKey.StartsWith("doctor-approved:") && e.RecipientEmail == "missing@example.test"));
        });
    }
    [Theory][InlineData("paid")][InlineData("annual")][InlineData("invalid-product")][InlineData("invalid-plan")][InlineData("invalid-trial")][InlineData("trial")][InlineData("disabled-booking")][InlineData("missing")]
    public async Task Registration_validates_commercial_selection_before_media_or_identity_writes(string selection)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var anonymous = app.CreateClient(); using var form = Registration(selection);
        using var response = await anonymous.PostAsync("/api/v1/auth/doctors/register", form, TestContext.Current.CancellationToken);
        var valid = selection is "paid" or "annual" or "disabled-booking" or "missing";
        Assert.Equal(valid ? HttpStatusCode.Created : HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await app.WithDbAsync(async db =>
        { Assert.Equal(valid, await db.ApplicationUsers.AnyAsync(u => u.Email == selection + "@example.test")); Assert.Equal(valid && selection != "missing", await db.DoctorSubscriptions.AnyAsync(s => db.Doctors.Any(d => d.Id == s.DoctorId && d.NameEn == selection))); });
    }
    private static MultipartFormDataContent Registration(string selection)
    {
        var form = new MultipartFormDataContent();
        var fields = new Dictionary<string, string>
        {
            ["UserName"] = selection, ["Email"] = selection + "@example.test", ["PhoneNumber"] = "01011111111",
            ["Password"] = "CommercialTest123!", ["ConfirmPassword"] = "CommercialTest123!", ["NameAr"] = "طبيب تجريبي", ["NameEn"] = selection,
            ["DateOfBirth"] = "1990-01-01", ["Gender"] = "Male"
        };
        if (selection != "missing")
        {
            fields["Subscription.ProductCode"] = selection == "invalid-product" ? "INVALID" : selection == "disabled-booking" ? "ONLINE_BOOKING_ONLY" : "FULL_PLATFORM";
            fields["Subscription.PlanCode"] = selection == "invalid-plan" ? "INVALID" : selection == "disabled-booking" ? "BOOKING_ONLY" : selection == "annual" ? "PROFESSIONAL" : "STARTER";
            if (selection is "trial" or "invalid-trial") fields["Subscription.OfferCode"] = selection == "trial" ? "STARTER_FREE_TRIAL_20D" : "INVALID";
            else fields["Subscription.BillingCycle"] = selection == "annual" ? "Annual" : "Monthly";
            if (selection == "disabled-booking") fields["Subscription.SelectedPracticeCount"] = "3";
        }
        foreach (var (key, value) in fields) form.Add(new StringContent(value), key);
        foreach (var name in new[] { "PersonalIdFrontImage", "PersonalIdBackImage", "SyndicateCardFrontImage" })
        { var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]); file.Headers.ContentType = new MediaTypeHeaderValue("image/png"); form.Add(file, name, name + ".png"); }
        return form;
    }
}
