namespace BuildingBlock.Domain.Primitive
{
    public interface IAuditableEntity
    {
        DateTime CreatedOnUtc { get; set; }
        DateTime? ModifiedOnUtc { get; set; }
    }
}
