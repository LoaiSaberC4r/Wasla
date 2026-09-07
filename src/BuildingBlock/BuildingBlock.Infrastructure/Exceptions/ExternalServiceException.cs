using BuildingBlock.Domain.Results;

namespace BuildingBlock.Infrastructure.Exceptions
{
    public abstract class ExternalServiceException : Exception
    {
        protected ExternalServiceException(Error error, Exception? innerException = null)
            : base(error.Message, innerException)
        {
            Error = error;
        }

        public Error Error { get; }
    }

    public sealed class MediaServiceException : ExternalServiceException
    {
        public MediaServiceException(Error error, Exception? innerException = null)
            : base(error, innerException)
        {
        }
    }

    public sealed class EmailServiceException : ExternalServiceException
    {
        public EmailServiceException(Error error, Exception? innerException = null)
            : base(error, innerException)
        {
        }
    }

    public sealed class QrCodeServiceException : ExternalServiceException
    {
        public QrCodeServiceException(Error error, Exception? innerException = null)
            : base(error, innerException)
        {
        }
    }
}
