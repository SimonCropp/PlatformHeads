namespace PlatformHeads;

/// <summary>
/// The org.kde.StatusNotifierItem object: properties the host reads, and a few methods it calls.
/// Variants with a structure inside are written by hand as a signature followed by the value,
/// which is what a variant is on the wire.
/// </summary>
sealed class StatusNotifierItemHandler<TIcon>(StatusNotifierOptions<TIcon> options, Action activated) : IPathMethodHandler
    where TIcon : struct, Enum
{
    public const string Interface = "org.kde.StatusNotifierItem";
    public const string ObjectPath = "/StatusNotifierItem";
    const string properties = "org.freedesktop.DBus.Properties";
    static readonly int[] pixmapSizes = [16, 22, 24, 32, 48];
    static readonly string[] propertyNames = ["Category", "Id", "Title", "Status", "WindowId", "IconName", "IconThemePath", "OverlayIconName", "AttentionIconName", "AttentionMovieName", "IconPixmap", "OverlayIconPixmap", "AttentionIconPixmap", "ToolTip", "Menu", "ItemIsMenu"];

    public TIcon Icon { get; set; }

    public string Tooltip { get; set; } = options.Title;

    public string StatusText
    {
        get
        {
            if (options.NeedsAttention?.Invoke(Icon) == true)
            {
                return "NeedsAttention";
            }

            return "Active";
        }
    }

    public string Path => ObjectPath;

    public bool HandlesChildPaths => false;

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        var request = context.Request;
        switch (request.InterfaceAsString, request.MemberAsString)
        {
            case (properties, "Get"):
            {
                var reader = request.GetBodyReader();
                reader.ReadString();
                var name = reader.ReadString();
                if (!propertyNames.Contains(name))
                {
                    context.ReplyError("org.freedesktop.DBus.Error.UnknownProperty", $"No property {name}");
                    return default;
                }

                var writer = context.CreateReplyWriter("v");
                WriteProperty(ref writer, name);
                context.Reply(writer.CreateMessage());
                writer.Dispose();
                return default;
            }
            case (properties, "GetAll"):
            {
                var writer = context.CreateReplyWriter("a{sv}");
                var start = writer.WriteDictionaryStart();
                foreach (var name in propertyNames)
                {
                    writer.WriteDictionaryEntryStart();
                    writer.WriteString(name);
                    WriteProperty(ref writer, name);
                }

                writer.WriteDictionaryEnd(start);
                context.Reply(writer.CreateMessage());
                writer.Dispose();
                return default;
            }
            case (properties, "Set"):
                context.ReplyError("org.freedesktop.DBus.Error.PropertyReadOnly", "Read only");
                return default;
            case (Interface, "Activate"):
                activated();
                Empty(context);
                return default;
            case (Interface, "SecondaryActivate"):
            case (Interface, "ContextMenu"):
            case (Interface, "Scroll"):
            case (Interface, "ProvideXdgActivationToken"):
                Empty(context);
                return default;
            default:
                context.ReplyUnknownMethodError();
                return default;
        }
    }

    static void Empty(MethodContext context)
    {
        var writer = context.CreateReplyWriter("");
        context.Reply(writer.CreateMessage());
        writer.Dispose();
    }

    void WriteProperty(ref MessageWriter writer, string name)
    {
        switch (name)
        {
            case "Category":
                writer.WriteVariantString("ApplicationStatus");
                break;
            case "Id":
                writer.WriteVariantString(options.Id);
                break;
            case "Title":
                writer.WriteVariantString(options.Title);
                break;
            case "Status":
                writer.WriteVariantString(StatusText);
                break;
            case "WindowId":
                writer.WriteVariantInt32(0);
                break;
            case "IconPixmap":
            case "AttentionIconPixmap":
                writer.WriteSignature("a(iiay)");
                WritePixmaps(ref writer, Icon);
                break;
            case "OverlayIconPixmap":
                writer.WriteSignature("a(iiay)");
                WriteNoPixmaps(ref writer);
                break;
            case "ToolTip":
                writer.WriteSignature("(sa(iiay)ss)");
                writer.WriteStructureStart();
                writer.WriteString("");
                WriteNoPixmaps(ref writer);
                writer.WriteString(Tooltip);
                writer.WriteString("");
                break;
            case "Menu":
                writer.WriteVariantObjectPath(DbusMenuHandler.ObjectPath);
                break;
            case "ItemIsMenu":
                writer.WriteVariantBool(false);
                break;
            default:
                // IconName, IconThemePath, OverlayIconName, AttentionIconName, AttentionMovieName
                writer.WriteVariantString("");
                break;
        }
    }

    /// <summary>
    /// ARGB32 in network byte order, one entry per size, which is what the host picks from.
    /// </summary>
    void WritePixmaps(ref MessageWriter writer, TIcon icon)
    {
        var start = writer.WriteArrayStart(DBusType.Struct);
        foreach (var size in pixmapSizes)
        {
            var pixels = options.IconPixels(icon, size);
            if (pixels is null)
            {
                continue;
            }

            writer.WriteStructureStart();
            writer.WriteInt32(size);
            writer.WriteInt32(size);
            writer.WriteArray(pixels);
        }

        writer.WriteArrayEnd(start);
    }

    static void WriteNoPixmaps(ref MessageWriter writer)
    {
        var start = writer.WriteArrayStart(DBusType.Struct);
        writer.WriteArrayEnd(start);
    }
}
