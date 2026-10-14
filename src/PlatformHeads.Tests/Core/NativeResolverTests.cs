/// <summary>
/// Which runtimes/{rid} directories the resolver probes, which is what decides whose build of the
/// native library a process loads. Nothing here loads one, so these run on every platform.
/// </summary>
public class NativeResolverTests
{
    /// <summary>
    /// The probe for a musl RID is that RID alone. The linux-{arch} candidate after it names the
    /// glibc build, which a musl process must not load.
    /// </summary>
    [Test]
    [Arguments("linux-musl-x64")]
    [Arguments("linux-musl-arm64")]
    public async Task MuslProbesItsOwnRidAndNothingElse(string runtimeIdentifier)
    {
        var rids = NativeResolver.Rids(runtimeIdentifier).ToList();
        await Assert.That(rids).IsEquivalentTo([runtimeIdentifier]);
    }

    /// <summary>
    /// Everywhere else the synthesised RID still follows the framework's own.
    /// </summary>
    [Test]
    public async Task OtherRidsStillFallBackToTheSynthesisedRid()
    {
        var rids = NativeResolver.Rids("some-rid").ToList();
        await Assert.That(rids.Count).IsGreaterThan(1);
        await Assert.That(rids[0]).IsEqualTo("some-rid");
    }

    [Test]
    public async Task TheFileIsNamedForThePlatform()
    {
        var file = NativeResolver.FileName("sample_ui");
        if (OperatingSystem.IsWindows())
        {
            await Assert.That(file).IsEqualTo("sample_ui.dll");
        }
        else if (OperatingSystem.IsMacOS())
        {
            await Assert.That(file).IsEqualTo("libsample_ui.dylib");
        }
        else
        {
            await Assert.That(file).IsEqualTo("libsample_ui.so");
        }
    }

    [Test]
    public async Task ALibraryNotShippedIsNotFound()
    {
        var found = NativeResolver.TryFind("no_such_library", out var path);

        await Assert.That(found).IsFalse();
        await Assert.That(path).IsNull();
    }

    [Test]
    public async Task AShippedLibraryIsFoundUnderItsRid()
    {
        var library = $"resolver_{Guid.NewGuid():N}";
        var directory = Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native");
        var file = Path.Combine(directory, NativeResolver.FileName(library));
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(file, []);
        try
        {
            var found = NativeResolver.TryFind(library, out var path);

            await Assert.That(found).IsTrue();
            await Assert.That(path).IsEqualTo(file);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// From parallel threads too, as tests call it: every caller is through once the resolver is
    /// set, and the runtime is asked to set it once.
    /// </summary>
    [Test]
    public async Task RegisteringAgainDoesNothing()
    {
        var assembly = Loaded();

        Parallel.For(0, 16, _ => NativeResolver.Register(assembly, "sample_ui"));

        await Assert.That(() => NativeResolver.Register(assembly, "sample_ui")).ThrowsNothing();
    }

    /// <summary>
    /// The runtime takes one resolver an assembly, so a second library would replace nothing and
    /// fail later, in the P/Invoke.
    /// </summary>
    [Test]
    public async Task AnotherLibraryForTheSameAssemblyIsRefused()
    {
        var assembly = Loaded();
        NativeResolver.Register(assembly, "sample_ui");

        await Assert.That(() => NativeResolver.Register(assembly, "other_ui")).Throws<InvalidOperationException>();
    }

    /// <summary>
    /// An assembly of its own for each test, since a resolver once set on one stays for the process:
    /// the kernel loaded again, into a context of its own.
    /// </summary>
    static Assembly Loaded() =>
        new AssemblyLoadContext($"Resolved{Guid.NewGuid():N}", isCollectible: true)
            .LoadFromAssemblyPath(typeof(NativeResolver).Assembly.Location);
}
