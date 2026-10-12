using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Wasla.Application.Features.Clinical;

namespace Wasla.Api.OpenApi;

public sealed class MedicalRecordSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema definition) return;
        if (context.Type != typeof(MedicalRecordItemDetailsResponse) && typeof(MedicalRecordItemDetailsResponse).IsAssignableFrom(context.Type))
            _ = context.SchemaGenerator.GenerateSchema(typeof(MedicalRecordItemDetailsResponse), context.SchemaRepository);
        if (context.Type == typeof(TimelineEntry))
            definition.OneOf = [context.SchemaGenerator.GenerateSchema(typeof(ClinicalTimelineEntry), context.SchemaRepository),
                context.SchemaGenerator.GenerateSchema(typeof(HiddenTimelineEntry), context.SchemaRepository)];
        if (typeof(MedicalRecordItemDetailsResponse).IsAssignableFrom(context.Type) && definition.Properties is not null)
            definition.Properties["safeContent"] = new OpenApiSchema
            {
                OneOf = [context.SchemaGenerator.GenerateSchema(typeof(SafePrescriptionContent), context.SchemaRepository),
                    context.SchemaGenerator.GenerateSchema(typeof(SafeOrderContent), context.SchemaRepository),
                    context.SchemaGenerator.GenerateSchema(typeof(SafeResultContent), context.SchemaRepository)]
            };
    }
}
