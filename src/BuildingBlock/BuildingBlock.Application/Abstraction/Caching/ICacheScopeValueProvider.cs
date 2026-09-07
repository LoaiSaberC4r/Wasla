using BuildingBlock.Application.Abstraction.Security;

namespace BuildingBlock.Application.Abstraction.Caching
{
    public interface ICacheScopeValueProvider
    {
        bool TryGetScopeValue(CacheScope scope, out string value);
    }

    public sealed class DefaultCacheScopeValueProvider : ICacheScopeValueProvider
    {
        private readonly ICurrentUser _currentUser;

        public DefaultCacheScopeValueProvider(ICurrentUser currentUser)
        {
            _currentUser = currentUser;
        }

        public bool TryGetScopeValue(CacheScope scope, out string value)
        {
            value = string.Empty;

            switch (scope)
            {
                case CacheScope.Global:
                    value = "global";
                    return true;

                case CacheScope.User when _currentUser.UserId is { } userId:
                    value = $"user:{userId:N}";
                    return true;

                default:
                    return false;
            }
        }
    }
}
