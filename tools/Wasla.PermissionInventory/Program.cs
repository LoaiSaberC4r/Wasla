using System.Text.Json;
using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Security;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var csvPath = Path.Combine(root, "docs/subscriptions/permission-coverage.csv");
var existing = File.ReadAllLines(csvPath);
var byName = existing.Skip(1).ToDictionary(r => r.Split(',')[0], StringComparer.Ordinal);
var rows = new List<string> { existing[0] };
var inventory = PermissionNames.All.Select((name, index) => new { index, name, id = SystemPermissionIds.For(name) }).ToArray();
foreach (var item in inventory)
{
    var classification = CommercialPermissionCatalog.All[item.name];
    var purpose = item.name switch
    {
        FamilyGovernancePermissionNames.IdentityCorrectionRequest => "Request evidence reviewed dependent identity correction",
        FamilyGovernancePermissionNames.InviteCreate => "Issue or revoke a private second parent invitation",
        FamilyGovernancePermissionNames.InviteRedeem => "Redeem invitation into reviewed relationship or confidential conflict case",
        _ => ""
    };
    var row = byName.GetValueOrDefault(item.name) ?? $"{item.name},{item.id},{classification.Category},,,Patient,JWT+DB and exact operation scope,,,docs/family-self-service/API-HANDOFF-EXTENSIONS.md,{purpose}";
    var cells = row.Split(',');
    if (Guid.Parse(cells[1]) != item.id || cells[2] != classification.Category.ToString() || cells[3] != (classification.FeatureCode ?? ""))
        throw new InvalidOperationException($"Inventory drift: {item.name}");
    rows.Add(row);
}
File.WriteAllLines(csvPath, rows);
File.WriteAllText(Path.Combine(root, "docs/family-self-service/new-permissions.json"),
    JsonSerializer.Serialize(inventory.Skip(337), new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
Console.WriteLine($"Generated {inventory.Length} stable permission mappings; {inventory.Length - 337} appended since the frozen baseline.");
