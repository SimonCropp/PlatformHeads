/// <summary>
/// A screen is built when the state is another one or the clock ticks over, and not otherwise. A
/// loop presents sixty times a second, so anything built per frame is built for a window nobody
/// is touching.
/// </summary>
public class ScreenCacheTests
{
    static readonly DateTimeOffset now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    static readonly TimeSpan second = TimeSpan.FromSeconds(1);
    static readonly TimeSpan spin = TimeSpan.FromMilliseconds(250);

    static CounterScreen Build(Counter state, DateTimeOffset at) =>
        new(state, at);

    static TimeSpan? Tick(Counter state, CounterScreen screen)
    {
        if (state.Unseen)
        {
            return null;
        }

        if (state.Spinning)
        {
            return spin;
        }

        return second;
    }

    static ScreenCache<Counter, CounterScreen> Clocked() =>
        new(Build, Tick);

    static TimeSpan Milliseconds(int milliseconds) =>
        TimeSpan.FromMilliseconds(milliseconds);

    [Test]
    public async Task TheSameStateWithinATickIsTheSameScreen()
    {
        var cache = Clocked();
        var state = new Counter(0);

        var first = cache.Get(state, now, out var firstRebuilt);
        var again = cache.Get(state, now + second - Milliseconds(1), out var againRebuilt);

        await Assert.That(firstRebuilt).IsTrue();
        await Assert.That(againRebuilt).IsFalse();
        await Assert.That(ReferenceEquals(first, again)).IsTrue();
    }

    [Test]
    public async Task AnotherStateIsAnotherScreen()
    {
        var cache = Clocked();
        var state = new Counter(0);
        var first = cache.Get(state, now);

        var next = cache.Get(state with { Value = 1 }, now, out var rebuilt);

        await Assert.That(rebuilt).IsTrue();
        await Assert.That(ReferenceEquals(first, next)).IsFalse();
        await Assert.That(next.State.Value).IsEqualTo(1);
    }

    /// <summary>
    /// By reference, not by value: a state is only ever replaced, so an equal one is still another
    /// one, and comparing by value would cost what the cache is there to save.
    /// </summary>
    [Test]
    public async Task AnEqualStateIsStillAnotherState()
    {
        var cache = Clocked();
        var state = new Counter(0);
        cache.Get(state, now);

        cache.Get(state with { }, now, out var rebuilt);

        await Assert.That(rebuilt).IsTrue();
    }

    [Test]
    public async Task TheNextTickRebuilds()
    {
        var cache = Clocked();
        var state = new Counter(0);
        var first = cache.Get(state, now);

        var next = cache.Get(state, now + second, out var rebuilt);

        await Assert.That(rebuilt).IsTrue();
        await Assert.That(ReferenceEquals(first, next)).IsFalse();
    }

    /// <summary>
    /// Built a millisecond before a second turns over, the times on screen are a second behind one
    /// millisecond later.
    /// </summary>
    [Test]
    public async Task TheTickIsOnWholeTicksOfTheClock()
    {
        var cache = Clocked();
        var state = new Counter(0);
        cache.Get(state, now - Milliseconds(1));

        cache.Get(state, now, out var rebuilt);

        await Assert.That(rebuilt).IsTrue();
    }

    [Test]
    public async Task AScreenCanTickFaster()
    {
        var cache = Clocked();
        var state = new Counter(0, Spinning: true);
        cache.Get(state, now);

        cache.Get(state, now + spin, out var rebuilt);

        await Assert.That(rebuilt).IsTrue();
    }

    [Test]
    public async Task NoTickIgnoresTheClock()
    {
        var cache = Clocked();
        var state = new Counter(0, Unseen: true);
        var first = cache.Get(state, now);

        var later = cache.Get(state, now + TimeSpan.FromMinutes(5), out var rebuilt);

        await Assert.That(rebuilt).IsFalse();
        await Assert.That(ReferenceEquals(first, later)).IsTrue();
    }

    /// <summary>
    /// Showing a window is a change of state, which rebuilds with the clock as it is now.
    /// </summary>
    [Test]
    public async Task BeingSeenAgainRebuildsWithTheClock()
    {
        var cache = Clocked();
        var unseen = new Counter(0, Unseen: true);
        cache.Get(unseen, now);

        var seen = cache.Get(unseen with { Unseen = false }, now + TimeSpan.FromMinutes(5), out var rebuilt);

        await Assert.That(rebuilt).IsTrue();
        await Assert.That(seen.At).IsEqualTo(now + TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task UntilTickIsToTheNextWholeTick()
    {
        var cache = Clocked();
        cache.Get(new(0), now + Milliseconds(300));

        await Assert.That(cache.UntilTick(now + Milliseconds(400))).IsEqualTo(Milliseconds(600));
        // Past it, as when the loop wakes late, the tick is due at once.
        await Assert.That(cache.UntilTick(now + Milliseconds(1100))).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task UntilTickKeepsUpWithAFasterTick()
    {
        var cache = Clocked();
        cache.Get(new(0, Spinning: true), now + Milliseconds(100));

        await Assert.That(cache.UntilTick(now + Milliseconds(100))).IsEqualTo(Milliseconds(150));
    }

    [Test]
    public async Task NoTickIsComingBeforeAScreenOrWithNoTick()
    {
        var cache = Clocked();
        await Assert.That(cache.UntilTick(now)).IsNull();

        cache.Get(new(0, Unseen: true), now);
        await Assert.That(cache.UntilTick(now)).IsNull();
    }

    /// <summary>
    /// A tick of nothing would divide by it.
    /// </summary>
    [Test]
    public async Task ATickOfNothingIsRefused()
    {
        var cache = new ScreenCache<Counter, CounterScreen>(Build, (_, _) => TimeSpan.Zero);
        cache.Get(new(0), now);

        await Assert.That(() => cache.UntilTick(now)).Throws<InvalidOperationException>();
    }

    /// <summary>
    /// A screen that shows no time is built from its state alone, and only another state rebuilds
    /// it, however long goes by.
    /// </summary>
    [Test]
    public async Task ATimelessScreenIsRebuiltOnlyByAnotherState()
    {
        var built = 0;
        var cache = new ScreenCache<Counter, CounterScreen>(_ =>
        {
            built++;
            return new(_, default);
        });
        var state = new Counter(0);

        var first = cache.Get(state);
        var later = cache.Get(state, now + TimeSpan.FromHours(1));
        cache.Get(state with { Value = 1 });

        await Assert.That(ReferenceEquals(first, later)).IsTrue();
        await Assert.That(built).IsEqualTo(2);
        await Assert.That(cache.UntilTick(now)).IsNull();
    }

    /// <summary>
    /// Only the state last asked about is kept, so going back to an earlier one builds it again.
    /// </summary>
    [Test]
    public async Task AStateComeBackToIsBuiltAgain()
    {
        var cache = new ScreenCache<Counter, CounterScreen>(_ => new(_, default));
        var state = new Counter(0);
        var first = cache.Get(state);
        cache.Get(state with { Value = 1 });

        var again = cache.Get(state);

        await Assert.That(ReferenceEquals(first, again)).IsFalse();
        await Assert.That(again).IsEqualTo(first);
    }
}
