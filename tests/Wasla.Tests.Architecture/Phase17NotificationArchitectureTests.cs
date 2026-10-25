using System.Reflection;
using Wasla.Application.Features.Notifications;
using Wasla.Domain.Notifications;
using Wasla.Api.Controllers;

namespace Wasla.Tests.Architecture;

public sealed class Phase17NotificationArchitectureTests
{
    private static readonly string[] SensitiveFields = ["Diagnosis", "ClinicalNotes", "Medication", "ResultValues", "PrivateMediaKey", "RecipientEmail", "HtmlBody", "Password"];
    [Fact]
    public void E04_E07_E08_C11_O14_contracts_have_no_arbitrary_send_or_clinical_payload_surface()
    {
        var methods = typeof(NotificationsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Concat(typeof(AdminNotificationsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.DoesNotContain(methods, m => m.Name.Contains("Send", StringComparison.OrdinalIgnoreCase));
        var payloads = new[] { typeof(NotificationEvent), typeof(UserNotification), typeof(DeliveryResponse) };
        foreach (var type in payloads)
            foreach (var property in type.GetProperties())
                Assert.DoesNotContain(SensitiveFields,
                    term => property.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        Assert.False(NotificationPolicy.KnownEvent("Ticket.Called")); Assert.False(NotificationPolicy.KnownEvent("Ticket.Recalled"));
        Assert.False(NotificationPolicy.KnownEvent("Reception.AccountInvitation"));
        // O16: the module exposes no disposal route and has no retention/purge hosted service.
        Assert.DoesNotContain(methods, m => m.GetCustomAttributes(true).Any(a => a.GetType().Name == "HttpDeleteAttribute"));
        var workers = typeof(Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications.NotificationDispatchProcessor).Assembly.GetTypes()
            .Where(t => t.Namespace == "Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications" && typeof(Microsoft.Extensions.Hosting.IHostedService).IsAssignableFrom(t)).ToArray();
        Assert.Equal("NotificationDispatchBackgroundService", Assert.Single(workers).Name);
        // O17: notification preferences cannot activate independent clinical/privacy releases.
        Assert.False(new NotificationPrivacyOptions().ClinicalEmailReleaseApproved);
        Assert.False(new NotificationPrivacyOptions().ClinicalInAppReleaseApproved);
        Assert.False(new Wasla.Application.Features.Clinical.MedicalRecordOptions().CrossDoctorSharingEnabled);
        Assert.False(new Wasla.Application.Features.Clinical.MedicalRecordOptions().GuardianSharingEnabled);
    }
}
