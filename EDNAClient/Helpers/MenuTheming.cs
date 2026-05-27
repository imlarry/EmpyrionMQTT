using System.Windows;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem    = System.Windows.Controls.MenuItem;
using Separator   = System.Windows.Controls.Separator;

namespace EDNAClient.Helpers
{
    // Applies the dark theme defined in App.xaml to a code-built ContextMenu.
    // The MenuItem / Separator styles are injected as implicit-typed entries
    // into the menu's runtime Resources dictionary so they propagate to every
    // submenu opened from the menu, including dynamically-added items.
    //
    // Centralized here because WPF won't accept setting Resources via a
    // Style.Setter (Resources is a CLR property, not a DependencyProperty),
    // so the App.xaml ContextMenu style alone can't theme its children.
    internal static class MenuTheming
    {
        public static void ApplyDarkTheme(ContextMenu menu)
        {
            var app = Application.Current;
            if (app == null) return;

            if (app.TryFindResource("DarkContextMenuStyle") is Style menuStyle)
                menu.Style = menuStyle;
            if (app.TryFindResource("DarkMenuItemStyle") is Style itemStyle)
                menu.Resources[typeof(MenuItem)] = itemStyle;
            if (app.TryFindResource("DarkSeparatorStyle") is Style sepStyle)
                menu.Resources[typeof(Separator)] = sepStyle;
        }
    }
}
