/// <summary>
/// Packs an app with two heads through the targets as a package reference would import them.
/// Each test is a real pack with a real publish for each head, so there are few of them.
/// </summary>
public class BundlingTests
{
    [Test]
    public async Task EachHeadIsBundledUnderItsRuntimeIdentifier()
    {
        using var app = new FixtureApp();
        app.AddNative("linux-x64");
        app.AddNative("linux-arm64");

        var result = await app.Pack("-p:PlatformHeadsOutputPath=heads");

        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        var entries = app.PackageEntries();
        await Assert.That(entries).Contains("heads/win-x64/App.Head.exe");
        // No extension, which is what NuGet reads as a folder when the file is named outright.
        await Assert.That(entries).Contains("heads/linux-x64/App.Head");
        await Assert.That(entries).Contains("heads/linux-x64/App.Head.dll");
        // A head published for one runtime identifier carries its own native library and no other.
        await Assert.That(entries).Contains("heads/linux-x64/runtimes/linux-x64/native/libfixture.so");
        await Assert.That(entries.Where(_ => _.Contains("linux-arm64"))).IsEmpty();
        await Assert.That(entries.Where(_ => _.StartsWith("heads/win-x64/runtimes/"))).IsEmpty();
        await Assert.That(result.Output).DoesNotContain("PH0001");

        // And beside the build output, since the test asked for that.
        var output = Path.Combine(app.Root, "App", "bin", "Release", "net10.0", "heads");
        await Assert.That(File.Exists(Path.Combine(output, "win-x64", "App.Head.exe"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(output, "linux-x64", "App.Head"))).IsTrue();
    }

    [Test]
    public async Task ANativeHeadWithNoLibraryIsWarnedOf()
    {
        using var app = new FixtureApp();

        var result = await app.Pack();

        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        await Assert.That(result.Output).Contains("warning PH0001: No native renderer for linux-x64");
        await Assert.That(result.Output).DoesNotContain("No native renderer for win-x64");
    }

    [Test]
    public async Task AHeadWhoseApphostWasNotStagedFailsThePack()
    {
        using var app = new FixtureApp();
        app.AddNative("linux-x64");

        var result = await app.Pack("-p:LinuxApphost=NotWhatThePublishMakes");

        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(result.Output).Contains("error PH0002: No apphost was staged for: linux-x64");
    }
}
