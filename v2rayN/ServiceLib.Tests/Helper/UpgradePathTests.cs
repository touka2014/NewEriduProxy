namespace ServiceLib.Tests.Helper;

public class UpgradePathTests
{
    [Test]
    [Arguments("NewEriduProxy.exe", true)]
    [Arguments("bin/Xray/xray.exe", true)]
    [Arguments("../outside.txt", false)]
    [Arguments("bin/../../outside.txt", false)]
    [Arguments("../app-sibling/outside.txt", false)]
    public async Task IsPathUnderDirectory_RejectsArchivePathsOutsideApplication(string relativePath, bool expected)
    {
        var appDirectory = Path.Combine(Path.GetTempPath(), "new-eridu-path-tests", "app");
        var outputPath = Path.Combine(appDirectory, relativePath);

        await AmazTool.Utils.IsPathUnderDirectory(appDirectory, outputPath).Should().BeEqualTo(expected);
    }

    [Test]
    public async Task IsPathUnderDirectory_RejectsAbsoluteOutsidePath()
    {
        var appDirectory = Path.Combine(Path.GetTempPath(), "new-eridu-path-tests", "app");
        var outsidePath = Path.Combine(Path.GetTempPath(), "outside.txt");

        await AmazTool.Utils.IsPathUnderDirectory(appDirectory, outsidePath).Should().BeFalse();
    }
}
