using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Naming",
    "CA1707:Identifiers should not contain underscores",
    Justification = "xUnit tests intentionally use underscore-separated names for behavior readability.")]

[assembly: SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "Inline test data arrays keep assertions local and readable.")]

[assembly: SuppressMessage(
    "Performance",
    "CA1859:Use concrete types when possible for improved performance",
    Justification = "Test helpers favor abstraction and clarity over micro-optimizations.")]

[assembly: SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Instance test fixture helpers keep call sites tied to fixture lifetime.")]
