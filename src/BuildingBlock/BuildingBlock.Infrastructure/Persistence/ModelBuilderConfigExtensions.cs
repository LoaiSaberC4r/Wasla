using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace BuildingBlock.Infrastructure.Persistence
{
    public static class ModelBuilderConfigExtensions
    {
        public static void ApplyWriteConfigurations(this ModelBuilder modelBuilder, Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            ArgumentNullException.ThrowIfNull(assembly);

            modelBuilder.ApplyConfigurationsFromAssembly(
                assembly,
                type => ImplementsConfigurationContract(type, typeof(IWriteEntityConfiguration<>)));
        }

        public static void ApplyReadConfigurations(this ModelBuilder modelBuilder, Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            ArgumentNullException.ThrowIfNull(assembly);

            modelBuilder.ApplyConfigurationsFromAssembly(
                assembly,
                type => ImplementsConfigurationContract(type, typeof(IReadEntityConfiguration<>)));
        }

        private static bool ImplementsConfigurationContract(Type type, Type openContract)
            => !type.IsAbstract &&
               !type.ContainsGenericParameters &&
               type.GetInterfaces().Any(interfaceType =>
                   interfaceType.IsGenericType &&
                   interfaceType.GetGenericTypeDefinition() == openContract);
    }
}
