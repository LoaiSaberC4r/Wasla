using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Subscriptions;

public sealed class CommercialProduct : Entity<Guid>
{
    private CommercialProduct() { }
    public CommercialProduct(Guid id, string code, string nameAr, string? nameEn, string? descriptionAr, string? descriptionEn, bool isActive, bool isPubliclySelectable, int displayOrder, DateTime createdOnUtc, DateTime? modifiedOnUtc) : base(id)
    {
        Code = code;
        NameAr = nameAr;
        NameEn = nameEn;
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
        IsActive = isActive;
        IsPubliclySelectable = isPubliclySelectable;
        DisplayOrder = displayOrder;
        CreatedOnUtc = createdOnUtc;
        ModifiedOnUtc = modifiedOnUtc;
    }
    public string Code { get; private set; } = "";
    public string NameAr { get; private set; } = "";
    public string? NameEn { get; private set; }
    public string? DescriptionAr { get; private set; }
    public string? DescriptionEn { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsPubliclySelectable { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ModifiedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
