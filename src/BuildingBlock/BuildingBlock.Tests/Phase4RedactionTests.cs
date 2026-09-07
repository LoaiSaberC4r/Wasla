using BuildingBlock.Api.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace BuildingBlock.Tests;

public sealed class Phase4RedactionTests
{
    [Theory]
    [InlineData("Password")]
    [InlineData("password")]
    [InlineData("Credentials.Password")]
    [InlineData("Headers.Authorization")]
    public void Sensitive_property_names_are_fully_redacted(string propertyName)
    {
        var policy = new DefaultLogRedactionPolicy();

        var result = policy.Redact(propertyName, "P@ssw0rd!");

        Assert.Equal("***", result.Value);
    }

    [Fact]
    public void Free_text_sensitive_content_is_redacted()
    {
        var policy = new DefaultLogRedactionPolicy();

        var result = (string)policy.Redact(
            "Message",
            "User john@example.com used Authorization: Bearer abc.def.ghi from +201234567890").Value!;

        Assert.DoesNotContain("john@example.com", result);
        Assert.DoesNotContain("abc.def.ghi", result);
        Assert.DoesNotContain("+201234567890", result);
        Assert.Contains("***", result);
    }

    [Fact]
    public void Dictionaries_sequences_and_nested_objects_are_redacted()
    {
        var policy = new DefaultLogRedactionPolicy();
        var input = new Dictionary<string, object?>
        {
            ["UserName"] = "john",
            ["Password"] = "secret",
            ["Items"] = new[] { "john@example.com", "safe" },
            ["Credentials"] = new { AccessToken = "abc.def.ghi", DisplayName = "John" }
        };

        var result = Assert.IsType<Dictionary<object, object?>>(policy.Redact("Root", input).Value);

        Assert.Equal("john", result["UserName"]);
        Assert.Equal("***", result["Password"]);
        var items = Assert.IsType<List<object?>>(result["Items"]);
        Assert.Equal("***", items[0]);
        var credentials = Assert.IsType<Dictionary<string, object?>>(result["Credentials"]);
        Assert.Equal("***", credentials["AccessToken"]);
        Assert.Equal("John", credentials["DisplayName"]);
    }

    [Fact]
    public void Long_strings_are_truncated_after_redaction()
    {
        var policy = new DefaultLogRedactionPolicy(new LogRedactionOptions
        {
            MaximumStringLength = 32
        });

        var result = (string)policy.Redact("Message", "john@example.com " + new string('a', 80)).Value!;

        Assert.DoesNotContain("john@example.com", result);
        Assert.EndsWith("...[truncated]", result);
        Assert.True(result.Length <= 32);
    }

    [Fact]
    public void Serilog_structured_properties_are_replaced_safely()
    {
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            new MessageTemplateParser().Parse("{Credentials}"),
            new[]
            {
                new LogEventProperty(
                    "Credentials",
                    new StructureValue(new[]
                    {
                        new LogEventProperty("Password", new ScalarValue("P@ssw0rd!")),
                        new LogEventProperty("Email", new ScalarValue("john@example.com")),
                        new LogEventProperty("UserName", new ScalarValue("john"))
                    }))
            });

        new RedactionEnricher().Enrich(logEvent, new TestPropertyFactory());

        var structure = Assert.IsType<StructureValue>(logEvent.Properties["Credentials"]);
        Assert.Equal("***", Assert.IsType<ScalarValue>(structure.Properties.Single(p => p.Name == "Password").Value).Value);
        Assert.Equal("***", Assert.IsType<ScalarValue>(structure.Properties.Single(p => p.Name == "Email").Value).Value);
        Assert.Equal("john", Assert.IsType<ScalarValue>(structure.Properties.Single(p => p.Name == "UserName").Value).Value);
    }

    private sealed class TestPropertyFactory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
            => new(name, new ScalarValue(value));
    }
}
