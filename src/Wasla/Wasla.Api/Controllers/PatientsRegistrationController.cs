using Wasla.Api.OpenApi;
using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wasla.Api.Security;
using Wasla.Application.Features.PatientOnboarding;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
using Wasla.Domain.Common;

namespace Wasla.Api.Controllers;
[ApiController, ApiVersion("1.0"), Route("api/v{version:apiVersion}/patients"), Authorize]
[EnableRateLimiting("patient-identity"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PatientsRegistrationController(ISender sender) : ControllerBase
{
    [HttpPost("full"), Consumes("multipart/form-data"), Permission(PermissionNames.PatientsRegisterFull)]
    [ProducesResponseType<Wasla.Application.Features.PatientOnboarding.ReceptionFullRegistrationResponse>(201)]
    public async Task<IActionResult> Full([FromForm] ReceptionFullPatientRequest p, CancellationToken ct)
    {
        var input = new PatientIdentityInput { DoctorPracticeId = p.DoctorPracticeId ?? Guid.Empty, NameAr = p.NameAr, NameEn = p.NameEn, DateOfBirth = p.DateOfBirth, Gender = p.Gender,
            Email = p.Email, PhoneNumber = p.PhoneNumber, ProfileImage = PatientOnboardingController.Upload(p.ProfileImage), PrimaryContactNameAr = p.PrimaryContactNameAr,
            PrimaryContactNameEn = p.PrimaryContactNameEn, PrimaryContactPhoneNumber = p.PrimaryContactPhoneNumber, PrimaryContactEmail = p.PrimaryContactEmail,
            PrimaryContactLinkedPatientId = p.PrimaryContactLinkedPatientId, PrimaryContactRelationshipType = p.PrimaryContactRelationshipType, PrimaryContactIsPrimary = p.PrimaryContactIsPrimary, VerifiedInPerson = p.VerifiedInPerson,
            VerificationBasis = p.IdentityVerificationBasis, VerificationNote = p.VerificationNote, MatchReviewOutcome = p.MatchReviewOutcome, MatchReviewReason = p.MatchReviewReason };
        var r = await sender.Send(new PatientIdentityCommand(PatientIdentityMutation.Full, input, Request.Headers["Idempotency-Key"].ToString()), ct);
        return r.IsSuccess ? StatusCode(201, r.Value) : r.Errors.ToActionProblem(ct);
    }
    [HttpPost("registration-matches"), Permission(PermissionNames.PatientsRegistrationMatch)]
    public async Task<IActionResult> Matches(PatientRegistrationMatchesRequest p, CancellationToken ct)
    {
        var input = new PatientIdentityInput { DoctorPracticeId = p.DoctorPracticeId, Email = p.Mode == "ConfirmClaimIdentity" ? p.ClaimantEmail : p.Email, PhoneNumber = p.PhoneNumber,
            NameAr = p.NameAr ?? "", DateOfBirth = p.DateOfBirth ?? default, Gender = p.Gender ?? default, PageSize = p.PageSize, PatientId = p.PatientId ?? Guid.Empty,
            VerifiedInPerson = p.VerifiedInPerson, VerificationBasis = p.VerificationBasis, Reason = p.Reason };
        if (p.Mode == "Search") return (await sender.Send(new PatientIdentityQuery(PatientIdentityRead.Matches, input), ct)).ToIActionResult(ct);
        if (p.Mode is not ("ConfirmReuse" or "ConfirmClaimIdentity")) return new[] { PatientIdentityErrors.Invalid }.ToActionProblem(ct);
        return (await sender.Send(new PatientIdentityCommand(p.Mode == "ConfirmReuse" ? PatientIdentityMutation.ConfirmReuse : PatientIdentityMutation.ConfirmClaimIdentity,
            input, Request.Headers["Idempotency-Key"].ToString()), ct)).ToIActionResult(ct);
    }
}
public sealed class ReceptionFullPatientRequest : CreateReceptionPatientRequest
{
    public bool VerifiedInPerson { get; set; }
    public PatientIdentityVerificationBasis IdentityVerificationBasis { get; set; }
    public string? VerificationNote { get; set; }
}
public sealed record PatientRegistrationMatchesRequest(string Mode, Guid DoctorPracticeId, string? Email = null, string? PhoneNumber = null,
    string? NameAr = null, DateOnly? DateOfBirth = null, Gender? Gender = null, int PageSize = 20, Guid? PatientId = null,
    bool VerifiedInPerson = false, PatientIdentityVerificationBasis VerificationBasis = default, string? Reason = null, string? ClaimantEmail = null);
