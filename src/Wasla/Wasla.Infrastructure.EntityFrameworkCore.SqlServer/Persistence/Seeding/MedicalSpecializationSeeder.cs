using Microsoft.EntityFrameworkCore;
using Wasla.Domain.ReferenceData;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed record MedicalSpecializationSeed(Guid Id, int SortOrder, string NameAr, string NameEn);

internal static class MedicalSpecializationSeedCatalog
{
    public static readonly IReadOnlyList<MedicalSpecializationSeed> All =
    [
        new(Guid.Parse("61bc8e48-ddee-587c-a1f7-206a619842ec"), 10, "الباطنة العامة", "Internal Medicine"),
        new(Guid.Parse("fd278b08-69f9-523a-ab4d-dc24b4d84a4d"), 20, "طب الأطفال", "Pediatrics"),
        new(Guid.Parse("354c1d7b-c47f-5211-8baf-59731eb66dfb"), 30, "أمراض القلب", "Cardiology"),
        new(Guid.Parse("89b1b1e2-e425-5cfe-be3d-a0dc16f1dd35"), 40, "الأمراض الجلدية", "Dermatology"),
        new(Guid.Parse("9656afee-1ea1-5cdf-96d2-91acae8a025e"), 50, "جراحة العظام", "Orthopedic Surgery"),
        new(Guid.Parse("c8c2d98e-3d04-5b69-a8b2-b8662e666c59"), 60, "النساء والتوليد", "Obstetrics and Gynecology"),
        new(Guid.Parse("9dae02e4-6ee2-593c-86e2-d9386b8107d3"), 70, "الأنف والأذن والحنجرة", "Otolaryngology (ENT)"),
        new(Guid.Parse("e9f1fe67-34c8-52fd-9f7f-61d83d47dc92"), 80, "طب وجراحة العيون", "Ophthalmology"),
        new(Guid.Parse("67a896cb-9abe-5653-91b6-f2108b6a2a63"), 90, "المخ والأعصاب", "Neurology"),
        new(Guid.Parse("b25d0ebe-e7ba-5006-92a2-0dd862995da9"), 100, "الطب النفسي", "Psychiatry"),
        new(Guid.Parse("86840cc4-f4c1-5ca7-90cd-11b35b5f8dd7"), 110, "الجراحة العامة", "General Surgery"),
        new(Guid.Parse("50a703e2-3837-5f35-bbbe-3796b52ca3f0"), 120, "جراحة القلب والصدر", "Cardiothoracic Surgery"),
        new(Guid.Parse("eae76f89-4907-5f77-a38d-e8852f586b33"), 130, "جراحة المخ والأعصاب", "Neurosurgery"),
        new(Guid.Parse("5b8563ff-ea48-5008-928d-a98c908d0580"), 140, "جراحة الأوعية الدموية", "Vascular Surgery"),
        new(Guid.Parse("422aeeb1-8229-5731-8bc2-a81c9cce03ec"), 150, "جراحة التجميل", "Plastic Surgery"),
        new(Guid.Parse("4fc21b1b-6135-5a5a-8242-673a8575e148"), 160, "جراحة المسالك البولية", "Urology"),
        new(Guid.Parse("37ed49b9-62aa-5c48-ace4-447cea3e2bf2"), 170, "أمراض الكلى", "Nephrology"),
        new(Guid.Parse("a3350f80-61fd-526b-a2bd-753a95b1c251"), 180, "أمراض الجهاز الهضمي والكبد", "Gastroenterology and Hepatology"),
        new(Guid.Parse("d88f4644-345d-5a9d-a7aa-c7bf0c6f0928"), 190, "أمراض الصدر والجهاز التنفسي", "Pulmonology"),
        new(Guid.Parse("41de3579-19c6-565f-b17f-dc311c264801"), 200, "أمراض الدم", "Hematology"),
        new(Guid.Parse("21576d82-0125-5f8d-a0d3-195eec0d9e44"), 210, "الأورام", "Oncology"),
        new(Guid.Parse("60684c08-f5a9-5cc3-830e-e1ee7a3aef67"), 220, "الروماتيزم والمناعة", "Rheumatology and Immunology"),
        new(Guid.Parse("070e0e8a-9599-5b29-9cd2-2aa84743131c"), 230, "الغدد الصماء والسكري", "Endocrinology and Diabetes"),
        new(Guid.Parse("4b1922b9-ccb6-5f73-a072-49c4127442a3"), 240, "الأمراض المعدية", "Infectious Diseases"),
        new(Guid.Parse("50615eb8-c5ab-552a-bb21-170e046ba27f"), 250, "طب الأسرة", "Family Medicine"),
        new(Guid.Parse("39e10414-5a89-5cb4-b1fe-221c040d670b"), 260, "الطب العام", "General Practice"),
        new(Guid.Parse("a244a168-8432-53a2-bf6c-24c0087cea19"), 270, "طب الطوارئ", "Emergency Medicine"),
        new(Guid.Parse("c0ff09e0-0133-51c7-adc1-32bc5447a21f"), 280, "التخدير", "Anesthesiology"),
        new(Guid.Parse("9ae747a5-f6bf-5724-919d-ef1afb6a9dff"), 290, "العناية المركزة", "Critical Care Medicine"),
        new(Guid.Parse("68d285ba-3534-52a1-b088-cf20a388f74b"), 300, "طب الألم", "Pain Medicine"),
        new(Guid.Parse("1d80b533-bee7-5399-a26a-a7fb70f05383"), 310, "طب المسنين", "Geriatric Medicine"),
        new(Guid.Parse("32a96203-06de-5bbe-affc-1b84cb54e729"), 320, "الطب الطبيعي والتأهيل", "Physical Medicine and Rehabilitation"),
        new(Guid.Parse("8e7a6963-8c58-5ca4-949b-049965b76bf6"), 330, "الطب الرياضي", "Sports Medicine"),
        new(Guid.Parse("e284a2c7-3110-5136-a121-84fb580475a3"), 340, "الحساسية والمناعة", "Allergy and Immunology"),
        new(Guid.Parse("158fa1a7-85bc-5e7d-b0a7-dbfceb47a6fa"), 350, "أمراض الذكورة والعقم", "Andrology and Male Infertility"),
        new(Guid.Parse("80a4373f-79e8-55f1-b05e-ca542a7b63e0"), 360, "الأشعة التشخيصية", "Diagnostic Radiology"),
        new(Guid.Parse("b264ba8e-4111-58cc-93de-f22a960a867a"), 370, "الأشعة التداخلية", "Interventional Radiology"),
        new(Guid.Parse("d2904c93-e214-5d0e-8859-f54989531f33"), 380, "الطب النووي", "Nuclear Medicine"),
        new(Guid.Parse("935526c5-b4d5-5461-bd61-6559199b4e6c"), 390, "الباثولوجيا الإكلينيكية", "Clinical Pathology"),
        new(Guid.Parse("a82a4493-7a1b-55a5-97f0-1208f2e19d11"), 400, "الباثولوجيا التشريحية", "Anatomic Pathology"),
        new(Guid.Parse("e07c926d-a82e-5d15-99b4-2f2cf82a7dac"), 410, "التغذية العلاجية", "Clinical Nutrition"),
        new(Guid.Parse("08f9f83f-1e43-5fce-b56e-9af6dc83e64e"), 420, "الصحة العامة وطب المجتمع", "Public Health and Community Medicine"),
        new(Guid.Parse("641b0c3a-b1fc-5aa1-b00d-74664a776a7c"), 430, "طب العمل", "Occupational Medicine"),
        new(Guid.Parse("af4f1fa4-d367-5f57-a774-ab80c70c091a"), 440, "الطب الشرعي والسموم", "Forensic Medicine and Toxicology"),
        new(Guid.Parse("c4853a1f-7e0d-5cd8-907d-ac60afcc75be"), 450, "حديثي الولادة", "Neonatology"),
        new(Guid.Parse("204aa365-40f4-5555-93e5-17717d1e7d6c"), 460, "قلب الأطفال", "Pediatric Cardiology"),
        new(Guid.Parse("368c87f4-a3fc-5157-9026-135c468b45ad"), 470, "جراحة الأطفال", "Pediatric Surgery")
    ];
}

internal sealed class MedicalSpecializationSeeder(WaslaDbContext dbContext)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var seed in MedicalSpecializationSeedCatalog.All)
        {
            if (await dbContext.MedicalSpecializations.IgnoreQueryFilters()
                    .AnyAsync(item => item.Id == seed.Id, cancellationToken))
            {
                continue;
            }

            var conflict = await dbContext.MedicalSpecializations.IgnoreQueryFilters()
                .AnyAsync(item => item.NameAr == seed.NameAr || item.NameEn == seed.NameEn, cancellationToken);
            if (conflict)
            {
                throw new InvalidOperationException(
                    $"Medical specialization seed '{seed.Id}' conflicts with an existing specialization name.");
            }

            var created = MedicalSpecialization.Create(
                seed.Id,
                seed.NameAr,
                seed.NameEn,
                null,
                null,
                seed.SortOrder,
                SystemSeedIds.RootApplicationUserId);
            if (created.IsFailure)
            {
                throw new InvalidOperationException($"Medical specialization seed '{seed.Id}' is invalid.");
            }

            dbContext.MedicalSpecializations.Add(created.Value);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
