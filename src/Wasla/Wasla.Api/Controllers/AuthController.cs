using Asp.Versioning;
using BuildingBlock.Api;
using BuildingBlock.Application.Abstraction.Media;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wasla.Application.Features.Auth;
using Wasla.Application.Features.Registration;
using Wasla.Domain.Common;

namespace Wasla.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken cancellationToken)
        => (await sender.Send(command, cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
        => (await sender.Send(new MeQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordCommand command,
        CancellationToken cancellationToken)
        => (await sender.Send(command, cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("doctors/register")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RegisterDoctor(
        [FromForm] RegisterDoctorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RegisterDoctorCommand(
            request.UserName,
            request.Email,
            request.PhoneNumber,
            request.Password,
            request.ConfirmPassword,
            request.NameAr,
            request.NameEn,
            request.DateOfBirth,
            request.Gender,
            ToUpload(request.ProfileImage),
            ToUpload(request.PersonalIdFrontImage),
            ToUpload(request.PersonalIdBackImage),
            ToUpload(request.SyndicateCardFrontImage),
            ToUpload(request.SyndicateCardBackImage)), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPost("patients/register")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RegisterPatient(
        [FromForm] RegisterPatientRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RegisterPatientCommand(
            request.UserName,
            request.Email,
            request.PhoneNumber,
            request.Password,
            request.ConfirmPassword,
            request.NameAr,
            request.NameEn,
            request.DateOfBirth,
            request.Gender,
            ToUpload(request.ProfileImage),
            ToUpload(request.PersonalIdFrontImage),
            ToUpload(request.PersonalIdBackImage)), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.Errors.ToActionProblem(cancellationToken);
    }

    [HttpPost("forgot-password/request-otp")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset-request")]
    public async Task<IActionResult> RequestOtp(
        RequestPasswordResetOtpCommand command,
        CancellationToken cancellationToken)
        => (await sender.Send(command, cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("forgot-password/verify-otp")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset-verify")]
    public async Task<IActionResult> VerifyOtp(
        VerifyPasswordResetOtpCommand command,
        CancellationToken cancellationToken)
        => (await sender.Send(command, cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("forgot-password/reset")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset-complete")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordCommand command,
        CancellationToken cancellationToken)
        => (await sender.Send(command, cancellationToken)).ToIActionResult(cancellationToken);

    private static MediaUpload? ToUpload(IFormFile? file)
        => file is null
            ? null
            : new MediaUpload(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
}

public sealed class RegisterDoctorRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public IFormFile? ProfileImage { get; set; }
    public IFormFile? PersonalIdFrontImage { get; set; }
    public IFormFile? PersonalIdBackImage { get; set; }
    public IFormFile? SyndicateCardFrontImage { get; set; }
    public IFormFile? SyndicateCardBackImage { get; set; }
}

public sealed class RegisterPatientRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public IFormFile? ProfileImage { get; set; }
    public IFormFile? PersonalIdFrontImage { get; set; }
    public IFormFile? PersonalIdBackImage { get; set; }
}
