using BuildingBlock.Domain.Primitive;

namespace BuildingBlock.Domain.EntitiesHelper
{
    public abstract class Entity<TKey>
    {
        protected Entity(TKey id) => Id = id;

        protected Entity()
        { }

        public TKey Id { get; protected init; } = default!;
    }

    public abstract class AuditableEntity<TKey> : Entity<TKey>, IAuditableEntity
    {
        protected AuditableEntity(TKey id)
            : base(id)
        { }

        protected AuditableEntity()
        { }

        public DateTime CreatedOnUtc { get; set; }

        public DateTime? ModifiedOnUtc { get; set; }
    }
}
