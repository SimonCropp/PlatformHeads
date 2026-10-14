/// <summary>
/// A state for the kernel's tests: immutable, and replaced rather than changed, as an app's is.
/// </summary>
/// <param name="Unseen">Nobody can see the window, so the clock rebuilds nothing.</param>
/// <param name="Spinning">The screen has a spinner, so the clock ticks faster.</param>
record Counter(int Value, bool Unseen = false, bool Spinning = false);
