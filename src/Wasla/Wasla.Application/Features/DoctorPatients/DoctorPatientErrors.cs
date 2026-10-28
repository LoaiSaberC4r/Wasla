using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Application.Features.DoctorPatients;

public static class DoctorPatientErrors
{
    public static Error PdfUnavailable(string code) => Error.Infra(code, ErrorMessage.GetString("PrescriptionPdfUnavailable"));
}
