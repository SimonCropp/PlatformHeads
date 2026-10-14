namespace PlatformHeads;

/// <summary>
/// Finds a head's native library under runtimes/{rid}/native.
/// <para>
/// The default probing rules only consult that layout for natives that arrived through a NuGet
/// package and are recorded in deps.json. A head ships them as plain content, both in its
/// repository and inside a runtime agnostic tool package, so it resolves them itself.
/// </para>
/// </summary>
public static class NativeResolver
{
    static readonly Lock gate = new();
    static readonly Dictionary<Assembly, string> registered = [];

    /// <summary>
    /// Resolves <paramref name="library"/> for the P/Invokes of <paramref name="assembly"/>. Again
    /// for the same pair does nothing; another library for an assembly already registered throws,
    /// since the runtime takes one resolver an assembly.
    /// <para>
    /// Tests call this from parallel threads. The lock makes a second caller wait until the
    /// resolver is set: with only a flag, set before it, BuildMonitor's second caller went straight
    /// on to a P/Invoke, which fell back to the default probing and threw DllNotFoundException.
    /// </para>
    /// </summary>
    /// <param name="assembly">The assembly whose P/Invokes name the library.</param>
    /// <param name="library">The name the P/Invokes give, with no prefix or extension.</param>
    public static void Register(Assembly assembly, string library)
    {
        lock (gate)
        {
            if (registered.TryGetValue(assembly, out var existing))
            {
                if (existing == library)
                {
                    return;
                }

                throw new InvalidOperationException($"{assembly.GetName().Name} already resolves {existing}, so it cannot resolve {library} too.");
            }

            NativeLibrary.SetDllImportResolver(assembly, Resolve);
            registered.Add(assembly, library);
        }

        nint Resolve(string name, Assembly caller, DllImportSearchPath? searchPath)
        {
            if (name != library)
            {
                return nint.Zero;
            }

            foreach (var candidate in Candidates(library))
            {
                if (File.Exists(candidate) &&
                    NativeLibrary.TryLoad(candidate, out var handle))
                {
                    return handle;
                }
            }

            // Zero hands back to the default resolver, so a native sitting beside the executable,
            // or on the system path, still works.
            return nint.Zero;
        }
    }

    /// <summary>
    /// The shipped binary of <paramref name="library"/> for the current runtime, or false when none
    /// is shipped for it, such as on linux-musl.
    /// </summary>
    public static bool TryFind(string library, [NotNullWhen(true)] out string? path)
    {
        foreach (var candidate in Candidates(library))
        {
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }
        }

        path = null;
        return false;
    }

    static IEnumerable<string> Candidates(string library)
    {
        var root = AppContext.BaseDirectory;
        var file = FileName(library);
        foreach (var rid in Rids(RuntimeInformation.RuntimeIdentifier))
        {
            yield return Path.Combine(root, "runtimes", rid, "native", file);
        }

        yield return Path.Combine(root, file);
    }

    /// <summary>
    /// The runtimes/{rid} directories probed, in order, for a process whose runtime identifier is
    /// <paramref name="runtimeIdentifier"/>.
    /// </summary>
    public static IEnumerable<string> Rids(string runtimeIdentifier)
    {
        // The exact RID first. On Alpine that is linux-musl-x64, which is not shipped, so the probe
        // simply misses rather than loading a glibc binary and hard crashing.
        yield return runtimeIdentifier;

        // And nothing else, or the synthesised RID below undoes that: linux-{arch} is the glibc
        // build. A load that fails is reported, but one that succeeded would leave a crash to come
        // later, in whichever call first met a libc that is not the one the library was built
        // against.
        if (runtimeIdentifier.Contains("-musl-", StringComparison.Ordinal))
        {
            yield break;
        }

        var architecture = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => null
        };

        if (architecture is null)
        {
            yield break;
        }

        if (OperatingSystem.IsWindows())
        {
            yield return $"win-{architecture}";
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return $"osx-{architecture}";
        }
        else if (OperatingSystem.IsLinux())
        {
            yield return $"linux-{architecture}";
        }
    }

    /// <summary>
    /// The file <paramref name="library"/> is on this platform.
    /// </summary>
    public static string FileName(string library)
    {
        if (OperatingSystem.IsWindows())
        {
            return $"{library}.dll";
        }

        if (OperatingSystem.IsMacOS())
        {
            return $"lib{library}.dylib";
        }

        return $"lib{library}.so";
    }
}
