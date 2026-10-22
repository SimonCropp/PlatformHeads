/// <summary>
/// The two messages a logoff or a shutdown sends every top level window, sent to the one kept for
/// hearing them.
/// <para>
/// Sent from the thread that made the window, which calls its procedure directly, so no message
/// loop is needed and nothing here awaits between making the window and destroying it.
/// </para>
/// </summary>
public class SessionEndWindowTests
{
    [Test]
    public async Task TheSessionEndingIsHeard()
    {
        var heard = 0;
        nint query;
        using (var window = new SessionEndWindow(() => heard++))
        {
            query = SendMessage(window.Handle, queryEndSession, 0, logoff);
            SendMessage(window.Handle, endSession, 1, logoff);
        }

        // Agreed to. Refusing would hold up the logoff, and nothing here is worth that
        await Assert.That(query).IsEqualTo(1);
        await Assert.That(heard).IsEqualTo(1);
    }

    /// <summary>
    /// Another application refused the query, so everything that agreed is told the session goes
    /// on, and an app that wound down for it would carry on wound down.
    /// </summary>
    [Test]
    public async Task ASessionThatDidNotEndAfterAllIsNotHeard()
    {
        var heard = 0;
        using (var window = new SessionEndWindow(() => heard++))
        {
            SendMessage(window.Handle, queryEndSession, 0, logoff);
            SendMessage(window.Handle, endSession, 0, logoff);
        }

        await Assert.That(heard).IsEqualTo(0);
    }

    /// <summary>
    /// Told, and not thrown out of the window procedure into WinForms' exception dialog, which
    /// during a logoff holds the session open with nobody to answer it.
    /// </summary>
    [Test]
    public async Task WhatTheEndingThrowsIsHandedOver()
    {
        Exception? failed = null;
        using (var window = new SessionEndWindow(() => throw new InvalidOperationException("staging failed"), _ => failed = _))
        {
            SendMessage(window.Handle, endSession, 1, logoff);
        }

        await Assert.That(failed).IsTypeOf<InvalidOperationException>();
    }

    [DllImport("user32.dll")]
    static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);

    const int queryEndSession = 0x0011;
    const int endSession = 0x0016;
    static readonly nint logoff = unchecked((int) 0x80000000);
}
