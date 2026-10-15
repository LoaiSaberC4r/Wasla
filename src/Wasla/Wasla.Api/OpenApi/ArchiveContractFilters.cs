using System.Reflection;
using System.Text.Json;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Wasla.Application.Features.Archives;
using Wasla.Application.Features.Reservations;
using Wasla.Application.Features.Tickets.Common;

namespace Wasla.Api.OpenApi;

public sealed class ArchiveSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema definition || definition.Properties == null ||
            context.Type.Namespace != typeof(ArchiveHealth).Namespace && context.Type != typeof(TicketDetailsResponse) && context.Type != typeof(ReservationDetailsResponse) &&
            context.Type != typeof(TicketOperationalCapabilities) && context.Type != typeof(TicketTimelineItem) &&
            context.Type != typeof(ReservationCapabilitiesResponse) && context.Type != typeof(ReservationTimelineItemResponse)) return;
        var nullability = new NullabilityInfoContext();
        foreach (var property in context.Type.GetProperties())
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            if (!definition.Properties.TryGetValue(name, out var value)) continue;
            var nullable = Nullable.GetUnderlyingType(property.PropertyType) != null ||
                !property.PropertyType.IsValueType && nullability.Create(property).ReadState != NullabilityState.NotNull;
            if (!nullable && context.Type != typeof(ArchiveFilter) && context.Type != typeof(AuditFilter) && context.Type != typeof(ArchiveOperationsFilter))
            { definition.Required ??= new HashSet<string>(); definition.Required.Add(name); }
            if (nullable && value is OpenApiSchema field && field.Type != null) field.Type |= JsonSchemaType.Null;
            else if (nullable && value is OpenApiSchemaReference)
                // OAS 3.0 ignores nullable siblings of a bare $ref. A null-only
                // alternative preserves the referenced schema and actual null payload.
                definition.Properties[name] = new OpenApiSchema
                {
                    AnyOf = [value, new OpenApiSchema { Type = JsonSchemaType.Object | JsonSchemaType.Null, Enum = [null!] }]
                };
        }
    }
}

public sealed class ArchiveOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? string.Empty;
        var archive = context.MethodInfo.DeclaringType?.Name == "Phase18PartTwoController";
        var detail = context.ApiDescription.HttpMethod == "GET" &&
            (path.EndsWith("/{ticketId}", StringComparison.Ordinal) || path.EndsWith("/{reservationId}", StringComparison.Ordinal));
        if (!archive && !detail) return;
        if (operation.Responses == null) return;
        if (operation.Responses.TryGetValue("200", out var success) && success is OpenApiResponse ok)
        {
            ok.Headers ??= new Dictionary<string, IOpenApiHeader>();
            ok.Headers["Cache-Control"] = new OpenApiHeader { Description = "no-store; private role-scoped data", Schema = new OpenApiSchema { Type=JsonSchemaType.String } };
            if (context.MethodInfo.DeclaringType?.Name == "AdministrativeReservationsController")
                ok.Content = new Dictionary<string,OpenApiMediaType> { ["application/json"] = new() { Schema = new OpenApiSchema { OneOf = [
                    context.SchemaGenerator.GenerateSchema(typeof(ReservationDetailsResponse), context.SchemaRepository),
                    context.SchemaGenerator.GenerateSchema(typeof(PlatformReservationMetadata), context.SchemaRepository)] } } };
        }
        if (operation.Responses.TryGetValue("429", out var rejected) && rejected is OpenApiResponse limited)
        {
            limited.Headers ??= new Dictionary<string, IOpenApiHeader>();
            limited.Headers["Retry-After"] = new OpenApiHeader { Description = "Seconds until this actor's request window resets.", Schema = new OpenApiSchema { Type=JsonSchemaType.Integer } };
        }
    }
}
