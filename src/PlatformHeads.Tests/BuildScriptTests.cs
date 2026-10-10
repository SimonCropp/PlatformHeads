public class BuildScriptTests
{
    /// <summary>
    /// The workflow carries the build script in itself, because a called workflow runs in the
    /// caller's checkout and cannot fetch a file of this repository at the tag it was called at.
    /// native/build-linux.sh is the same script for running by hand, and two copies of anything
    /// here are what this repository exists to be rid of, so they are held to each other.
    /// </summary>
    [Test]
    public async Task TheWorkflowCarriesTheScriptWordForWord()
    {
        var carried = WorkflowFile.Block("- name: Build", "BUILD_SCRIPT: |");
        var script = File.ReadAllLines(Repository.BuildScript).ToList();

        await Assert.That(string.Join('\n', carried)).IsEqualTo(string.Join('\n', script));
    }

    /// <summary>
    /// A carriage return at the end of a line is part of the command to bash, and the script is
    /// run in a container from a checkout that may be on Windows.
    /// </summary>
    [Test]
    public async Task TheScriptHasUnixLineEndings()
    {
        var bytes = await File.ReadAllBytesAsync(Repository.BuildScript);

        await Assert.That(bytes.Contains((byte) '\r')).IsFalse();
    }
}
