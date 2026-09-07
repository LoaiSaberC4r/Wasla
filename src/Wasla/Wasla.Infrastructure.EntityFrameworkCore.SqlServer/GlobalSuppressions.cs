using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Performance",
    "CA1861:Avoid constant arrays as arguments",
    Justification = "EF Core generates migration index column arrays.",
    Scope = "member",
    Target = "~M:Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Migrations.InitialTechnicalFoundation.Up(Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder)")]
