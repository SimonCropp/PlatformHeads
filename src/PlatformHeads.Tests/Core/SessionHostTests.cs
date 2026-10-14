/// <summary>
/// The gate around the one mutable reference.
/// </summary>
public class SessionHostTests
{
    [Test]
    public async Task TheChangeIsWhatComesBack()
    {
        var host = new SessionHost<Counter>(new(0));

        var state = host.Mutate(_ => _ with { Value = 1 });

        await Assert.That(state.Value).IsEqualTo(1);
        await Assert.That(host.State.Value).IsEqualTo(1);
    }

    /// <summary>
    /// The gate is reentrant, so a nested change does not deadlock. It swaps in its own state and
    /// the outer change then swaps in the one it had already computed, undoing it: a quit swapped
    /// in from inside an action is how BuildMonitor's Update button came to do nothing. Loud,
    /// because there is nothing to see when it happens quietly.
    /// </summary>
    [Test]
    public async Task AChangeThatChangesAgainIsRefused()
    {
        var host = new SessionHost<Counter>(new(0));

        await Assert.That(() => host.Mutate(_ =>
            {
                host.Mutate(inner => inner with { Value = 2 });
                return _ with { Value = 1 };
            }))
            .Throws<InvalidOperationException>();
        await Assert.That(host.State.Value).IsEqualTo(0);
    }

    /// <summary>
    /// A refused nested change does not leave the gate shut: the next one through works.
    /// </summary>
    [Test]
    public async Task TheGateSurvivesARefusal()
    {
        var host = new SessionHost<Counter>(new(0));
        try
        {
            host.Mutate(_ =>
            {
                host.Mutate(inner => inner);
                return _;
            });
        }
        catch (InvalidOperationException)
        {
        }

        await Assert.That(host.Mutate(_ => _ with { Value = 1 }).Value).IsEqualTo(1);
    }

    /// <summary>
    /// A frame loop idles until a change is raised, and applies a frame of input itself every time
    /// it wakes, which is usually nothing. Raised for that too, the loop would wake itself.
    /// </summary>
    [Test]
    public async Task OnlyANewStateIsRaised()
    {
        var host = new SessionHost<Counter>(new(0));
        var raised = 0;
        host.Changed += () => raised++;

        host.Mutate(_ => _);
        await Assert.That(raised).IsEqualTo(0);

        host.Mutate(_ => _ with { Value = 1 });
        await Assert.That(raised).IsEqualTo(1);
    }

    /// <summary>
    /// Outside the gate, so a handler may read the state it was raised for, and mutate again.
    /// </summary>
    [Test]
    public async Task AChangeIsRaisedOnceTheGateIsOpen()
    {
        var host = new SessionHost<Counter>(new(0));
        host.Changed += () =>
        {
            if (host.State.Value == 1)
            {
                host.Mutate(_ => _ with { Value = 2 });
            }
        };

        host.Mutate(_ => _ with { Value = 1 });

        await Assert.That(host.State.Value).IsEqualTo(2);
    }

    /// <summary>
    /// Writers on many threads at once each start from what the one before left, so none is lost.
    /// </summary>
    [Test]
    public async Task NoWriterLosesToAnother()
    {
        var host = new SessionHost<Counter>(new(0));

        Parallel.For(0, 1000, _ => host.Mutate(state => state with { Value = state.Value + 1 }));

        await Assert.That(host.State.Value).IsEqualTo(1000);
    }
}
