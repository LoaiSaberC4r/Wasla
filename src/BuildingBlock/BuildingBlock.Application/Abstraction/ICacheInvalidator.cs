namespace BuildingBlock.Application.Abstraction
{
    public interface ICacheInvalidator
    {
        /// <summary>
        /// Cache tags to invalidate after a successful command.
        /// </summary>
        IEnumerable<string> Tags { get; }
    }
}
