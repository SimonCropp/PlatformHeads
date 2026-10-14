namespace PlatformHeads;

/// <summary>
/// The one mutable reference in an app whose state is immutable. The frame loop reads it every
/// frame while listeners and pollers write to it from their own threads, so both go through here.
/// <para>
/// Reads take no lock. A state is immutable, so a reader only needs the reference, and reading a
/// reference is already atomic; volatile stops the read being hoisted out of the loop. That is not
/// an optimisation. A mutation can hold the gate for as long as its work takes, which in
/// DiffEngine's viewer is a cross process mutex waited on for up to ten seconds, and a loop that
/// queued behind it stopped pumping long enough for Windows to paint the window "Not Responding".
/// </para>
/// </summary>
public sealed class SessionHost<TState>(TState initial)
    where TState : class
{
    readonly Lock gate = new();
    volatile TState state = initial;
    bool mutating;

    public TState State => state;

    /// <summary>
    /// Raised after a mutation swaps in another state, on the thread that made it and outside the
    /// gate. A mutation handing back the state it was given raises nothing: a frame loop idles
    /// until this says there is something new to draw, and the loop's own mutation of every frame
    /// usually changes nothing, so raising for it would never let the loop idle.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Serialised, so two writers cannot compute from the same starting state and one lose.
    /// <para>
    /// <paramref name="change"/> may not mutate again while it runs. The gate is reentrant, so a
    /// nested call does not deadlock: it swaps in its own state, and then the outer call swaps in
    /// the one it had already computed, silently undoing it. An action calling back in here instead
    /// of returning what it wanted is how that gets written, and what it leaves behind is a button
    /// that does nothing, a long way from the line that caused it.
    /// </para>
    /// </summary>
    public TState Mutate(Func<TState, TState> change)
    {
        TState next;
        bool changed;
        lock (gate)
        {
            if (mutating)
            {
                throw new InvalidOperationException("A mutation cannot mutate. Return the state you want, or take the work off this thread.");
            }

            mutating = true;
            try
            {
                next = change(state);
                changed = !ReferenceEquals(next, state);
                state = next;
            }
            finally
            {
                mutating = false;
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }

        return next;
    }
}
