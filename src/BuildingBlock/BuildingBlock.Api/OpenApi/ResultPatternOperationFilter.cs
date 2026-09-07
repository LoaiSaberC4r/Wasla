using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Globalization;

namespace BuildingBlock.Api.OpenApi
{
    internal sealed class ResultPatternOperationFilter : IOperationFilter
    {
        private static readonly (int Status, string Description)[] ProblemResponses =
        {
            (401, "Unauthorized"),
            (403, "Forbidden"),
            (404, "Not Found"),
            (409, "Conflict"),
            (422, "Unprocessable Entity"),
            (429, "Too Many Requests"),
            (500, "Internal Server Error"),
            (503, "Service Unavailable")
        };

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            operation.Responses ??= new OpenApiResponses();

            foreach (var (status, description) in ProblemResponses)
            {
                AddProblemIfMissing(operation, context, status, description);
            }

            var successType = TryGetSuccessTypeFromAction(context);
            if (successType is not null)
            {
                if (operation.Responses.Keys.Any(key => key is "201" or "202" or "204"))
                {
                    return;
                }

                if (successType != typeof(void))
                {
                    operation.Responses["200"] = new OpenApiResponse
                    {
                        Description = "OK",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Schema = context.SchemaGenerator.GenerateSchema(successType, context.SchemaRepository)
                            }
                        }
                    };
                }
                else
                {
                    operation.Responses.Remove("200");
                    operation.Responses["204"] = new OpenApiResponse { Description = "No Content" };
                }

                return;
            }

            if (HasExplicitSuccess(operation))
            {
                return;
            }

            operation.Responses["204"] = new OpenApiResponse { Description = "No Content" };
        }

        private static bool HasExplicitSuccess(OpenApiOperation operation)
            => operation.Responses?.Keys.Any(key => key is "200" or "201" or "202" or "204") == true;

        private static void AddProblemIfMissing(OpenApiOperation operation, OperationFilterContext context, int status, string description)
        {
            operation.Responses ??= new OpenApiResponses();
            var key = status.ToString(CultureInfo.InvariantCulture);
            if (operation.Responses.ContainsKey(key))
            {
                return;
            }

            operation.Responses[key] = new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new OpenApiMediaType
                    {
                        Schema = CreateProblemDetailsSchema()
                    }
                }
            };
        }

        private static OpenApiSchema CreateProblemDetailsSchema()
        {
            var errorSchema = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["code"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["message"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["type"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["details"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["source"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["retryAfter"] = new OpenApiSchema { Type = JsonSchemaType.Number }
                }
            };

            return new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["type"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                    ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["instance"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["correlationId"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["errors"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Array,
                        Items = errorSchema
                    }
                }
            };
        }

        private static Type? TryGetSuccessTypeFromAction(OperationFilterContext context)
        {
            var returnType = UnwrapAsyncType(context.MethodInfo.ReturnType);
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                return returnType.GetGenericArguments()[0];
            }

            if (returnType == typeof(Result))
            {
                return typeof(void);
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ActionResult<>))
            {
                return returnType.GetGenericArguments()[0];
            }

            foreach (var parameterType in context.MethodInfo.GetParameters().Select(parameter => parameter.ParameterType))
            {
                if (TryGetCqrsResponse(parameterType, typeof(IQuery<>), out var queryResponse) ||
                    TryGetCqrsResponse(parameterType, typeof(ICommand<>), out queryResponse))
                {
                    return queryResponse;
                }

                if (parameterType.GetInterfaces().Any(type => type == typeof(ICommand)))
                {
                    return typeof(void);
                }
            }

            return null;
        }

        private static Type UnwrapAsyncType(Type returnType)
        {
            if (!returnType.IsGenericType)
            {
                return returnType;
            }

            var definition = returnType.GetGenericTypeDefinition();
            return definition == typeof(Task<>) || definition == typeof(ValueTask<>)
                ? returnType.GetGenericArguments()[0]
                : returnType;
        }

        private static bool TryGetCqrsResponse(Type candidate, Type openContract, out Type? responseType)
        {
            responseType = null;

            foreach (var contract in candidate.GetInterfaces())
            {
                if (contract.IsGenericType && contract.GetGenericTypeDefinition() == openContract)
                {
                    responseType = contract.GetGenericArguments()[0];
                    return true;
                }
            }

            return false;
        }
    }
}
