using Tmds.DBus.Protocol;

/// <summary>
/// What a desktop's panel is to a StatusNotifierItem: it owns the watcher's name, hears the item
/// register, and reads and clicks it as a host does.
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

    public Task<VariantValue> Property(string destination, string path, string interfaceName, string name)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination, path, "org.freedesktop.DBus.Properties", "Get", "ss");
        writer.WriteString(interfaceName);
        writer.WriteString(name);
        var message = writer.CreateMessage();
        writer.Dispose();
        return connection.CallMethodAsync(message, ReadVariant);
    }

    static VariantValue ReadVariant(Message reply, object? state) =>
        reply.GetBodyReader().ReadVariantValue();

    public Task<VariantValue> MenuProperty(string destination, int id, string name)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination, "/MenuBar", "com.canonical.dbusmenu", "GetProperty", "is");
        writer.WriteInt32(id);
        writer.WriteString(name);
        var message = writer.CreateMessage();
        writer.Dispose();
        return connection.CallMethodAsync(message, ReadVariant);
    }

    /// <summary>
    /// A click on a menu node, as a host reports one.
    /// </summary>
    public Task Click(string destination, int id)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination, "/MenuBar", "com.canonical.dbusmenu", "Event", "isvu");
        writer.WriteInt32(id);
        writer.WriteString("clicked");
        writer.WriteVariantInt32(0);
        writer.WriteUInt32(0);
        var message = writer.CreateMessage();
        writer.Dispose();
        return connection.CallMethodAsync(message);
    }

    /// <summary>
    /// A left click on the icon.
    /// </summary>
    public Task Activate(string destination)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination, "/StatusNotifierItem", "org.kde.StatusNotifierItem", "Activate", "ii");
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        var message = writer.CreateMessage();
        writer.Dispose();
        return connection.CallMethodAsync(message);
    }

    public void Dispose() =>
        connection.Dispose();
}
