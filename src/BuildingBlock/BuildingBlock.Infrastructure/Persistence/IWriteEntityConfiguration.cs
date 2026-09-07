using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlock.Infrastructure.Persistence
{
    public interface IWriteEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
        where TEntity : class
    {
        void IEntityTypeConfiguration<TEntity>.Configure(EntityTypeBuilder<TEntity> builder)
            => ConfigureAggregate(builder);

        void ConfigureAggregate(EntityTypeBuilder<TEntity> builder);
    }
}
