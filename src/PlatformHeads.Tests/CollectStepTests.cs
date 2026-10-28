/// <summary>
/// Runs the macOS job's collect step, as the workflow file has it, over a stand-in for what
/// swift build leaves, with lipo and strip stood in for: a Swift package that makes a second
/// library, such as a tray's beside a window's, has each one collected into the head that loads
/// it. The step runs on macOS only, so nothing else runs it before an app's first build.
/// </summary>
public class CollectStepTests
{
    [Test]
    public async Task EachLibraryIsCollectedIntoItsOwnHead()
    {
        var (result, collected) = await RunCollect(
            ["myapp_ui", "myapp_tray"],
            "myapp_tray src/MyApp.Tray/runtimes");

        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        await Assert.That(collected).IsEquivalentTo(
        [
            "src/MyApp.Native/runtimes/osx-arm64/native/libmyapp_ui.dylib",
            "src/MyApp.Native/runtimes/osx-x64/native/libmyapp_ui.dylib",
            "src/MyApp.Tray/runtimes/osx-arm64/native/libmyapp_tray.dylib",
            "src/MyApp.Tray/runtimes/osx-x64/native/libmyapp_tray.dylib"
        ]);
    }

    /// <summary>
    /// An app with one library, which is every app that does not set mac-libraries.
    /// </summary>
    [Test]
    public async Task OneLibraryIsCollectedAsItWas()
    {
        var (result, collected) = await RunCollect(["myapp_ui"], "");

        await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
        await Assert.That(collected).IsEquivalentTo(
        [
            "src/MyApp.Native/runtimes/osx-arm64/native/libmyapp_ui.dylib",
            "src/MyApp.Native/runtimes/osx-x64/native/libmyapp_ui.dylib"
        ]);
    }

    [Test]
    public async Task ALibraryTheBuildDidNotMakeFails()
    {
        var (result, _) = await RunCollect(["myapp_ui"], "myapp_tray src/MyApp.Tray/runtimes");

        await Assert.That(result.ExitCode).IsEqualTo(1).Because(result.Output);
        await Assert.That(result.Output).Contains("swift build produced no libmyapp_tray.dylib");
    }

    /// <summary>
    /// The propose job's paths step, which is what puts the further libraries' folders in the
    /// pull request.
    /// </summary>
    [Test]
    public async Task ThePullRequestTakesEachLibrarysFolder()
    {
        var bash = Bash.Find();
        if (bash is null)
        {
            Skip.Test("No bash to run the step with.");
        }

        var directory = Directory.CreateTempSubdirectory("PlatformHeads.Tests").FullName;
        try
        {
            var script = new StringBuilder();
            script.Append("export MORE='myapp_tray src/MyApp.Tray/runtimes\nmyapp_agent src/MyApp.Agent/runtimes'\n");
            script.Append("export GITHUB_OUTPUT=output\n");
            script.Append(string.Join('\n', WorkflowFile.Block("- name: Paths", "run: |")));
            script.Append('\n');

            var result = await Bash.Run(bash, script.ToString(), directory);

            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
            var output = await File.ReadAllTextAsync(Path.Combine(directory, "output"));
            await Assert.That(output.ReplaceLineEndings("\n")).IsEqualTo(
                "more<<END_OF_PATHS\nsrc/MyApp.Tray/runtimes\nsrc/MyApp.Agent/runtimes\nEND_OF_PATHS\n");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Each library in <paramref name="built"/> as swift build leaves it, already universal, in
    /// one of the places it leaves them.
    /// </summary>
    static async Task<(RunResult Result, List<string> Collected)> RunCollect(string[] built, string more)
    {
        var bash = Bash.Find();
        if (bash is null)
        {
            Skip.Test("No bash to run the step with.");
        }

        var directory = Directory.CreateTempSubdirectory("PlatformHeads.Tests").FullName;
        try
        {
            var release = Path.Combine(directory, "native", "swift", ".build", "apple", "Products", "Release");
            Directory.CreateDirectory(release);
            foreach (var library in built)
            {
                await File.WriteAllTextAsync(Path.Combine(release, $"lib{library}.dylib"), library);
            }

            var script = new StringBuilder();
            // The two tools only a Mac has, as they answer for a universal binary
            script.Append("lipo() { echo 'x86_64 arm64'; }\n");
            script.Append("strip() { :; }\n");
            script.Append("export LIBRARY=myapp_ui\n");
            script.Append("export PACKAGE=native/swift\n");
            script.Append("export RUNTIMES=src/MyApp.Native/runtimes\n");
            script.Append($"export MORE='{more}'\n");
            script.Append(string.Join('\n', WorkflowFile.Block(["name: osx", "- name: Collect"], "run: |")));
            script.Append('\n');

            var result = await Bash.Run(bash, script.ToString(), directory);
            var artifacts = Path.Combine(directory, "artifacts");
            if (!Directory.Exists(artifacts))
            {
                return (result, []);
            }

            var collected = Directory.EnumerateFiles(artifacts, "*", SearchOption.AllDirectories)
                .Select(_ => Path.GetRelativePath(artifacts, _).Replace('\\', '/'))
                .Order()
                .ToList();
            return (result, collected);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
