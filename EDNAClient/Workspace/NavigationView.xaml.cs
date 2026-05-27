using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EDNAClient.Helpers;
using UserControl   = System.Windows.Controls.UserControl;
using MenuItem      = System.Windows.Controls.MenuItem;
using TreeViewItem  = System.Windows.Controls.TreeViewItem;
using Separator     = System.Windows.Controls.Separator;
using ContextMenu   = System.Windows.Controls.ContextMenu;

namespace EDNAClient.Workspace
{
    public partial class NavigationView : UserControl
    {
        public NavigationView(NavigationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is NavNode node)
                node.OnSelected?.Invoke();
        }

        private void OnLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // If the clicked node is already selected, SelectedItemChanged won't fire.
            // Re-invoke OnSelected here so clicking a selected item reopens the document.
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && dep is not TreeViewItem)
                dep = VisualTreeHelper.GetParent(dep);

            if (dep is TreeViewItem item2 && item2.IsSelected && item2.DataContext is NavNode node)
                node.OnSelected?.Invoke();
        }

        private void OnRightClick(object sender, MouseButtonEventArgs e)
        {
            // Walk up the visual tree to find the TreeViewItem that was clicked
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && dep is not TreeViewItem)
                dep = VisualTreeHelper.GetParent(dep);

            if (dep is not TreeViewItem item || item.DataContext is not NavNode node)
                return;
            if (node.ContextItems == null || node.ContextItems.Count == 0)
                return;

            e.Handled = true;
            item.IsSelected = true;

            var menu = new ContextMenu();
            MenuTheming.ApplyDarkTheme(menu);

            foreach (var mi in node.ContextItems)
            {
                if (mi.IsSeparator) { menu.Items.Add(new Separator()); continue; }
                menu.Items.Add(BuildMenuItem(mi));
            }
            menu.IsOpen = true;
        }

        private static MenuItem BuildMenuItem(NavMenuItem mi)
        {
            var entry = new MenuItem { Header = mi.Header };

            if (mi.SubItems != null && mi.SubItems.Count > 0)
            {
                foreach (var child in mi.SubItems)
                {
                    if (child.IsSeparator) { entry.Items.Add(new Separator()); continue; }
                    entry.Items.Add(BuildMenuItem(child));
                }
            }
            else
            {
                var action = mi.Execute;
                entry.Click += (_, _) => action();
            }

            return entry;
        }
    }
}
