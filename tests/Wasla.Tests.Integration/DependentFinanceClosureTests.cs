using System.Net;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Payments;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.FamilyClosureRegressionTests;

namespace Wasla.Tests.Integration;

public sealed class DependentFinanceClosureTests
{
    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Parent_reads_exact_child_payment_and_refund_receipts_without_staff_notes_and_cross_child_scope_is_denied()
    {
        await using var f = await CreateAsync(true); Enable(f); using var parent = await f.ClientAsync("patient"); using var admin = await f.ClientAsync("root"); using var reception = await f.ClientAsync("reception");
        var child = await Child(f, parent, admin, "طفل الإيصال"); var sibling = await Child(f, parent, admin, "أخ الإيصال");
        var ticket = await f.WalkInAsync(reception, patientId: child); var ticketId = ticket.GetProperty("ticketId").GetGuid(); Guid paymentId = default, refundId = default;
        const string privateNote = "SYNTHETIC_INTERNAL_STAFF_FINANCE_NOTE";
        await f.WithDbAsync(async db => {
            var payment = await db.Payments.SingleAsync(p => p.TicketId == ticketId); paymentId = payment.Id;
            var source = await db.Tickets.SingleAsync(t => t.Id == ticketId); Assert.True(source.Cancel(f.DoctorUserId, "Synthetic cancellation", f.Clock.UtcNow).IsSuccess);
            var refund = Refund.Record(Guid.NewGuid(), payment, new FinancialTransactionNumber(1, "REF-SYNTHETIC-CLOSURE-000001"), f.Today, PaymentMethod.Cash,
                RefundReasonCode.PatientRequestedCancellation, privateNote, null, privateNote, f.DoctorUserId, f.Clock.UtcNow).Value;
            refundId = refund.Id; db.Add(refund); await db.SaveChangesAsync();
        });
        var prefix = $"/api/v1/families/mine/dependents/{child}";
        var paymentReceipt = await GetAsync(parent, prefix + $"/payments/{paymentId}/receipt"); Assert.DoesNotContain(privateNote, paymentReceipt.GetRawText(), StringComparison.Ordinal);
        var refundReceipt = await GetAsync(parent, prefix + $"/refunds/{refundId}/receipt"); Assert.DoesNotContain(privateNote, refundReceipt.GetRawText(), StringComparison.Ordinal);
        var transactions = await GetAsync(parent, prefix + $"/financial-transactions?patientId={sibling}"); Assert.Equal(2, transactions.GetProperty("items").GetArrayLength()); Assert.DoesNotContain(privateNote, transactions.GetRawText(), StringComparison.Ordinal);
        using var crossPayment = await parent.GetAsync($"/api/v1/families/mine/dependents/{sibling}/payments/{paymentId}/receipt", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, crossPayment.StatusCode);
        using var crossRefund = await parent.GetAsync($"/api/v1/families/mine/dependents/{sibling}/refunds/{refundId}/receipt", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, crossRefund.StatusCode);
        await f.WithDbAsync(db => db.RolePermissions.Where(p => p.RoleId == SystemRoleIds.Patient && p.PermissionId == SystemPermissionIds.For(FamilyExtensionPermissionNames.Receipts)).ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        using var revoked = await parent.GetAsync(prefix + $"/payments/{paymentId}/receipt", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        Assert.Equal(2, (await GetAsync(parent, prefix + "/financial-transactions")).GetProperty("items").GetArrayLength());
    }
}
