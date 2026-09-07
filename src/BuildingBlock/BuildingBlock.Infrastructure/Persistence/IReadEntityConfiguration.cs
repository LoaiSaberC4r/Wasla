using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlock.Infrastructure.Persistence
{
    public interface IReadEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
        where TEntity : class
    {
        void IEntityTypeConfiguration<TEntity>.Configure(EntityTypeBuilder<TEntity> builder)
            => ConfigureReadModel(builder);

        void ConfigureReadModel(EntityTypeBuilder<TEntity> builder);
    }
}
