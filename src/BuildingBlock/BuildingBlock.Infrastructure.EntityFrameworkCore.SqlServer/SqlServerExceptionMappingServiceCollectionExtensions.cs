using BuildingBlock.Application.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer
{
    public static class SqlServerExceptionMappingServiceCollectionExtensions
    {
        public static IServiceCollection AddBuildingBlockSqlServerExceptionMapping(this IServiceCollection services)
        {
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IExceptionToErrorMapper, SqlServerExceptionToErrorMapper>());
            return services;
        }
    }
}
