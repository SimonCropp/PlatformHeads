/// <summary>
/// A native library says where the window is on every poll, and each placement passed on is a
/// save, so only one the window has settled at goes on.
/// </summary>
public class PlacementSettlerTests
{
    static readonly WindowPlacement left = new(100, 80, 1000, 640);
    static readonly WindowPlacement moved = new(300, 200, 1000, 640);

    static TimeSpan At(int milliseconds) =>
        TimeSpan.FromMilliseconds(milliseconds);

    /// <summary>
    /// Where it opened, which was where it was left or a first start, so there is nothing to save.
    /// </summary>
    [Test]
    public async Task TheFirstPlacementIsNotNews()
    {
        var settler = new PlacementSettler();
        await Assert.That(settler.Poll(left, At(0))).IsNull();
        await Assert.That(settler.Poll(left, At(5000))).IsNull();
    }

    [Test]
    public async Task AMoveIsPassedOnOnceItHasHeld()
    {
        var settler = new PlacementSettler();
        settler.Poll(left, At(0));
        await Assert.That(settler.Poll(moved, At(1000))).IsNull();
        await Assert.That(settler.Poll(moved, At(1400))).IsNull();
        await Assert.That(settler.Poll(moved, At(1500))).IsEqualTo(moved);
        // Once.
        await Assert.That(settler.Poll(moved, At(3000))).IsNull();
    }

    /// <summary>
    /// A drag is a placement every frame, and only where it ended is saved.
    /// </summary>
    [Test]
    public async Task ADragIsPassedOnWhereItEnds()
    {
        var settler = new PlacementSettler();
        settler.Poll(left, At(0));
        List<WindowPlacement> passed = [];
        for (var frame = 1; frame <= 60; frame++)
        {
            if (settler.Poll(left with { X = 100 + frame * 5 }, At(1000 + frame * 16)) is { } placement)
            {
                passed.Add(placement);
            }
        }

        if (settler.Poll(left with { X = 400 }, At(3000)) is { } settled)
        {
            passed.Add(settled);
        }

        await Assert.That(passed).IsEquivalentTo([left with { X = 400 }]);
    }

    /// <summary>
    /// A hide is how the window is left, and the process may not see the half second out.
    /// </summary>
    [Test]
    public async Task AHideDoesNotWaitForTheMoveToSettle()
    {
        var settler = new PlacementSettler();
        settler.Poll(left, At(0));
        settler.Poll(moved, At(1000));
        settler.Hidden();
        await Assert.That(settler.Poll(moved, At(1016))).IsEqualTo(moved);
    }

    [Test]
    public async Task AHideWithNoMovePassesNothingOn()
    {
        var settler = new PlacementSettler();
        settler.Poll(left, At(0));
        settler.Hidden();
        await Assert.That(settler.Poll(left, At(16))).IsNull();
        // Nor does the hide linger for a move made after it.
        await Assert.That(settler.Poll(moved, At(1000))).IsNull();
    }

    /// <summary>
    /// Before the window is first shown the library has no placement to give, which is not a move.
    /// </summary>
    [Test]
    public async Task NoPlacementIsNotAMove()
    {
        var settler = new PlacementSettler();
        await Assert.That(settler.Poll(null, At(0))).IsNull();
        await Assert.That(settler.Poll(left, At(100))).IsNull();
        await Assert.That(settler.Poll(null, At(2000))).IsNull();
    }
}
