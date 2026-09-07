namespace BuildingBlock.Application.Abstraction.Security
{
    public sealed class TokenInfoDto
    {
        public Guid? UserId { get; set; }

        public string? UserName { get; set; }

        public string? Email { get; set; }

        public string[] Roles { get; set; } = Array.Empty<string>();

        public string[] Permissions { get; set; } = Array.Empty<string>();

        public string? Issuer { get; set; }

        public string? Audience { get; set; }

        public DateTime? IssuedAtUtc { get; set; }

        public DateTime? ExpiresAtUtc { get; set; }

        public string? Subject { get; set; }

        public string? JwtId { get; set; }
    }
}
