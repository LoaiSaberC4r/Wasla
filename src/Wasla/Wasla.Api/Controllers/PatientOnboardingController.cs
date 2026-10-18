using Wasla.Api.OpenApi;
using Asp.Versioning;
using BuildingBlock.Api;
using BuildingBlock.Application.Abstraction.Media;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Serialization;
using Wasla.Application.Features.PatientOnboarding;
using Wasla.Domain.Patients;
using Wasla.Domain.Common;

namespace Wasla.Api.Controllers;

[ApiController, ApiVersion("1.0"), Route("api/v{version:apiVersion}/patient-onboarding")]
[AllowAnonymous, EnableRateLimiting("patient-identity"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PatientOnboardingController(ISender sender) : ControllerBase
{
    [HttpPost("start")]
    [ProducesResponseType<PatientEmailStartResponse>(202)]
    public Task<IActionResult> Start(StartPatientEmailRequest request, CancellationToken ct) => Write(PatientIdentityMutation.Start,
        new() { Email = request.Email, Purpose = request.Purpose, PatientReference = request.PatientReference }, 202, ct);
    [HttpPost("verify-email")]
    [ProducesResponseType<PatientEmailVerifiedResponse>(200)]
    public Task<IActionResult> Verify(VerifyPatientEmailRequest request, CancellationToken ct) => Write(PatientIdentityMutation.Verify, new() { RequestId = request.RequestId, Otp = request.Otp }, 200, ct);
    [HttpPost("register"), Consumes("multipart/form-data")]
    [ProducesResponseType<Wasla.Application.Features.PatientOnboarding.VerifiedPatientRegistrationResponse>(201)]
    public Task<IActionResult> Register([FromForm] RegisterPatientRequest request, CancellationToken ct) => Write(PatientIdentityMutation.Register, RegistrationInput(request), 201, ct);
    [HttpPost("claims"), Consumes("multipart/form-data")]
    [ProducesResponseType<PatientClaimSubmittedResponse>(201)]
    public Task<IActionResult> Claim([FromForm] PatientClaimSubmissionRequest request, CancellationToken ct) => Write(PatientIdentityMutation.SubmitClaim, request.ToInput(), 201, ct);
    [HttpGet("claims/{id:guid}")]
    [ProducesResponseType<PatientClaimStatusResponse>(200)]
    public async Task<IActionResult> ClaimStatus(Guid id, CancellationToken ct) => (await sender.Send(new PatientIdentityQuery(PatientIdentityRead.OwnClaim, new(), Request.Headers["X-Onboarding-Token"].ToString(), id), ct)).ToIActionResult(ct);
    [HttpPost("claims/{id:guid}/resubmit"), Consumes("multipart/form-data")]
    [ProducesResponseType<PatientClaimResubmittedResponse>(200)]
    public Task<IActionResult> Resubmit(Guid id, [FromForm] PatientClaimResubmissionRequest request, CancellationToken ct) => Write(PatientIdentityMutation.ResubmitClaim, request.ToInput(), 200, ct, id, Request.Headers["X-Onboarding-Token"].ToString());
    [HttpPost("activate")]
    [ProducesResponseType<PatientActivatedResponse>(200)]
    public Task<IActionResult> Activate(ActivatePatientRequest request, CancellationToken ct) => Write(PatientIdentityMutation.Activate,
        new() { InvitationToken = request.InvitationToken, Password = request.Password, ConfirmPassword = request.ConfirmPassword }, 200, ct);
    [HttpPost("activation/resend")]
    [ProducesResponseType<PatientActivationResentResponse>(202)]
    public Task<IActionResult> Resend(ResendPatientActivationRequest request, CancellationToken ct) => Write(PatientIdentityMutation.Resend,
        new() { Email = request.Email, VerificationToken = request.VerificationToken }, 202, ct, request.ClaimRequestId);
    private async Task<IActionResult> Write(PatientIdentityMutation op, PatientIdentityInput input, int status, CancellationToken ct, Guid? id = null, string? token = null)
    {
        var result = await sender.Send(new PatientIdentityCommand(op, input, Request.Headers["Idempotency-Key"].ToString(), token, id), ct);
        if (result.IsFailure) return result.Errors.ToActionProblem(ct);
        if (result.Value is PatientOtpVerificationFailure failed) return new[] { failed.Error }.ToActionProblem(ct);
        return StatusCode(status, result.Value);
    }
    internal static MediaUpload? Upload(IFormFile? f) => f is null ? null : new(f.OpenReadStream(), f.FileName, f.ContentType, f.Length);
    internal static PatientIdentityInput RegistrationInput(RegisterPatientRequest p) => new() { UserName = p.UserName, Email = p.Email, EmailVerificationToken = p.EmailVerificationToken,
        PhoneNumber = p.PhoneNumber, Password = p.Password, ConfirmPassword = p.ConfirmPassword, NameAr = p.NameAr, NameEn = p.NameEn, DateOfBirth = p.DateOfBirth, Gender = p.Gender,
        ProfileImage = Upload(p.ProfileImage), PersonalIdFrontImage = Upload(p.PersonalIdFrontImage), PersonalIdBackImage = Upload(p.PersonalIdBackImage) };
}
public sealed record StartPatientEmailRequest(string Email, PatientOnboardingPurpose Purpose, Guid? PatientReference = null);
public sealed record VerifyPatientEmailRequest(Guid RequestId, string Otp);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ActivatePatientRequest(string InvitationToken, string Password, string ConfirmPassword);
public sealed record ResendPatientActivationRequest(string Email, Guid? ClaimRequestId = null, string? VerificationToken = null);
public class PatientClaimSubmissionRequest
{
    public string EmailVerificationToken { get; set; } = string.Empty;
    public Guid? PatientReference { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? PhoneNumber { get; set; }
    public List<PatientClaimEvidenceType> EvidenceType { get; set; } = [];
    public IFormFile? IdentityFront { get; set; }
    public IFormFile? IdentityBack { get; set; }
    public IFormFile? BirthCertificate { get; set; }
    public IFormFile? GuardianRelationshipDocument { get; set; }
    public Guid? InPersonIdentityAttestationId { get; set; }
    public string? VerificationNote { get; set; }
    public PatientIdentityInput ToInput() => new() { EmailVerificationToken = EmailVerificationToken, PatientReference = PatientReference, NameAr = NameAr, DateOfBirth = DateOfBirth, Gender = Gender,
        PhoneNumber = PhoneNumber, IdentityFront = PatientOnboardingController.Upload(IdentityFront), IdentityBack = PatientOnboardingController.Upload(IdentityBack), BirthCertificate = PatientOnboardingController.Upload(BirthCertificate),
        GuardianRelationshipDocument = PatientOnboardingController.Upload(GuardianRelationshipDocument), InPersonIdentityAttestationId = InPersonIdentityAttestationId, VerificationNote = VerificationNote, EvidenceTypes = EvidenceType.ToArray() };
}
public sealed class PatientClaimResubmissionRequest : PatientClaimSubmissionRequest
{
    public string RowVersion { get; set; } = string.Empty;
    public string? ReasonResponse { get; set; }
    public PatientClaimIdentityCorrections? IdentityProfileCorrections { get; set; }
    public new PatientIdentityInput ToInput() { var p = base.ToInput(); p.RowVersion = RowVersion; p.Reason = ReasonResponse; p.IdentityProfileCorrections = IdentityProfileCorrections; return p; }
}
