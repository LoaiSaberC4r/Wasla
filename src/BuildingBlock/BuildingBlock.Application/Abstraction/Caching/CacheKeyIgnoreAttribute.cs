namespace BuildingBlock.Application.Abstraction.Caching
{
    /// <summary>
    /// Excludes a property from cache-key identity because it does not influence the response.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class CacheKeyIgnoreAttribute : Attribute
    {
    }
}
