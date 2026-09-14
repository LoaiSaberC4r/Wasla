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
[assembly: SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Schedule exception is the established operational-domain term.",
    Scope = "type",
    Target = "~T:Wasla.Domain.Practices.DoctorPracticeScheduleException")]
[assembly: SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Assignment permission is the established authorization-domain term.",
    Scope = "type",
    Target = "~T:Wasla.Domain.Practices.ReceptionPracticeAssignmentPermission")]
