/// <summary>
/// The screen built from a <see cref="Counter"/>, saying when it was built so a rebuild by the
/// clock can be told from the screen before.
/// </summary>
record CounterScreen(Counter State, DateTimeOffset At);
