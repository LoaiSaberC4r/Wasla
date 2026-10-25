using System.Security.Cryptography;
using System.Text;

namespace Wasla.Domain.Notifications;

// Durable proof of a verified email-to-identity binding, independent of short-lived OTP cleanup.
// The address is not duplicated; current identity/contact state must still match this fingerprint.
public sealed class NotificationVerifiedContact
{
    private NotificationVerifiedContact() { }
    public NotificationVerifiedContact(Guid user, Guid patient, string normalizedEmail, Guid evidence, DateTime verifiedAtUtc)
    {
        if (user == Guid.Empty || patient == Guid.Empty || evidence == Guid.Empty || string.IsNullOrWhiteSpace(normalizedEmail)) throw new ArgumentException("Verified contact evidence required.");
        Id = Guid.NewGuid(); ApplicationUserId = user; PatientId = patient; AddressFingerprint = Fingerprint(normalizedEmail);
        EvidenceId = evidence; VerifiedAtUtc = verifiedAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid ApplicationUserId { get; private set; }
    public Guid PatientId { get; private set; }
    public string AddressFingerprint { get; private set; } = "";
    public Guid EvidenceId { get; private set; }
    public DateTime VerifiedAtUtc { get; private set; }
    public static string Fingerprint(string email) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToUpperInvariant())));
}
