using BuildingBlock.Domain.Primitive;

namespace Wasla.Tests.Architecture;

public sealed class Phase11TicketArchitectureTests
{
    [Fact]
    public void Phase11_ticket_application_does_not_depend_on_frozen_data_store()
    {
        var ticketTypes = Wasla.Application.AssemblyReference.Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "Wasla.Application.Features.Tickets",
                StringComparison.Ordinal) == true)
            .ToArray();

        Assert.NotEmpty(ticketTypes);
        Assert.DoesNotContain(ticketTypes, type =>
            type.GetConstructors(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => parameter.ParameterType ==
                    typeof(Wasla.Application.Persistence.IWaslaDataStore)));
    }

    [Fact]
    public void Phase11_application_has_no_dbcontext_dependency()
    {
        var ticketTypes = Wasla.Application.AssemblyReference.Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "Wasla.Application.Features.Tickets",
                StringComparison.Ordinal) == true);

        Assert.DoesNotContain(ticketTypes, type =>
            type.GetFields(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)
                .Any(field => field.FieldType.Name.Contains("DbContext", StringComparison.Ordinal)));
    }

    [Fact]
    public void Ticket_domain_has_no_ef_or_soft_delete_behavior()
    {
        var domainReferences = Wasla.Domain.AssemblyReference.Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Select(name => name!);

        Assert.DoesNotContain(domainReferences, name =>
            name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.False(typeof(ISoftDeleteEntity).IsAssignableFrom(typeof(Wasla.Domain.Tickets.Ticket)));
        Assert.False(typeof(ISoftDeleteEntity).IsAssignableFrom(typeof(Wasla.Domain.Tickets.TicketHistory)));
        Assert.False(typeof(ISoftDeleteEntity).IsAssignableFrom(typeof(Wasla.Domain.Tickets.TicketCallAttempt)));
        Assert.False(typeof(ISoftDeleteEntity).IsAssignableFrom(typeof(Wasla.Domain.Payments.Payment)));
    }
}
