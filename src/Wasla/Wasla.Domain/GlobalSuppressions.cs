using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Permission and RolePermission are canonical security-domain terms.",
    Scope = "type",
    Target = "~T:Wasla.Domain.Security.Permission")]
[assembly: SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Permission and RolePermission are canonical security-domain terms.",
    Scope = "type",
    Target = "~T:Wasla.Domain.Security.RolePermission")]
