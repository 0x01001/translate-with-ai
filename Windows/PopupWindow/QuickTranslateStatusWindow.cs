using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ReWrite
{
    internal sealed class QuickTranslateStatusWindow : Window
    {
        private readonly TextBlock _messageText;
        private readonly Border _dot;

        public QuickTranslateStatusWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Focusable = false;

            var root = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 10, 14, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Background = new LinearGradientBrush(
                    Color.FromArgb(245, 17, 21, 26),
                    Color.FromArgb(245, 10, 12, 16),
                    90),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 22,
                    ShadowDepth = 8,
                    Opacity = 0.35,
                    Color = Colors.Black
                }
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            _dot = new Border
            {
                Width = 9,
                Height = 9,
                CornerRadius = new CornerRadius(99),
                Background = new SolidColorBrush(Color.FromRgb(168, 85, 247)),
                Margin = new Thickness(0, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            _messageText = new TextBlock
            {
                Text = "Translating...",
                Foreground = new SolidColorBrush(Color.FromRgb(245, 243, 255)),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                MaxWidth = 460
            };

            row.Children.Add(_dot);
            row.Children.Add(_messageText);
            root.Child = row;
            Content = root;
        }

        public void SetMessage(string message, bool isError = false)
        {
            _messageText.Text = string.IsNullOrWhiteSpace(message) ? "Translating..." : message;
            _messageText.Foreground = new SolidColorBrush(isError ? Color.FromRgb(254, 202, 202) : Color.FromRgb(245, 243, 255));
            _dot.Background = new SolidColorBrush(isError ? Color.FromRgb(248, 113, 113) : Color.FromRgb(168, 85, 247));
        }

        public void ShowNearCursor(string message, bool isError = false)
        {
            SetMessage(message, isError);

            NativeMethods.GetCursorPos(out NativeMethods.POINT cursorPos);
            Left = cursorPos.X + 14;
            Top = cursorPos.Y + 14;

            Show();
            ClampToScreen();
        }

        public async void ShowErrorThenHide(string message)
        {
            ShowNearCursor(message, true);
            await Task.Delay(2600);
            Hide();
        }

        private void ClampToScreen()
        {
            double width = ActualWidth > 0 ? ActualWidth : Width;
            double height = ActualHeight > 0 ? ActualHeight : Height;
            if (double.IsNaN(width) || width <= 0) width = 260;
            if (double.IsNaN(height) || height <= 0) height = 48;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            if (Left + width > screenWidth - 10) Left = screenWidth - width - 10;
            if (Top + height > screenHeight - 10) Top = screenHeight - height - 10;
            if (Left < 10) Left = 10;
            if (Top < 10) Top = 10;
        }
    }
}
