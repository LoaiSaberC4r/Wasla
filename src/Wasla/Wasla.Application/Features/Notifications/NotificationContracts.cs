using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Persistence;
using Wasla.Domain.Notifications;

namespace Wasla.Application.Features.Notifications;

public sealed record NotificationPage<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);
public sealed record InboxFilter(int PageNumber = 1, int PageSize = 20, NotificationCategory? Category = null, bool? IsRead = null);
public sealed record NotificationTarget(string ResourceType, string? ResourceId);
public sealed record InboxItem(Guid Id, string EventType, NotificationCategory Category, string Title, string Message,
    DateTime CreatedAtUtc, DateTime? ReadAtUtc, bool IsRead, NotificationTarget? Target);
public sealed record UnreadCountResponse(int UnreadCount);
public sealed record PreferenceItem(NotificationCategory Category, NotificationChannel Channel, bool Enabled, bool Editable = true);
public sealed record PreferencesResponse(string PreferredLanguage, IReadOnlyList<PreferenceItem> Preferences, string RowVersion);
public sealed record UpdatePreferencesRequest(string PreferredLanguage, IReadOnlyList<PreferenceItem> Preferences, string RowVersion);
public sealed record NotificationSettingsResponse(bool AppointmentReminderEnabled, int ReminderLeadMinutes, long Version, string RowVersion);
public sealed record UpdateNotificationSettingsRequest(bool AppointmentReminderEnabled, int ReminderLeadMinutes, string Reason, string RowVersion);
public sealed record DeliveryFilter(NotificationDeliveryStatus? Status = null, NotificationChannel? Channel = null, string? EventType = null,
    DateTime? FromUtc = null, DateTime? ToUtc = null, int PageNumber = 1, int PageSize = 20);
public sealed record DeliveryResponse(Guid Id, string EventType, NotificationCategory Category, NotificationChannel Channel,
    NotificationDeliveryStatus Status, int AttemptCount, DateTime CreatedAtUtc, DateTime? DispatchedAtUtc,
    DateTime? LastAttemptAtUtc, string? FailureCode, bool RetryEligible, string RowVersion);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record RetryDeliveryRequest(string Reason, string RowVersion);
internal static class NotificationValidation
{
    internal static bool Version(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100) return false;
        Span<byte> bytes = stackalloc byte[64];
        return Convert.TryFromBase64String(value, bytes, out var written) && written == length;
    }
}
public interface INotificationService
{
    Task<Result<NotificationPage<InboxItem>>> InboxAsync(InboxFilter filter, CancellationToken ct);
    Task<Result<UnreadCountResponse>> UnreadAsync(CancellationToken ct);
    Task<Result> MarkReadAsync(Guid? id, CancellationToken ct);
    Task<Result<PreferencesResponse>> PreferencesAsync(CancellationToken ct);
    Task<Result<PreferencesResponse>> UpdatePreferencesAsync(UpdatePreferencesRequest request, CancellationToken ct);
    Task<Result<NotificationSettingsResponse>> SettingsAsync(CancellationToken ct);
    Task<Result<NotificationSettingsResponse>> UpdateSettingsAsync(UpdateNotificationSettingsRequest request, CancellationToken ct);
    Task<Result<NotificationPage<DeliveryResponse>>> DeliveriesAsync(DeliveryFilter filter, CancellationToken ct);
    Task<Result<DeliveryResponse>> DeliveryAsync(Guid id, CancellationToken ct);
    Task<Result> RetryAsync(Guid id, RetryDeliveryRequest request, CancellationToken ct);
}
public interface INotificationAuthorizationAudit { Task RetryDeniedAsync(Guid actor, CancellationToken ct); }
public sealed record ListMyNotificationsQuery(InboxFilter Filter) : IQuery<NotificationPage<InboxItem>>;
public sealed record GetMyUnreadCountQuery : IQuery<UnreadCountResponse>;
public sealed record GetMyNotificationPreferencesQuery : IQuery<PreferencesResponse>;
public sealed record GetPlatformNotificationSettingsQuery : IQuery<NotificationSettingsResponse>;
public sealed record ListNotificationDeliveriesQuery(DeliveryFilter Filter) : IQuery<NotificationPage<DeliveryResponse>>;
public sealed record GetNotificationDeliveryQuery(Guid Id) : IQuery<DeliveryResponse>;
public sealed record MarkMyNotificationReadCommand(Guid Id) : ICommand, ITransactionalCommand<WaslaWritePersistence>;
public sealed record MarkAllMyNotificationsReadCommand : ICommand, ITransactionalCommand<WaslaWritePersistence>;
public sealed record UpdateMyNotificationPreferencesCommand(UpdatePreferencesRequest Request) : ICommand<PreferencesResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record UpdatePlatformNotificationSettingsCommand(UpdateNotificationSettingsRequest Request) : ICommand<NotificationSettingsResponse>, ITransactionalCommand<WaslaWritePersistence>;
// Retry persists denial audit, so its service owns the transaction and commits the audit even on denial.
public sealed record RetryNotificationDeliveryCommand(Guid Id, RetryDeliveryRequest Request) : ICommand;

internal sealed class NotificationHandlers(INotificationService service) :
    IQueryHandler<ListMyNotificationsQuery, NotificationPage<InboxItem>>, IQueryHandler<GetMyUnreadCountQuery, UnreadCountResponse>,
    IQueryHandler<GetMyNotificationPreferencesQuery, PreferencesResponse>, IQueryHandler<GetPlatformNotificationSettingsQuery, NotificationSettingsResponse>,
    IQueryHandler<ListNotificationDeliveriesQuery, NotificationPage<DeliveryResponse>>, IQueryHandler<GetNotificationDeliveryQuery, DeliveryResponse>,
    ICommandHandler<MarkMyNotificationReadCommand>, ICommandHandler<MarkAllMyNotificationsReadCommand>,
    ICommandHandler<UpdateMyNotificationPreferencesCommand, PreferencesResponse>, ICommandHandler<UpdatePlatformNotificationSettingsCommand, NotificationSettingsResponse>,
    ICommandHandler<RetryNotificationDeliveryCommand>
{
    public Task<Result<NotificationPage<InboxItem>>> Handle(ListMyNotificationsQuery r, CancellationToken ct) => service.InboxAsync(r.Filter, ct);
    public Task<Result<UnreadCountResponse>> Handle(GetMyUnreadCountQuery r, CancellationToken ct) => service.UnreadAsync(ct);
    public Task<Result<PreferencesResponse>> Handle(GetMyNotificationPreferencesQuery r, CancellationToken ct) => service.PreferencesAsync(ct);
    public Task<Result<NotificationSettingsResponse>> Handle(GetPlatformNotificationSettingsQuery r, CancellationToken ct) => service.SettingsAsync(ct);
    public Task<Result<NotificationPage<DeliveryResponse>>> Handle(ListNotificationDeliveriesQuery r, CancellationToken ct) => service.DeliveriesAsync(r.Filter, ct);
    public Task<Result<DeliveryResponse>> Handle(GetNotificationDeliveryQuery r, CancellationToken ct) => service.DeliveryAsync(r.Id, ct);
    public Task<Result> Handle(MarkMyNotificationReadCommand r, CancellationToken ct) => service.MarkReadAsync(r.Id, ct);
    public Task<Result> Handle(MarkAllMyNotificationsReadCommand r, CancellationToken ct) => service.MarkReadAsync(null, ct);
    public Task<Result<PreferencesResponse>> Handle(UpdateMyNotificationPreferencesCommand r, CancellationToken ct) => service.UpdatePreferencesAsync(r.Request, ct);
    public Task<Result<NotificationSettingsResponse>> Handle(UpdatePlatformNotificationSettingsCommand r, CancellationToken ct) => service.UpdateSettingsAsync(r.Request, ct);
    public Task<Result> Handle(RetryNotificationDeliveryCommand r, CancellationToken ct) => service.RetryAsync(r.Id, r.Request, ct);
}
internal sealed class InboxQueryValidator : AbstractValidator<ListMyNotificationsQuery>
{
    public InboxQueryValidator() { RuleFor(r => r.Filter.PageNumber).InclusiveBetween(1, 1000000); RuleFor(r => r.Filter.PageSize).InclusiveBetween(1, 100); RuleFor(r => r.Filter.Category).IsInEnum().When(r => r.Filter.Category.HasValue); }
}
internal sealed class DeliveryQueryValidator : AbstractValidator<ListNotificationDeliveriesQuery>
{
    public DeliveryQueryValidator()
    {
        RuleFor(r => r.Filter.PageNumber).InclusiveBetween(1, 1000000); RuleFor(r => r.Filter.PageSize).InclusiveBetween(1, 100);
        RuleFor(r => r.Filter.Status).IsInEnum().When(r => r.Filter.Status.HasValue); RuleFor(r => r.Filter.Channel).IsInEnum().When(r => r.Filter.Channel.HasValue);
        RuleFor(r => r.Filter.EventType).Must(x => x is null || NotificationPolicy.KnownEvent(x));
        RuleFor(r => r.Filter).Must(f => (!f.FromUtc.HasValue || f.FromUtc.Value.Kind == DateTimeKind.Utc) && (!f.ToUtc.HasValue || f.ToUtc.Value.Kind == DateTimeKind.Utc) &&
            (!f.FromUtc.HasValue || !f.ToUtc.HasValue || f.FromUtc <= f.ToUtc));
    }
}
internal sealed class PreferencesValidator : AbstractValidator<UpdateMyNotificationPreferencesCommand>
{
    public PreferencesValidator()
    {
        RuleFor(x => x.Request.PreferredLanguage).Must(x => x is "ar" or "en");
        RuleFor(x => x.Request.RowVersion).Must(v => NotificationValidation.Version(v, 32));
        RuleFor(x => x.Request.Preferences).NotNull().Must(x => x is { Count: >= 1 and <= 4 } && x.All(i => NotificationPolicy.Editable(i.Category) && Enum.IsDefined(i.Channel)) && x.DistinctBy(i => (i.Category, i.Channel)).Count() == x.Count);
    }
}
internal sealed class SettingsValidator : AbstractValidator<UpdatePlatformNotificationSettingsCommand>
{
    public SettingsValidator() { RuleFor(x => x.Request.ReminderLeadMinutes).InclusiveBetween(1, 43200); RuleFor(x => x.Request.Reason).NotEmpty().MaximumLength(500); RuleFor(x => x.Request.RowVersion).Must(v => NotificationValidation.Version(v, 8)); }
}
internal sealed class ReadValidator : AbstractValidator<MarkMyNotificationReadCommand> { public ReadValidator() => RuleFor(x => x.Id).NotEmpty(); }
internal sealed class DeliveryValidator : AbstractValidator<GetNotificationDeliveryQuery> { public DeliveryValidator() => RuleFor(x => x.Id).NotEmpty(); }
internal sealed class RetryValidator : AbstractValidator<RetryNotificationDeliveryCommand>
{
    public RetryValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Request.Reason).NotEmpty().MaximumLength(500); RuleFor(x => x.Request.RowVersion).Must(v => NotificationValidation.Version(v, 32)); }
}
