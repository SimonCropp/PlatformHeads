namespace PlatformHeads;

/// <summary>
/// The desktop's own start-at-login mechanism. <see cref="Exists" /> reads what was written rather
/// than an app's setting, so an options checkbox says what will actually happen at the next login.
/// </summary>
public interface IRunAtLogin
{
    bool Exists();

    void Set(bool enabled);
}
