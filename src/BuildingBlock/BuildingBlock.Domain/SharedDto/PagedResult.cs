namespace BuildingBlock.Domain.SharedDto
{
    public sealed record PagedResult<T>
    {
        public PagedResult(
            int pageNumber,
            int pageSize,
            long totalItems,
            IEnumerable<T> items)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
            ArgumentOutOfRangeException.ThrowIfNegative(totalItems);

            PageNumber = pageNumber;
            PageSize = pageSize;
            TotalItems = totalItems;
            TotalPages = totalItems == 0
                ? 0
                : (totalItems / pageSize) + (totalItems % pageSize == 0 ? 0 : 1);
            Items = items?.ToArray() ?? throw new ArgumentNullException(nameof(items));
        }

        public int PageNumber { get; }

        public int PageSize { get; }

        public long TotalItems { get; }

        public long TotalPages { get; }

        public IReadOnlyList<T> Items { get; }

        public bool HasPreviousPage => PageNumber > 1;

        public bool HasNextPage => PageNumber < TotalPages;
    }
}
