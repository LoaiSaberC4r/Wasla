using Wasla.Domain.Diagnostics;

namespace Wasla.Application.Features.Diagnostics;

internal static class DiagnosticCapabilities
{
    public static DiagnosticRequestStateResponse? Request(DiagnosticRequestStateResponse? state, IReadOnlyList<string> permissions, string kind)
    {
        if (state is null) return null;
        var draft = state.Status == DiagnosticRequestStatus.Draft;
        var manage = draft && permissions.Contains(kind + "Requests.ManageOwnDraft");
        var cancel = !draft && permissions.Contains(kind + "Requests.CancelOwn");
        return state with
        {
            Capabilities = new(manage, cancel && state.Items.Any(i => i.Status == DiagnosticItemStatus.Requested),
                !draft && state.Items.Any(i => i.Status == DiagnosticItemStatus.Requested) && permissions.Contains(kind + "Results.UploadOwn")),
            Items = state.Items.Select(i => i with { Capabilities = new(manage, manage, cancel && i.Status == DiagnosticItemStatus.Requested, i.Status == DiagnosticItemStatus.Completed) }).ToArray(),
            CurrentResults = permissions.Contains(kind + "Results.ViewOwn") ? state.CurrentResults.Select(r => r with
            { Capabilities = new(r.Current is not null && permissions.Contains(kind + "Results.CorrectOwn"), r.Current is not null && permissions.Contains(kind + "Results.VoidOwn")) }).ToArray() : [],
            Submissions = permissions.Contains(kind + "ResultSubmissions.ViewOwn") ? state.Submissions : []
        };
    }
}
