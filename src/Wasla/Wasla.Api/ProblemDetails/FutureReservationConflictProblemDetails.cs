using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlock.Api;
using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Mvc;

namespace Wasla.Api.ProblemDetails;

public static class FutureReservationConflictProblemDetails
{
    private const string ErrorCode = "DoctorPractice.FutureReservationsExist";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IActionResult ToWaslaActionProblem(
        this IEnumerable<Error> errors,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var materialized = errors.ToArray();
        var conflict = materialized.FirstOrDefault(error => error.Code == ErrorCode);
        if (conflict is null || string.IsNullOrWhiteSpace(conflict.Details))
        {
            return materialized.ToActionProblem(cancellationToken);
        }

        try
        {
            var source = JsonSerializer.Deserialize<SourceConflict>(conflict.Details, JsonOptions);
            if (source is null)
            {
                return materialized.ToActionProblem(cancellationToken);
            }

            var affected = source.Reservations.Select(item => new AffectedReservation(
                item.ReservationId,
                item.ReservationReference,
                item.PatientId,
                item.NameAr,
                item.NameEn,
                item.BusinessDate,
                item.ScheduledTime)).ToArray();
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = conflict.Message,
                Type = "https://httpstatuses.com/409"
            };
            problem.Extensions["code"] = ErrorCode;
            problem.Extensions["affectedReservationsCount"] = source.AffectedCount;
            problem.Extensions["affectedReservations"] = affected;
            return new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
        }
        catch (JsonException)
        {
            return materialized.ToActionProblem(cancellationToken);
        }
    }

    private sealed record SourceConflict(int AffectedCount, IReadOnlyList<SourceReservation> Reservations);
    private sealed record SourceReservation(
        Guid ReservationId,
        string ReservationReference,
        Guid PatientId,
        string NameAr,
        string? NameEn,
        DateOnly BusinessDate,
        TimeOnly ScheduledTime);
    private sealed record AffectedReservation(
        [property: JsonPropertyName("reservationId")] Guid ReservationId,
        [property: JsonPropertyName("reservationReference")] string ReservationReference,
        [property: JsonPropertyName("patientId")] Guid PatientId,
        [property: JsonPropertyName("patientNameAr")] string PatientNameAr,
        [property: JsonPropertyName("patientNameEn")] string? PatientNameEn,
        [property: JsonPropertyName("businessDate")] DateOnly BusinessDate,
        [property: JsonPropertyName("scheduledTime")] TimeOnly ScheduledTime);
}
