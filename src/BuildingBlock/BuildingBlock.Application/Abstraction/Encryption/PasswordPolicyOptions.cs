namespace BuildingBlock.Application.Abstraction.Encryption
{
    public sealed class PasswordPolicyOptions
    {
        public const string SectionName = "PasswordPolicy";

        public int RequiredLength { get; set; } = 8;

        public int MaximumLength { get; set; } = 256;

        public bool RequireDigit { get; set; } = true;

        public bool RequireUppercase { get; set; } = true;

        public bool RequireLowercase { get; set; } = true;

        public bool RequireNonAlphanumeric { get; set; }
    }
}
