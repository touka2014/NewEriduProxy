using ServiceLib.Models.Configs;

namespace ServiceLib.Tests.Handler;

public class CheckUpdateMigrationTests
{
    [Test]
    public async Task Deserialize_LegacyPrereleaseSelection_PreservesApplicationAndXrayUpdates()
    {
        var item = JsonUtils.Deserialize<CheckUpdateItem>("""{"CheckPreReleaseUpdate":true}""")!;

        await item.CheckPreReleaseCoreTypes!.SequenceEqual(["v2rayN", "Xray"]).Should().BeTrue();
        await JsonUtils.Serialize(item).Contains("CheckPreReleaseUpdate").Should().BeFalse();
    }

    [Test]
    [Arguments("[]")]
    [Arguments("[\"Xray\"]")]
    public async Task Deserialize_NewSelection_TakesPrecedenceOverLegacyFlag(string selection)
    {
        var item = JsonUtils.Deserialize<CheckUpdateItem>(
            $"{{\"CheckPreReleaseUpdate\":true,\"CheckPreReleaseCoreTypes\":{selection}}}")!;

        await item.CheckPreReleaseCoreTypes!.SequenceEqual(JsonUtils.Deserialize<string[]>(selection)!).Should().BeTrue();
    }

    [Test]
    public async Task Deserialize_LegacyStableSelection_DoesNotEnablePrereleases()
    {
        var item = JsonUtils.Deserialize<CheckUpdateItem>("""{"CheckPreReleaseUpdate":false}""")!;

        await (item.CheckPreReleaseCoreTypes?.Count ?? 0).Should().BeEqualTo(0);
    }
}
