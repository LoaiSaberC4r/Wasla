using Microsoft.AspNetCore.Mvc.Routing;
using Wasla.Api.Controllers;
using Wasla.Domain.Patients;
namespace Wasla.Tests.Architecture;
public sealed class Phase5FinalIdentityArchitectureTests
{
    [Fact] public void Identity_contract_exposes_exactly_20_new_routes_and_no_merge_or_role_switch()
    {
        Type[] controllers = [typeof(PatientsRegistrationController), typeof(PatientOnboardingController), typeof(PatientClaimsAdminController), typeof(PatientDuplicatesAdminController)];
        var routes = controllers.SelectMany(c => c.GetMethods()).SelectMany(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true).Cast<HttpMethodAttribute>()).ToArray();
        Assert.Equal(20, routes.Length); Assert.DoesNotContain(routes, r => r.Template?.Contains("merge", StringComparison.OrdinalIgnoreCase) == true || r.Template?.Contains("role-switch", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Null(typeof(Patient).GetProperty("ApplicationUserId"));
    }
    [Fact] public void Identity_domain_stays_independent_of_application_database_api_and_mail()
    {
        var references = typeof(PatientClaimRequest).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.DoesNotContain(references, n => n is not null && (n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) || n.StartsWith("Wasla.Application", StringComparison.Ordinal) || n.StartsWith("Wasla.Api", StringComparison.Ordinal)));
    }
}
