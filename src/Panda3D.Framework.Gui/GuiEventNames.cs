using Panda3D.Core;

namespace Panda3D.Framework.Gui;

internal static class GuiEventNames
{
    // generated ButtonHandle overloads abort in native code; build the event name by hand
    public static string Press(PGItem item, ButtonHandle button)
        => ButtonQualified(PGItem.GetPressPrefix(), item, button);

    public static string Release(PGItem item, ButtonHandle button)
        => ButtonQualified(PGItem.GetReleasePrefix(), item, button);

    public static string Click(PGButton item, ButtonHandle button)
        => ButtonQualified(PGButton.GetClickPrefix(), item, button);

    public static string Accept(PGEntry item, ButtonHandle button)
        => ButtonQualified(PGEntry.GetAcceptPrefix(), item, button);

    public static string AcceptFailed(PGEntry item, ButtonHandle button)
        => ButtonQualified(PGEntry.GetAcceptFailedPrefix(), item, button);

    static string ButtonQualified(string prefix, PGItem item, ButtonHandle button)
        => prefix + button.GetName() + "-" + item.GetId();
}
