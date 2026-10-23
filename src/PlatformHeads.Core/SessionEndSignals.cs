namespace PlatformHeads;

/// <summary>
/// Hears that a macOS or Linux session is ending, by the signals it ends a process with: SIGTERM,
/// which systemd sends every process of a session it stops, and SIGHUP, which a session's processes
/// get as it hangs up. The Windows session is heard by a window instead, SessionEndWindow, or a
/// form's SessionEnding.
/// <para>
/// Nothing else would. .NET's own handling of either ends the process there and then: AppDomain's
/// ProcessExit is not raised, no finally runs, and whatever a clean exit would have saved went
/// with the process.
/// </para>
/// <para>
/// The process still ends once <c>ending</c> has run, as it would have without this. Kept from
/// ending, it would be killed outright when the session's time ran out.
/// </para>
/// <para>
/// Nothing is registered on Windows, where these are a console's close and shutdown events, which
/// a windowed app is never sent.
/// </para>
/// </summary>
public sealed class SessionEndSignals : IDisposable
{
    readonly Action ending;
    readonly Action<Exception>? failed;
    readonly List<PosixSignalRegistration> registrations = [];
    readonly Lock gate = new();
    bool ended;

    /// <param name="ending">
    /// Run on a thread pool thread, once, however many of the signals arrive, so whatever it
    /// touches has to be safe to touch from there while the app's own thread goes on.
    /// </param>
    /// <param name="failed">
    /// Told what <paramref name="ending"/> threw. Thrown out of the handler it would end the
    /// process with nothing logged.
    /// </param>
    /// <param name="signals">
    /// SIGTERM and SIGHUP unless given others. Others are for a test, which can send a signal
    /// whose default does not end the process.
    /// </param>
    public SessionEndSignals(Action ending, Action<Exception>? failed = null, IReadOnlyList<PosixSignal>? signals = null)
    {
        this.ending = ending;
        this.failed = failed;
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var signal in signals ?? [PosixSignal.SIGTERM, PosixSignal.SIGHUP])
        {
            registrations.Add(PosixSignalRegistration.Create(signal, _ => End()));
        }
    }

    /// <summary>
    /// A second signal waits for the first one's ending rather than returning at once. systemd
    /// sends a session's processes SIGHUP straight after SIGTERM, and a handler that returned
    /// would let .NET end the process with the first one's ending half done.
    /// </summary>
    void End()
    {
        lock (gate)
        {
            if (ended)
            {
                return;
            }

            ended = true;
            try
            {
                ending();
            }
            catch (Exception exception)
            {
                failed?.Invoke(exception);
            }
        }
    }

    public void Dispose()
    {
        foreach (var registration in registrations)
        {
            registration.Dispose();
        }

        registrations.Clear();
    }
}
