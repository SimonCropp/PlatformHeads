public static class ModuleInitializer
{
    /// <summary>
    /// For every thread, not only the one that runs this: the one argument overload covers only the
    /// calling thread, which leaves every test thread showing WinForms' dialog on the desktop of
    /// whoever is running the tests, and waiting for a click.
    /// </summary>
    [ModuleInitializer]
    public static void Initialize()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, threadScope: false);
        FormsApp.Configure(HighDpiMode.DpiUnaware, SystemColorMode.Classic);
    }
}
