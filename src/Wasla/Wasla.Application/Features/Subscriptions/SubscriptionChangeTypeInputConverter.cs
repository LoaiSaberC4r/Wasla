using System.Text.Json;
using System.Text.Json.Serialization;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Subscriptions;

// Accept the approved flat-contract action names while keeping persisted/output change categories stable.
public sealed class SubscriptionChangeTypeInputConverter : JsonConverter<SubscriptionChangeType?>
{
    public override SubscriptionChangeType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("A named subscription change type is required.");
        return reader.GetString()?.ToUpperInvariant() switch
        {
            "UPGRADE" or "UPGRADEPLAN" or "INCREASEBOOKINGPRACTICELIMIT" => SubscriptionChangeType.Upgrade,
            "DOWNGRADE" or "DOWNGRADEPLAN" or "DECREASEBOOKINGPRACTICELIMIT" => SubscriptionChangeType.Downgrade,
            "BILLINGCYCLE" or "CHANGEBILLINGCYCLE" => SubscriptionChangeType.BillingCycle,
            _ => throw new JsonException("The subscription change type is invalid.")
        };
    }
    public override void Write(Utf8JsonWriter writer, SubscriptionChangeType? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value.ToString());
    }
}
