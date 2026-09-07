using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Naming",
    "CA1715:Identifiers should have correct prefix",
    Justification = "Persistence marker names are part of the approved application architecture.",
    Scope = "type",
    Target = "~T:Wasla.Application.Persistence.WaslaReadPersistence")]
[assembly: SuppressMessage(
    "Naming",
    "CA1715:Identifiers should have correct prefix",
    Justification = "Persistence marker names are part of the approved application architecture.",
    Scope = "type",
    Target = "~T:Wasla.Application.Persistence.WaslaWritePersistence")]
