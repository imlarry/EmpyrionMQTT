using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color               = System.Windows.Media.Color;
using TextBox             = System.Windows.Controls.TextBox;
using Button              = System.Windows.Controls.Button;
using Orientation         = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace EDNAClient.Skills.Scripting.ScriptEditor
{
    internal class InputDialog : Window
    {
        private readonly TextBox _input = new TextBox();
        public string Result => _input.Text;

        public InputDialog(string title, string prompt, string initial = "")
        {
            Title                 = title;
            Width                 = 360;
            SizeToContent         = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode            = ResizeMode.NoResize;
            Background            = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));

            var stack = new StackPanel { Margin = new Thickness(12) };

            stack.Children.Add(new TextBlock
            {
                Text       = prompt,
                Foreground = Brushes.LightGray,
                Margin     = new Thickness(0, 0, 0, 6),
            });

            _input.Text             = initial;
            _input.Background       = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            _input.Foreground       = Brushes.LightGray;
            _input.BorderBrush      = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            _input.CaretBrush       = Brushes.White;
            _input.Padding          = new Thickness(4, 2, 4, 2);
            stack.Children.Add(_input);

            var buttons = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin              = new Thickness(0, 10, 0, 0),
            };

            var ok = new Button
            {
                Content   = "OK",
                Width     = 70,
                IsDefault = true,
                Margin    = new Thickness(0, 0, 6, 0),
            };
            var cancel = new Button { Content = "Cancel", Width = 70, IsCancel = true };
            ok.Click += (_, _) => DialogResult = true;

            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            stack.Children.Add(buttons);

            Content = stack;
            Loaded += (_, _) => { _input.SelectAll(); _input.Focus(); };
        }
    }
}
