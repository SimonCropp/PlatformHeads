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

    public FileManagerTests()
    {
        directory = Path.Combine(Path.GetTempPath(), $"FileManagerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
    }

    public void Dispose() =>
        Directory.Delete(directory, true);

    readonly string directory;
}
