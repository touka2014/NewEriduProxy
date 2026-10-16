using System.Text.Json;
using ServiceLib.Models.Configs;

namespace ServiceLib.Tests.Handler;

public class CheckUpdateMigrationTests
{
    [Test]
    public async Task Deserialize_LegacyPrereleaseSelection_PreservesApplicationAndXrayUpdates()
    {
        var item = JsonSerializer.Deserialize<CheckUpdateItem>("""{"CheckPreReleaseUpdate":true}""")!;

        await item.CheckPreReleaseCoreTypes!.Should().BeEquivalentTo(new[] { "v2rayN", "Xray" });
        await JsonSerializer.Serialize(item).Contains("CheckPreReleaseUpdate").Should().BeFalse();
    }

    [Test]
    [Arguments("[]")]
    [Arguments("[\"Xray\"]")]
    public async Task Deserialize_NewSelection_TakesPrecedenceOverLegacyFlag(string selection)
    {
        var item = JsonSerializer.Deserialize<CheckUpdateItem>(
            $"{{\"CheckPreReleaseUpdate\":true,\"CheckPreReleaseCoreTypes\":{selection}}}")!;

        await item.CheckPreReleaseCoreTypes!.Should().BeEquivalentTo(JsonSerializer.Deserialize<string[]>(selection)!);
    }

    [Test]
    public async Task Deserialize_LegacyStableSelection_DoesNotEnablePrereleases()
    {
        var item = JsonSerializer.Deserialize<CheckUpdateItem>("""{"CheckPreReleaseUpdate":false}""")!;

        await (item.CheckPreReleaseCoreTypes?.Count ?? 0).Should().BeEqualTo(0);
    }
}
