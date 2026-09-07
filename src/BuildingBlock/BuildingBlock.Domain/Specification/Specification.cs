using BuildingBlock.Domain.Enums;
using System.Linq.Expressions;

namespace BuildingBlock.Domain.Specification
{
    public static class SpecificationDefaults
    {
        public const int DefaultMaximumPageSize = 200;
    }

    public interface IOrderExpression<TEntity>
    {
        LambdaExpression KeySelector { get; }

        bool Descending { get; }

        IOrderedQueryable<TEntity> ApplyInitial(IQueryable<TEntity> query);

        IOrderedQueryable<TEntity> ApplyThen(IOrderedQueryable<TEntity> query);
    }

    internal sealed class OrderExpression<TEntity, TKey> : IOrderExpression<TEntity>
    {
        private readonly Expression<Func<TEntity, TKey>> _keySelector;

        public OrderExpression(Expression<Func<TEntity, TKey>> keySelector, bool descending)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            Descending = descending;
        }

        public LambdaExpression KeySelector => _keySelector;

        public bool Descending { get; }

        public IOrderedQueryable<TEntity> ApplyInitial(IQueryable<TEntity> query)
            => Descending
                ? query.OrderByDescending(_keySelector)
                : query.OrderBy(_keySelector);

        public IOrderedQueryable<TEntity> ApplyThen(IOrderedQueryable<TEntity> query)
            => Descending
                ? query.ThenByDescending(_keySelector)
                : query.ThenBy(_keySelector);
    }

    public abstract class Specification<TEntity> where TEntity : class
    {
        private const int MaxQueryTagLength = 256;
        private readonly List<Expression<Func<TEntity, object?>>> _includeExpressions = new();
        private readonly HashSet<string> _includeExpressionKeys = new(StringComparer.Ordinal);
        private readonly List<IOrderExpression<TEntity>> _orderExpressions = new();
        private readonly List<string> _queryTags = new();

        protected Specification()
        {
        }

        public Expression<Func<TEntity, bool>> Criteria { get; private set; } = _ => true;

        public IReadOnlyList<Expression<Func<TEntity, object?>>> IncludeExpressions
            => _includeExpressions.AsReadOnly();

        public IReadOnlyList<IOrderExpression<TEntity>> OrderExpressions
            => _orderExpressions.AsReadOnly();

        public int Skip { get; private set; }

        public int Take { get; private set; }

        public int PageNumber { get; private set; }

        public int PageSize { get; private set; }

        public int MaximumPageSize { get; private set; } = SpecificationDefaults.DefaultMaximumPageSize;

        public bool IsPagingEnabled { get; private set; }

        public bool IsTotalCountEnabled { get; private set; }

        public bool IsDistinct { get; private set; }

        public TrackingBehavior Tracking { get; private set; } = TrackingBehavior.NoTracking;

        public bool IsSplitQuery { get; private set; }

        public bool IsSingleQuery { get; private set; }

        public IReadOnlyList<string> QueryTags => _queryTags.AsReadOnly();

        protected Specification<TEntity> AddCriteria(Expression<Func<TEntity, bool>> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            Criteria = Criteria.AndAlso(predicate);
            return this;
        }

        protected Specification<TEntity> AddInclude(Expression<Func<TEntity, object?>> navigation)
        {
            ArgumentNullException.ThrowIfNull(navigation);
            var key = GetIncludeKey(navigation);
            if (_includeExpressionKeys.Add(key))
            {
                _includeExpressions.Add(navigation);
            }

            return this;
        }

        protected Specification<TEntity> Include(Expression<Func<TEntity, object?>> navigation)
            => AddInclude(navigation);

        protected Specification<TEntity> AddOrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        {
            ArgumentNullException.ThrowIfNull(keySelector);
            _orderExpressions.Add(new OrderExpression<TEntity, TKey>(keySelector, descending: false));
            return this;
        }

        protected Specification<TEntity> AddOrderByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
        {
            ArgumentNullException.ThrowIfNull(keySelector);
            _orderExpressions.Add(new OrderExpression<TEntity, TKey>(keySelector, descending: true));
            return this;
        }

        protected Specification<TEntity> ApplyPaging(
            int pageNumber,
            int pageSize,
            int maximumPageSize = SpecificationDefaults.DefaultMaximumPageSize)
        {
            if (pageNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than zero.");
            }

            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");
            }

            if (maximumPageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumPageSize), "Maximum page size must be greater than zero.");
            }

            if (pageSize > maximumPageSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pageSize),
                    $"Page size cannot exceed the configured maximum of {maximumPageSize}.");
            }

            var skip = ((long)pageNumber - 1) * pageSize;
            if (skip > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pageNumber),
                    "The requested page creates an offset larger than Int32.MaxValue.");
            }

            PageNumber = pageNumber;
            PageSize = pageSize;
            MaximumPageSize = maximumPageSize;
            Skip = (int)skip;
            Take = pageSize;
            IsPagingEnabled = true;
            EnableTotalCount();
            return this;
        }

        protected Specification<TEntity> EnableTotalCount()
        {
            IsTotalCountEnabled = true;
            return this;
        }

        protected Specification<TEntity> EnableDistinct()
        {
            IsDistinct = true;
            return this;
        }

        protected Specification<TEntity> UseNoTracking()
        {
            Tracking = TrackingBehavior.NoTracking;
            return this;
        }

        protected Specification<TEntity> UseNoTrackingWithIdentityResolution()
        {
            Tracking = TrackingBehavior.NoTrackingWithIdentityResolution;
            return this;
        }

        protected Specification<TEntity> UseTracking()
        {
            Tracking = TrackingBehavior.TrackAll;
            return this;
        }

        protected Specification<TEntity> UseSplitQuery()
        {
            if (IsSingleQuery)
            {
                throw new InvalidOperationException("A specification cannot require both split query and single query execution.");
            }

            IsSplitQuery = true;
            return this;
        }

        protected Specification<TEntity> UseSingleQuery()
        {
            if (IsSplitQuery)
            {
                throw new InvalidOperationException("A specification cannot require both split query and single query execution.");
            }

            IsSingleQuery = true;
            return this;
        }

        protected Specification<TEntity> TagWith(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new ArgumentException("Query tag cannot be empty.", nameof(tag));
            }

            var trimmed = tag.Trim();
            if (trimmed.Length > MaxQueryTagLength)
            {
                throw new ArgumentException(
                    $"Query tag cannot exceed {MaxQueryTagLength} characters.",
                    nameof(tag));
            }

            _queryTags.Add(trimmed);
            return this;
        }

        protected Specification<TEntity> CombineWith(params Specification<TEntity>[] specs)
        {
            ArgumentNullException.ThrowIfNull(specs);

            foreach (var specification in specs)
            {
                if (specification is null)
                {
                    continue;
                }

                Criteria = Criteria.AndAlso(specification.Criteria);

                foreach (var include in specification.IncludeExpressions)
                {
                    AddInclude(include);
                }

                foreach (var orderExpression in specification.OrderExpressions)
                {
                    _orderExpressions.Add(orderExpression);
                }

                if (specification.IsPagingEnabled)
                {
                    if (IsPagingEnabled)
                    {
                        throw new InvalidOperationException(
                            "A composed specification can contain only one paging definition.");
                    }

                    CopyPagingFrom(specification);
                }

                if (specification.IsTotalCountEnabled)
                {
                    EnableTotalCount();
                }

                if (specification.IsDistinct)
                {
                    EnableDistinct();
                }

                Tracking = CombineTracking(Tracking, specification.Tracking);

                if (specification.IsSplitQuery && IsSingleQuery ||
                    specification.IsSingleQuery && IsSplitQuery)
                {
                    throw new InvalidOperationException(
                        "Cannot compose specifications that require both split query and single query execution.");
                }

                if (specification.IsSplitQuery)
                {
                    UseSplitQuery();
                }

                if (specification.IsSingleQuery)
                {
                    UseSingleQuery();
                }

                foreach (var queryTag in specification.QueryTags)
                {
                    TagWith(queryTag);
                }
            }

            return this;
        }

        private void CopyPagingFrom(Specification<TEntity> specification)
        {
            PageNumber = specification.PageNumber;
            PageSize = specification.PageSize;
            MaximumPageSize = specification.MaximumPageSize;
            Skip = specification.Skip;
            Take = specification.Take;
            IsPagingEnabled = true;
        }

        private static TrackingBehavior CombineTracking(
            TrackingBehavior current,
            TrackingBehavior incoming)
        {
            if (current == TrackingBehavior.TrackAll || incoming == TrackingBehavior.TrackAll)
            {
                return TrackingBehavior.TrackAll;
            }

            if (current == TrackingBehavior.NoTrackingWithIdentityResolution ||
                incoming == TrackingBehavior.NoTrackingWithIdentityResolution)
            {
                return TrackingBehavior.NoTrackingWithIdentityResolution;
            }

            return TrackingBehavior.NoTracking;
        }

        private static string GetIncludeKey(Expression<Func<TEntity, object?>> navigation)
        {
            var body = StripConvert(navigation.Body);
            var segments = new Stack<string>();

            while (body is MemberExpression memberExpression)
            {
                segments.Push(memberExpression.Member.Name);
                body = StripConvert(memberExpression.Expression);
            }

            return segments.Count == 0
                ? navigation.Body.ToString()
                : string.Join(".", segments);
        }

        private static Expression? StripConvert(Expression? expression)
        {
            while (expression is UnaryExpression unary &&
                (unary.NodeType == ExpressionType.Convert ||
                 unary.NodeType == ExpressionType.ConvertChecked))
            {
                expression = unary.Operand;
            }

            return expression;
        }
    }

    public abstract class Specification<TEntity, TOut> : Specification<TEntity>
        where TEntity : class
    {
        private Expression<Func<TEntity, TOut>>? _selector;

        public Expression<Func<TEntity, TOut>> Selector
            => _selector ?? throw new InvalidOperationException(
                "Projection selector is not defined. Call Select(...) inside the specification constructor.");

        protected Specification<TEntity, TOut> Select(Expression<Func<TEntity, TOut>> selector)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            return this;
        }
    }

    internal static class SpecificationExpressionExtensions
    {
        public static Expression<Func<TEntity, bool>> AndAlso<TEntity>(
            this Expression<Func<TEntity, bool>> left,
            Expression<Func<TEntity, bool>> right)
        {
            var parameter = Expression.Parameter(typeof(TEntity), "entity");
            var leftBody = new ReplaceParameterVisitor(left.Parameters[0], parameter).Visit(left.Body)!;
            var rightBody = new ReplaceParameterVisitor(right.Parameters[0], parameter).Visit(right.Body)!;
            return Expression.Lambda<Func<TEntity, bool>>(Expression.AndAlso(leftBody, rightBody), parameter);
        }

        private sealed class ReplaceParameterVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _from;
            private readonly ParameterExpression _to;

            public ReplaceParameterVisitor(ParameterExpression from, ParameterExpression to)
            {
                _from = from;
                _to = to;
            }

            protected override Expression VisitParameter(ParameterExpression node)
                => node == _from ? _to : base.VisitParameter(node);
        }
    }
}
