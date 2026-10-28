namespace PlatformHeads;

/// <summary>
/// The Linux tray: a StatusNotifierItem on the session bus, with its menu exported as a
/// com.canonical.dbusmenu, which is what KDE, Cinnamon and XFCE show, and what GNOME shows once the
/// AppIndicator extension is installed. Pure managed: no GTK and no libappindicator, which would
/// need a GTK main loop on the app's thread.
/// <para>
/// The bus calls back on threads of its own. What the user does is queued and handed over by
/// <see cref="Poll" />, so the app's loop never runs inside a callback.
/// </para>
/// </summary>
public sealed class StatusNotifierTray<TIcon> : ITray<TIcon>
    where TIcon : struct, Enum
{
    public const string WatcherName = "org.kde.StatusNotifierWatcher";
    static readonly TimeSpan connectTimeout = TimeSpan.FromSeconds(5);

    readonly DBusConnection connection;
    readonly StatusNotifierOptions<TIcon> options;
    readonly ConcurrentQueue<TrayInput> events = new();
    readonly StatusNotifierItemHandler<TIcon> item;
    readonly DbusMenuHandler menu;
    TIcon? shownIcon;
    string? shownTooltip;

    StatusNotifierTray(string address, StatusNotifierOptions<TIcon> options)
    {
        this.options = options;
        connection = new(address);
        item = new(options, IconClicked);
        menu = new(options.Glyph, ItemClicked);
    }

    /// <summary>
    /// The item's own name on the bus, which it registers with the watcher.
    /// </summary>
    public string BusName { get; } = $"org.kde.StatusNotifierItem-{Environment.ProcessId}-1";

    /// <summary>
    /// The tray on the session bus. Null with an <paramref name="error" /> when there is no tray to
    /// put an icon in, which on Linux is ordinary: a stock GNOME has no watcher.
    /// </summary>
    public static StatusNotifierTray<TIcon>? Open(StatusNotifierOptions<TIcon> options, out string? error) =>
        Open(options, Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS"), out error);

    /// <summary>
    /// The tray on the bus at <paramref name="address" />, which a test points at a bus of its own.
    /// </summary>
    public static StatusNotifierTray<TIcon>? Open(StatusNotifierOptions<TIcon> options, string? address, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(address))
        {
            error = "No session bus (DBUS_SESSION_BUS_ADDRESS is not set), so there is no tray to put an icon in.";
            return null;
        }

        var tray = new StatusNotifierTray<TIcon>(address, options);
        try
        {
            var connect = tray.Connect();
            if (!connect.Wait(connectTimeout))
            {
                error = "The session bus did not answer.";
                tray.Dispose();
                return null;
            }

            if (!connect.Result)
            {
                error = $"Nothing on the session bus owns {WatcherName}. GNOME needs the AppIndicator extension for a tray.";
                tray.Dispose();
                return null;
            }

            return tray;
        }
        catch (Exception exception)
        {
            error = exception.InnerException?.Message ?? exception.Message;
            tray.Dispose();
            return null;
        }
    }

    async Task<bool> Connect()
    {
        await connection.ConnectAsync();
        connection.AddMethodHandler(item);
        connection.AddMethodHandler(menu);
        await connection.RequestNameAsync(BusName, RequestNameOptions.None);
        if (!await HasOwner(WatcherName))
        {
            return false;
        }

        await Register();
        // A watcher that restarts, as the panel does when it crashes or is reconfigured, needs
        // the item registered again.
        await connection.WatchSignalAsync(
            "org.freedesktop.DBus",
            "/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "NameOwnerChanged",
            ReadNameOwnerChanged,
            OnNameOwnerChanged,
            ObserverFlags.None,
            false);
        return true;
    }

    static (string Name, string NewOwner) ReadNameOwnerChanged(Message message, object? state)
    {
        var reader = message.GetBodyReader();
        var name = reader.ReadString();
        reader.ReadString();
        var newOwner = reader.ReadString();
        return (name, newOwner);
    }

    /// <summary>
    /// Asks whether the notification is the watch ending before asking why: the reason throws for
    /// any other notification, every signal among them, so read first it threw for every name
    /// that changed hands on the bus, and the item never registered with a panel that restarted.
    /// </summary>
    void OnNameOwnerChanged(Notification<(string Name, string NewOwner)> notification)
    {
        if (notification.IsCompletion)
        {
            options.Failed?.Invoke(notification.CompletionReason, "NameOwnerChanged");
            return;
        }

        if (notification is {HasValue: true, Value.Name: WatcherName} &&
            notification.Value.NewOwner.Length > 0)
        {
            _ = Register();
        }
    }

    Task<bool> HasOwner(string name)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader("org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus", "NameHasOwner", "s");
        writer.WriteString(name);
        var message = writer.CreateMessage();
        writer.Dispose();
        return connection.CallMethodAsync(message, ReadBool);
    }

    static bool ReadBool(Message message, object? state) =>
        message.GetBodyReader().ReadBool();

    async Task Register()
    {
        try
        {
            var writer = connection.GetMessageWriter();
            writer.WriteMethodCallHeader(WatcherName, "/StatusNotifierWatcher", WatcherName, "RegisterStatusNotifierItem", "s");
            writer.WriteString(BusName);
            var message = writer.CreateMessage();
            writer.Dispose();
            await connection.CallMethodAsync(message);
        }
        catch (Exception exception)
        {
            options.Failed?.Invoke(exception, "Registering the tray item");
        }
    }

    public void Apply(TrayModel<TIcon> model)
    {
        if (!Nullable.Equals(shownIcon, model.Icon))
        {
            shownIcon = model.Icon;
            item.Icon = model.Icon;
            Signal(StatusNotifierItemHandler<TIcon>.ObjectPath, StatusNotifierItemHandler<TIcon>.Interface, "NewIcon", "", null);
            Signal(StatusNotifierItemHandler<TIcon>.ObjectPath, StatusNotifierItemHandler<TIcon>.Interface, "NewStatus", "s", item.StatusText);
        }

        if (shownTooltip != model.Tooltip)
        {
            shownTooltip = model.Tooltip;
            item.Tooltip = model.Tooltip;
            Signal(StatusNotifierItemHandler<TIcon>.ObjectPath, StatusNotifierItemHandler<TIcon>.Interface, "NewToolTip", "", null);
        }

        if (menu.Update(model.Items))
        {
            Signal(DbusMenuHandler.ObjectPath, DbusMenuHandler.Interface, "LayoutUpdated", "ui", menu.Revision);
        }
    }

    /// <summary>
    /// The three signal shapes used here: no body, one string, or a revision and the root id.
    /// </summary>
    void Signal(string path, string interfaceName, string member, string signature, object? argument)
    {
        try
        {
            var writer = connection.GetMessageWriter();
            writer.WriteSignalHeader(null, path, interfaceName, member, signature);
            switch (argument)
            {
                case string text:
                    writer.WriteString(text);
                    break;
                case uint revision:
                    writer.WriteUInt32(revision);
                    writer.WriteInt32(0);
                    break;
            }

            var message = writer.CreateMessage();
            writer.Dispose();
            connection.TrySendMessage(message);
        }
        catch (Exception exception)
        {
            options.Failed?.Invoke(exception, $"Signal {member}");
        }
    }

    public void Notify(TrayNotification notification) =>
        DesktopNotification.Show(options.Id, notification);

    void IconClicked() =>
        events.Enqueue(new(IconClicked: true));

    void ItemClicked(string id) =>
        events.Enqueue(new(ClickedItem: id));

    public TrayInput Poll()
    {
        if (!events.TryDequeue(out var first))
        {
            return new();
        }

        // Everything queued since the last poll folds into one report: a click is a click.
        var result = first;
        while (events.TryDequeue(out var next))
        {
            result = new(
                next.ClickedItem ?? result.ClickedItem,
                result.IconClicked || next.IconClicked,
                next.ClickedNotification ?? result.ClickedNotification);
        }

        return result;
    }

    public void Dispose() =>
        connection.Dispose();
}
