namespace BuildingBlock.Infrastructure.Exceptions
{
    public static class ExternalServiceErrorCodes
    {
        public static class Media
        {
            public const string InvalidFile = "Media.InvalidFile";
            public const string FileTooLarge = "Media.FileTooLarge";
            public const string UnsupportedType = "Media.UnsupportedType";
            public const string SignatureMismatch = "Media.SignatureMismatch";
            public const string MalwareDetected = "Media.MalwareDetected";
            public const string StorageUnavailable = "Media.StorageUnavailable";
        }

        public static class Email
        {
            public const string InvalidMessage = "Email.InvalidMessage";
            public const string ConnectionFailed = "Email.ConnectionFailed";
            public const string AuthenticationFailed = "Email.AuthenticationFailed";
            public const string SendFailed = "Email.SendFailed";
            public const string Timeout = "Email.Timeout";
        }

        public static class QrCode
        {
            public const string InvalidPayload = "QrCode.InvalidPayload";
            public const string PayloadTooLarge = "QrCode.PayloadTooLarge";
            public const string GenerationFailed = "QrCode.GenerationFailed";
        }
    }
}
