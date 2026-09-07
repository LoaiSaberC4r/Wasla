namespace BuildingBlock.Domain.Primitive
{
    public interface ISoftDeleteEntity
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedOnUtc { get; set; }
        DateTime? RestoredOnUtc { get; set; }
    }
}
