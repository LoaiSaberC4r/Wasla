using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Security;

public static class SecurityErrors
{
    public static Error RootRequired => Error.Security(
        "SuperAdmin.RootRequired",
        ErrorMessage.RootSuperAdminRequired);

    public static Error RootProtected => Error.Conflict(
        "SuperAdmin.RootProtected",
        ErrorMessage.RootSuperAdminProtected);
}
