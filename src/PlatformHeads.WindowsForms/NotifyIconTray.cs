namespace PlatformHeads;

/// <summary>
/// The Windows tray icon. The menu is rebuilt from the last <see cref="TrayModel{TIcon}" /> each time
/// it opens, which is when its contents matter, rather than every time a model arrives: see
/// <see cref="MenuOnOpening" />.
/// <para>
/// Its events are raised on the thread that made it, as the app's loop pumps messages, and
/// <see cref="Poll" /> drains what they set.
/// </para>
/// </summary>
public sealed class NotifyIconTray<TIcon> : ITray<TIcon>
    where TIcon : struct, Enum
{
    /// <summary>
    /// NotifyIcon.Text throws past this many characters.
    /// </summary>
    public const int TooltipLimit = 127;

    readonly NotifyIconOptions<TIcon> options;
    readonly NotifyIcon icon;
    readonly ContextMenuStrip menu = new();
    TrayModel<TIcon>? model;
    TIcon? shown;
    string? clicked;
    bool iconClicked;
    string? announced;
    string? clickedNotification;

    NotifyIconTray(NotifyIconOptions<TIcon> options)
    {
        this.options = options;
        options.Theme?.Invoke(menu);
        MenuOnOpening.Rebuild(menu, Build);
        icon = new()
        {
            Text = Fit(options.Title),
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.MouseClick += (_, arguments) =>
        {
            if (arguments.Button == MouseButtons.Left)
            {
                iconClicked = true;
            }
        };
        icon.DoubleClick += (_, _) => iconClicked = true;
        icon.BalloonTipClicked += (_, _) => clickedNotification = announced ?? "";
    }

    /// <summary>
    /// The icon, shown at once with the app's title and no picture until the first model. Null with
    /// an <paramref name="error" /> when Windows would not make one.
    /// </summary>
    public static NotifyIconTray<TIcon>? Open(NotifyIconOptions<TIcon> options, out string? error)
    {
        error = null;
        try
        {
            return new(options);
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return null;
        }
    }

    public void Apply(TrayModel<TIcon> next)
    {
        model = next;
        if (!Nullable.Equals(shown, next.Icon))
        {
            shown = next.Icon;
            icon.Icon = options.Icon(next.Icon);
        }

        var tooltip = Fit(next.Tooltip);
        if (icon.Text != tooltip)
        {
            icon.Text = tooltip;
        }
    }

    static string Fit(string tooltip) =>
        tooltip.Length > TooltipLimit ? tooltip[..TooltipLimit] : tooltip;

    /// <summary>
    /// The key is kept rather than passed through the click, because Windows raises
    /// BalloonTipClicked with nothing about which balloon was clicked, and a second can pop over
    /// the first before either is.
    /// </summary>
    public void Notify(TrayNotification notification)
    {
        announced = notification.Key ?? "";
        var balloonIcon = notification.Kind == TrayNotificationKind.Info ? ToolTipIcon.Info : ToolTipIcon.Error;
        icon.ShowBalloonTip(5000, notification.Title, notification.Message, balloonIcon);
    }

    void Build(ToolStripItemCollection items)
    {
        options.Theme?.Invoke(menu);
        if (model is null)
        {
            return;
        }

        foreach (var item in model.Items)
        {
            items.Add(TrayMenuItems.Create(item, options, _ => clicked = _));
        }
    }

    public TrayInput Poll()
    {
        var input = new TrayInput(clicked, iconClicked, clickedNotification);
        clicked = null;
        iconClicked = false;
        clickedNotification = null;
        return input;
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
        menu.Dispose();
    }
}
