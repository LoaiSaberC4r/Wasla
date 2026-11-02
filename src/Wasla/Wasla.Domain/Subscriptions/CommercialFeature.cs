using BuildingBlock.Domain.EntitiesHelper;

namespace Wasla.Domain.Subscriptions;

public sealed class CommercialFeature : Entity<Guid>
{
    private CommercialFeature() { }
    public CommercialFeature(Guid id, string code, string nameAr, string? nameEn, string? descriptionAr, string? descriptionEn, string groupCode, FeatureImplementationStatus implementationStatus, FeatureReleaseReadiness releaseReadiness, bool isActive, bool isPubliclyVisible, int displayOrder, DateTime createdOnUtc, DateTime? modifiedOnUtc) : base(id)
    {
        Code = code;
        NameAr = nameAr;
        NameEn = nameEn;
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
        GroupCode = groupCode;
        ImplementationStatus = implementationStatus;
        ReleaseReadiness = releaseReadiness;
        IsActive = isActive;
        IsPubliclyVisible = isPubliclyVisible;
        DisplayOrder = displayOrder;
        CreatedOnUtc = createdOnUtc;
        ModifiedOnUtc = modifiedOnUtc;
    }
    public string Code { get; private set; } = "";
    public string NameAr { get; private set; } = "";
    public string? NameEn { get; private set; }
    public string? DescriptionAr { get; private set; }
    public string? DescriptionEn { get; private set; }
    public string GroupCode { get; private set; } = "";
    public FeatureImplementationStatus ImplementationStatus { get; private set; }
    public FeatureReleaseReadiness ReleaseReadiness { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsPubliclyVisible { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ModifiedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
