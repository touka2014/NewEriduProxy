namespace ServiceLib.Tests.Manager;

public class ProfileExManagerTests
{
    [Test]
    public async Task OrganizeMixedPorts_ShouldReturnCheckedConsecutivePortsInPreferredRange()
    {
        var manager = new ProfileExManager();
        var assignments = manager.OrganizeMixedPorts(["node-1", "node-2", "node-3", "node-4"]);
        var ports = assignments.Values.Order().ToList();
        var activePorts = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(x => x.Port)
            .Concat(IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Select(x => x.Port))
            .ToHashSet();

        await assignments.Count.Should().BeEqualTo(4);
        await ports.First().Should().BeGreaterThanOrEqualTo(Global.ParallelPortMin);
        await ports.Last().Should().BeLessThanOrEqualTo(Global.ParallelPortMax);
        await ports.Should().BeEquivalentTo(Enumerable.Range(ports.First(), 4));
        await ports.Any(activePorts.Contains).Should().BeFalse();
    }
}
