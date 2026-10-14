[TUnit.Core.Executors.STAThreadExecutor]
public class FormsAppTests
{
    /// <summary>
    /// The module initializer configured the process, and a second configuration would throw from
    /// SetCompatibleTextRenderingDefault once a window existed, so it is left as it was.
    /// </summary>
    [Test]
    public async Task ASecondConfigurationLeavesTheFirst()
    {
        using var form = new TestForm();
        form.ShowParked();

        await Assert.That(FormsApp.Configure(HighDpiMode.PerMonitorV2, SystemColorMode.Dark)).IsFalse();
    }
}
