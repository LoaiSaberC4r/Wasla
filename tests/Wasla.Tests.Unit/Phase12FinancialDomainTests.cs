using Wasla.Domain.Payments;

namespace Wasla.Tests.Unit;

public sealed class Phase12FinancialDomainTests
{
    [Theory]
    [InlineData(PaymentMethod.Cash)]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.Wallet)]
    public void Payment_records_exact_full_egp_amount_with_selectable_method(PaymentMethod method)
    {
        var snapshot = NewPaymentSnapshot(method);
        var result = Payment.RecordPaid(snapshot);

        Assert.True(result.IsSuccess);
        Assert.Equal(500m, result.Value.Amount);
        Assert.Equal("EGP", result.Value.CurrencyCode);
        Assert.Equal(method, result.Value.PaymentMethod);
        Assert.Equal(PaymentStatus.Paid, result.Value.Status);
        Assert.True(Payment.RecordPaid(snapshot with { Amount = 499m }).IsFailure);
        Assert.True(Payment.RecordPaid(snapshot with { Amount = 501m }).IsFailure);
        Assert.True(Payment.RecordPaid(snapshot with
        {
            PaymentMethod = PaymentMethod.LegacyUnspecified
        }).IsFailure);
    }

    [Fact]
    public void Refund_is_full_once_and_does_not_replace_original_payment()
    {
        var payment = Payment.RecordPaid(NewPaymentSnapshot(PaymentMethod.Card)).Value;
        var first = Refund.Record(
            Guid.NewGuid(), payment, NewRefundNumber(), new DateOnly(2026, 10, 2),
            PaymentMethod.Wallet, RefundReasonCode.PatientRequestedCancellation,
            null, null, null, Guid.NewGuid(), new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc));

        Assert.True(first.IsSuccess);
        Assert.Equal(payment.Amount, first.Value.Amount);
        Assert.Equal("EGP", first.Value.CurrencyCode);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.True(Refund.Record(
            Guid.NewGuid(), payment, NewRefundNumber(), new DateOnly(2026, 10, 2),
            PaymentMethod.Cash, RefundReasonCode.OperationalError,
            null, null, null, Guid.NewGuid(), DateTime.UtcNow).IsFailure);
    }

    [Fact]
    public void Payment_correction_preserves_financial_identity_and_stops_after_refund()
    {
        var payment = Payment.RecordPaid(NewPaymentSnapshot(PaymentMethod.Cash)).Value;
        var originalAmount = payment.Amount;
        var originalNumber = payment.TransactionNumber;
        var originalPatient = payment.PatientId;
        var actor = Guid.NewGuid();

        var correction = payment.CorrectMetadata(
            PaymentMethod.Card, "POS-123", "Corrected payment method",
            "Cash entry was mistaken", actor, DateTime.UtcNow);
        Assert.True(correction.IsSuccess);
        Assert.Single(payment.CorrectionHistory);
        Assert.Equal(PaymentMethod.Cash, correction.Value.OldPaymentMethod);
        Assert.Equal(PaymentMethod.Card, correction.Value.NewPaymentMethod);
        Assert.Equal(originalAmount, payment.Amount);
        Assert.Equal(originalNumber, payment.TransactionNumber);
        Assert.Equal(originalPatient, payment.PatientId);
        Assert.True(payment.CorrectMetadata(
            PaymentMethod.Card, "POS-123", "Corrected payment method",
            "No change", actor, DateTime.UtcNow).IsFailure);

        Assert.True(Refund.Record(
            Guid.NewGuid(), payment, NewRefundNumber(), new DateOnly(2026, 10, 2),
            PaymentMethod.Cash, RefundReasonCode.OperationalError,
            null, null, null, actor, DateTime.UtcNow).IsSuccess);
        Assert.True(payment.CorrectMetadata(
            PaymentMethod.Wallet, null, null, "Post-refund change",
            actor, DateTime.UtcNow).IsFailure);
    }

    [Fact]
    public void Other_refund_reason_requires_text_and_refund_correction_records_old_and_new_values()
    {
        var payment = Payment.RecordPaid(NewPaymentSnapshot(PaymentMethod.Cash)).Value;
        var invalid = Refund.Record(
            Guid.NewGuid(), payment, NewRefundNumber(), new DateOnly(2026, 10, 2),
            PaymentMethod.Card, RefundReasonCode.Other, null,
            null, null, Guid.NewGuid(), DateTime.UtcNow);
        Assert.True(invalid.IsFailure);

        var refund = Refund.Record(
            Guid.NewGuid(), payment, NewRefundNumber(), new DateOnly(2026, 10, 2),
            PaymentMethod.Card, RefundReasonCode.Other, "Special circumstance",
            null, null, Guid.NewGuid(), DateTime.UtcNow).Value;
        var correction = refund.CorrectMetadata(
            PaymentMethod.Wallet, RefundReasonCode.DoctorUnavailable, null,
            "Wallet-42", null, "Correct refund details", Guid.NewGuid(), DateTime.UtcNow);

        Assert.True(correction.IsSuccess);
        Assert.Single(refund.CorrectionHistory);
        Assert.Equal(PaymentMethod.Card, correction.Value.OldRefundMethod);
        Assert.Equal(PaymentMethod.Wallet, correction.Value.NewRefundMethod);
        Assert.Equal(RefundReasonCode.Other, correction.Value.OldRefundReasonCode);
        Assert.Equal(RefundReasonCode.DoctorUnavailable, correction.Value.NewRefundReasonCode);
        Assert.Equal(payment.Amount, refund.Amount);
        Assert.Equal(payment.Id, refund.PaymentId);
    }

    private static PaymentRecordSnapshot NewPaymentSnapshot(PaymentMethod method)
        => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            Guid.NewGuid(), 500m, 500m, $"PAY-TEST-{Guid.NewGuid():N}", 1,
            new DateOnly(2026, 9, 30), method, null, null,
            Guid.NewGuid(), new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc));

    private static FinancialTransactionNumber NewRefundNumber()
        => new(1, $"REF-TEST-{Guid.NewGuid():N}");
}
