using Tmds.DBus.Protocol;

/// <summary>
/// What a desktop's panel is to a StatusNotifierItem: it owns the watcher's name, hears the item
/// register, and reads and clicks it as a host does.
/// <para>
/// Every call yields to the pool before it returns. Tmds completes a call's task on the
/// connection's receive loop, and what awaited it runs there, inline, so a test that went on to
/// open the tray, which blocks its thread until the item has registered, blocked this
/// connection's loop with it, and the registration it waited for was never received. A real
/// panel is another process. ForceYielding is no help: it only yields for a task already complete
/// when it is awaited.
/// </para>
/// </summary>
sealed class StandInWatcher : IPathMethodHandler, IDisposable
{
    readonly DBusConnection connection;
    readonly TaskCompletionSource<string> registered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    StandInWatcher(string address) =>
        connection = new(address);

    public string Path => "/StatusNotifierWatcher";

    public bool HandlesChildPaths => false;

    /// <summary>
    /// The bus name the item registered with.
    /// </summary>
    public Task<string> Registered => registered.Task;

    public static async Task<StandInWatcher> Start(string address)
    {
        var watcher = new StandInWatcher(address);
        await watcher.connection.ConnectAsync();
        watcher.connection.AddMethodHandler(watcher);
        await watcher.connection.RequestNameAsync(StatusNotifierTray<TestTrayIcon>.WatcherName, RequestNameOptions.None);
        await Task.Yield();
        return watcher;
    }

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        var request = context.Request;
        if (request.MemberAsString == "RegisterStatusNotifierItem")
        {
            registered.TrySetResult(request.GetBodyReader().ReadString());
        }

        var writer = context.CreateReplyWriter("");
        context.Reply(writer.CreateMessage());
        writer.Dispose();
        return default;
    }

    public async Task<VariantValue> Property(string destination, string path, string interfaceName, string name)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination, path, "org.freedesktop.DBus.Properties", "Get", "ss");
        writer.WriteString(interfaceName);
        writer.WriteString(name);
        var message = writer.CreateMessage();
        writer.Dispose();
        var value = await connection.CallMethodAsync(message, ReadVariant);
        await Task.Yield();
        return value;
    }

    static VariantValue ReadVariant(Message reply, object? state) =>
        reply.GetBodyReader().ReadVariantValue();

    public async Task<VariantValue> MenuProperty(string destination, int id, string name)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination, "/MenuBar", "com.canonical.dbusmenu", "GetProperty", "is");
        writer.WriteInt32(id);
        writer.WriteString(name);
        var message = writer.CreateMessage();
        writer.Dispose();
        var value = await connection.CallMethodAsync(message, ReadVariant);
        await Task.Yield();
        return value;
    }

    /// <summary>
    /// A click on a menu node, as a host reports one.
    /// </summary>
    public async Task Click(string destination, int id)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination, "/MenuBar", "com.canonical.dbusmenu", "Event", "isvu");
        writer.WriteInt32(id);
        writer.WriteString("clicked");
        writer.WriteVariantInt32(0);
        writer.WriteUInt32(0);
        var message = writer.CreateMessage();
        writer.Dispose();
        await connection.CallMethodAsync(message);
        await Task.Yield();
    }

    /// <summary>
    /// A left click on the icon.
    /// </summary>
    public async Task Activate(string destination)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination, "/StatusNotifierItem", "org.kde.StatusNotifierItem", "Activate", "ii");
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        var message = writer.CreateMessage();
        writer.Dispose();
        await connection.CallMethodAsync(message);
        await Task.Yield();
    }

    public void Dispose() =>
        connection.Dispose();
}
