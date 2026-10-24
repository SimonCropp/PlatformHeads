namespace PlatformHeads;

/// <summary>
/// The com.canonical.dbusmenu object the tray item points at. The host asks for the layout, draws
/// the menu itself, and reports a click as an event.
/// </summary>
sealed class DbusMenuHandler(Func<string, int, byte[]?>? glyph, Action<string> clicked) : IPathMethodHandler
{
    public const string Interface = "com.canonical.dbusmenu";
    public const string ObjectPath = "/MenuBar";
    const string properties = "org.freedesktop.DBus.Properties";
    static readonly string[] propertyNames = ["Version", "TextDirection", "Status", "IconThemePath"];

    DbusMenuLayout layout = DbusMenuLayout.Empty;
    IReadOnlyList<TrayMenuItem> items = [];

    public uint Revision { get; private set; } = 1;

    public string Path => ObjectPath;

    public bool HandlesChildPaths => false;

    /// <summary>
    /// Swaps in a new menu when it differs from the last. True when the host should be told. The
    /// items are compared before a layout is built, because each node reads its glyph as it is
    /// made, and an app may hand over its model every time it rebuilds anything.
    /// </summary>
    public bool Update(IReadOnlyList<TrayMenuItem> next)
    {
        if (next.SequenceEqual(items))
        {
            return false;
        }

        items = next;
        var built = DbusMenuLayout.Build(next, glyph);
        if (built.Signature == layout.Signature)
        {
            return false;
        }

        layout = built;
        Revision++;
        return true;
    }

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
            case (Interface, "GetLayout"):
            {
                var reader = request.GetBodyReader();
                var parentId = reader.ReadInt32();
                var depth = reader.ReadInt32();
                var writer = context.CreateReplyWriter("u(ia{sv}av)");
                writer.WriteUInt32(Revision);
                var node = layout.Find(parentId) ?? layout.Root;
                WriteNode(ref writer, node, depth);
                context.Reply(writer.CreateMessage());
                writer.Dispose();
                return default;
            }
            case (Interface, "GetGroupProperties"):
            {
                var reader = request.GetBodyReader();
                var ids = reader.ReadArrayOfInt32();
                var writer = context.CreateReplyWriter("a(ia{sv})");
                var start = writer.WriteArrayStart(DBusType.Struct);
                foreach (var id in ids.Length == 0 ? layout.All.Select(_ => _.Id).ToArray() : ids)
                {
                    if (layout.Find(id) is not { } node)
                    {
                        continue;
                    }

                    writer.WriteStructureStart();
                    writer.WriteInt32(node.Id);
                    WriteProperties(ref writer, node);
                }

                writer.WriteArrayEnd(start);
                context.Reply(writer.CreateMessage());
                writer.Dispose();
                return default;
            }
            case (Interface, "GetProperty"):
            {
                var reader = request.GetBodyReader();
                var id = reader.ReadInt32();
                var name = reader.ReadString();
                if (layout.Find(id) is not { } node ||
                    !node.PropertyNames.Contains(name))
                {
                    context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", $"No property {name} on {id}");
                    return default;
                }

                var writer = context.CreateReplyWriter("v");
                WriteItemProperty(ref writer, node, name);
                context.Reply(writer.CreateMessage());
                writer.Dispose();
                return default;
            }
            case (Interface, "Event"):
            {
                var reader = request.GetBodyReader();
                var id = reader.ReadInt32();
                var eventId = reader.ReadString();
                if (eventId == "clicked" &&
                    layout.Find(id) is { ItemId: { } itemId })
                {
                    clicked(itemId);
                }

                Empty(context);
                return default;
            }
            case (Interface, "EventGroup"):
            {
                var writer = context.CreateReplyWriter("ai");
                writer.WriteArray(Array.Empty<int>());
                context.Reply(writer.CreateMessage());
                writer.Dispose();
                return default;
            }
            case (Interface, "AboutToShow"):
            {
                var writer = context.CreateReplyWriter("b");
                writer.WriteBool(false);
                context.Reply(writer.CreateMessage());
                writer.Dispose();
                return default;
            }
            case (Interface, "AboutToShowGroup"):
            {
                var writer = context.CreateReplyWriter("aiai");
                writer.WriteArray(Array.Empty<int>());
                writer.WriteArray(Array.Empty<int>());
                context.Reply(writer.CreateMessage());
                writer.Dispose();
                return default;
            }
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

    static void WriteProperty(ref MessageWriter writer, string name)
    {
        switch (name)
        {
            case "Version":
                writer.WriteVariantUInt32(3);
                break;
            case "TextDirection":
                writer.WriteVariantString("ltr");
                break;
            case "Status":
                writer.WriteVariantString("normal");
                break;
            default:
                // IconThemePath
                writer.WriteSignature("as");
                writer.WriteArray(Array.Empty<string>());
                break;
        }
    }

    /// <summary>
    /// (ia{sv}av): the node, its properties, and its children as variants of the same shape.
    /// </summary>
    static void WriteNode(ref MessageWriter writer, DbusMenuNode node, int depth)
    {
        writer.WriteStructureStart();
        writer.WriteInt32(node.Id);
        WriteProperties(ref writer, node);
        var children = writer.WriteArrayStart(DBusType.Variant);
        if (depth != 0)
        {
            foreach (var child in node.Children)
            {
                writer.WriteSignature("(ia{sv}av)");
                WriteNode(ref writer, child, depth < 0 ? depth : depth - 1);
            }
        }

        writer.WriteArrayEnd(children);
    }

    static void WriteProperties(ref MessageWriter writer, DbusMenuNode node)
    {
        var start = writer.WriteDictionaryStart();
        foreach (var name in node.PropertyNames)
        {
            writer.WriteDictionaryEntryStart();
            writer.WriteString(name);
            WriteItemProperty(ref writer, node, name);
        }

        writer.WriteDictionaryEnd(start);
    }

    static void WriteItemProperty(ref MessageWriter writer, DbusMenuNode node, string name)
    {
        switch (name)
        {
            case "label":
                writer.WriteVariantString(node.Label);
                break;
            case "enabled":
                writer.WriteVariantBool(node.Enabled);
                break;
            case "visible":
                writer.WriteVariantBool(true);
                break;
            case "type":
                writer.WriteVariantString("separator");
                break;
            case "toggle-type":
                writer.WriteVariantString("checkmark");
                break;
            case "toggle-state":
                writer.WriteVariantInt32(node.Checked == true ? 1 : 0);
                break;
            case "children-display":
                writer.WriteVariantString("submenu");
                break;
            case "icon-data":
                writer.WriteSignature("ay");
                if (node.IconPng is { } icon)
                {
                    writer.WriteArray(icon);
                }
                else
                {
                    writer.WriteArray(Array.Empty<byte>());
                }

                break;
        }
    }
}
