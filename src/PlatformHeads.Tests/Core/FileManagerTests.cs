/// <summary>
/// What the file manager is asked to open. Nothing here starts one: the tests answer what would be
/// opened, and what is done with a path that is not there.
/// </summary>
public class FileManagerTests :
    IDisposable
{
    [Test]
    public async Task AFileThatIsThereIsSelected()
    {
        var file = Path.Combine(directory, "sample.verified.txt");
        await File.WriteAllTextAsync(file, "");

        var target = FileManager.RevealTarget(file);

        await Assert.That(target!.Value.Target).IsEqualTo(file);
        await Assert.That(target.Value.Select).IsTrue();
    }

    /// <summary>
    /// Explorer opens Documents when asked to select a path that is not there, and <c>open -R</c>
    /// fails. The folder the file is about to be written into is the useful answer.
    /// </summary>
    [Test]
    public async Task AFileThatIsNotThereYetIsItsFolder()
    {
        var target = FileManager.RevealTarget(Path.Combine(directory, "new.verified.txt"));

        await Assert.That(target!.Value.Target).IsEqualTo(directory);
        await Assert.That(target.Value.Select).IsFalse();
    }

    [Test]
    public async Task AFileWithNoFolderEitherIsNothing()
    {
        var missing = Path.Combine(directory, "gone", "new.verified.txt");

        await Assert.That(FileManager.RevealTarget(missing)).IsNull();
        await Assert.That(FileManager.TryReveal(missing, out var error)).IsTrue();
        await Assert.That(error).IsNull();
    }

    /// <summary>
    /// A folder deleted since an app last looked opens nothing, and is not made again.
    /// </summary>
    [Test]
    public async Task AFolderThatIsGoneIsNotOpenedOrMade()
    {
        var gone = Path.Combine(directory, "gone");

        await Assert.That(FileManager.TryOpen(gone, out var error)).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(Directory.Exists(gone)).IsFalse();
    }

    /// <summary>
    /// A folder is started as its own path through the shell, so it takes the folder's default
    /// verb and opens in a file manager that replaces Explorer, which explorer.exe by name would not.
    /// </summary>
    [Test]
    public async Task AFolderOnWindowsOpensThroughTheShell()
    {
        var start = FileManager.WindowsStart(directory, false);

        await Assert.That(start.FileName).IsEqualTo(directory);
        await Assert.That(start.Arguments).IsEmpty();
        await Assert.That(start.UseShellExecute).IsTrue();
    }

    /// <summary>
    /// Selecting a file has no shell verb, so that alone is Explorer's.
    /// </summary>
    [Test]
    public async Task AFileOnWindowsIsSelectedByExplorer()
    {
        var file = Path.Combine(directory, "sample.verified.txt");

        var start = FileManager.WindowsStart(file, true);

        await Assert.That(start.FileName).IsEqualTo("explorer.exe");
        await Assert.That(start.Arguments).IsEqualTo($"/select,\"{file}\"");
        await Assert.That(start.UseShellExecute).IsTrue();
    }

    public FileManagerTests()
    {
        directory = Path.Combine(Path.GetTempPath(), $"FileManagerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
    }

    public void Dispose() =>
        Directory.Delete(directory, true);

    readonly string directory;
}
