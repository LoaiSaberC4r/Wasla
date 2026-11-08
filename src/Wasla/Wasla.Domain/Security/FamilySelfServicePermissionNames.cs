namespace Wasla.Domain.Security;

public static class FamilySelfServicePermissionNames
{
    public const string Workspace = "FamilyWorkspace.ViewOwn";
    public const string Search = "PatientFamilyCandidates.Search";
    public const string Submit = "PatientFamilyCandidates.Submit";
    public const string Profile = "DependentPatientProfile.View";
    public const string Tickets = "DependentTickets.ViewAuthorized";
    public const string RevokeAuthority = "FamilyOperationalProofRequests.Revoke";
    public const string Onboard = "ChildOnboarding.CreateOwn";
    public const string OnboardingOwn = "ChildOnboarding.ViewOwn";
    public const string OnboardingResubmit = "ChildOnboarding.ResubmitOwn";
    public const string OnboardingReview = "ChildOnboarding.Decide";
    public const string OnboardingInbox = "ChildOnboarding.ViewAll";
    public const string OnboardingDetails = "ChildOnboarding.ViewDetails";
    public const string OnboardingEvidence = "ChildOnboarding.ViewEvidence";
    public static readonly IReadOnlyList<string> PatientDefaults = [Workspace, Search, Submit, Profile, Tickets];
    public static readonly IReadOnlyList<string> AdminDefaults = [RevokeAuthority];
    public static readonly IReadOnlyList<string> OnboardingPatientDefaults = [Onboard, OnboardingOwn, OnboardingResubmit];
    public static readonly IReadOnlyList<string> OnboardingAdminDefaults = [OnboardingReview, OnboardingInbox, OnboardingDetails, OnboardingEvidence];
    // Stable registry order is independent of mutable role defaults. Future names
    // must be appended at the end of PermissionNames.All, never inserted here.
    public static readonly IReadOnlyList<string> All = [Workspace, Search, Submit, Profile, Tickets, RevokeAuthority,
        Onboard, OnboardingOwn, OnboardingResubmit, OnboardingReview, OnboardingInbox, OnboardingDetails, OnboardingEvidence];
}
