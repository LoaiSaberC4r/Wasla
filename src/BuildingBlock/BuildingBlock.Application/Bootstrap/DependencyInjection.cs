using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Behaviors;
using BuildingBlock.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlock.Application.Bootstrap
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddBuildingBlockApplicationBehaviors(this IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionToErrorMapper, DefaultExceptionToErrorMapper>());
            services.TryAddScoped(typeof(IApplicationTransactionManager<>), typeof(UnavailableApplicationTransactionManager<>));

            // Registration order is pipeline order. Tracing stays outermost so every later behavior shares one activity.
            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(TracingBehavior<,>)));
            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>)));
            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(ExceptionMappingBehavior<,>)));

            // Validation runs before transactions so invalid commands do not open database transactions.
            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)));

            // Command invalidation wraps transactions so cache tags are invalidated after a successful commit.
            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(CommandCacheInvalidationBehavior<,>)));
            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>)));

            // Query cache stays innermost for cacheable reads and is bypassed by non-query commands.
            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(QueryCacheBehavior<,>)));

            return services;
        }
    }
}
