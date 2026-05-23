using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FontForge
{
    public enum AppDialogKind
    {
        Info,
        Question,
        Warning,
        Error,
        Success
    }

    public partial class AppDialogWindow : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private readonly MessageBoxButton _buttons;
        private readonly AppDialogKind _kind;

        public AppDialogWindow(
            string title,
            string message,
            MessageBoxButton buttons,
            AppDialogKind kind)
        {
            InitializeComponent();

            _buttons = buttons;
            _kind = kind;

            Title = title;
            TitleText.Text = title;
            MessageText.Text = message;

            ConfigureIcon();
            ConfigureButtons();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (OkButton.Visibility == Visibility.Visible)
                OkButton.Focus();
            else if (NoButton.Visibility == Visibility.Visible)
                NoButton.Focus();
            else if (CancelButton.Visibility == Visibility.Visible)
                CancelButton.Focus();
        }

        private void ConfigureIcon()
        {
            string icon = "i";
            string color = "#2F2F2F";

            switch (_kind)
            {
                case AppDialogKind.Info:
                    icon = "i";
                    color = "#4F7CCF";
                    break;

                case AppDialogKind.Question:
                    icon = "?";
                    color = "#7A5FCB";
                    break;

                case AppDialogKind.Warning:
                    icon = "!";
                    color = "#C7772E";
                    break;

                case AppDialogKind.Error:
                    icon = "×";
                    color = "#C65B7C";
                    break;

                case AppDialogKind.Success:
                    icon = "✓";
                    color = "#3F8F57";
                    break;
            }

            IconText.Text = icon;
            IconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));

            IconBox.Background = BuildSoftBrush(color, 0.14);
            IconBox.BorderBrush = BuildSoftBrush(color, 0.32);
        }

        private static SolidColorBrush BuildSoftBrush(string hex, double opacity)
        {
            Color color = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(Color.FromArgb(
                (byte)(255 * opacity),
                color.R,
                color.G,
                color.B));
        }

        private void ConfigureButtons()
        {
            CancelButton.Visibility = Visibility.Collapsed;
            NoButton.Visibility = Visibility.Collapsed;
            OkButton.Visibility = Visibility.Collapsed;

            switch (_buttons)
            {
                case MessageBoxButton.OK:
                    OkButton.Content = "ОК";
                    OkButton.Visibility = Visibility.Visible;
                    break;

                case MessageBoxButton.OKCancel:
                    CancelButton.Content = "Отмена";
                    CancelButton.Visibility = Visibility.Visible;

                    OkButton.Content = "ОК";
                    OkButton.Visibility = Visibility.Visible;
                    break;

                case MessageBoxButton.YesNo:
                    NoButton.Content = "Нет";
                    NoButton.Visibility = Visibility.Visible;

                    OkButton.Content = "Да";
                    OkButton.Visibility = Visibility.Visible;
                    break;

                case MessageBoxButton.YesNoCancel:
                    CancelButton.Content = "Отмена";
                    CancelButton.Visibility = Visibility.Visible;

                    NoButton.Content = "Нет";
                    NoButton.Visibility = Visibility.Visible;

                    OkButton.Content = "Да";
                    OkButton.Visibility = Visibility.Visible;
                    break;
            }

            if (_kind == AppDialogKind.Error || _kind == AppDialogKind.Warning)
                OkButton.Style = (Style)FindResource("DialogDangerButton");
            else
                OkButton.Style = (Style)FindResource("DialogPrimaryButton");
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (_buttons == MessageBoxButton.YesNo ||
                _buttons == MessageBoxButton.YesNoCancel)
            {
                Result = MessageBoxResult.Yes;
            }
            else
            {
                Result = MessageBoxResult.OK;
            }

            DialogResult = true;
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            DialogResult = false;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            DialogResult = false;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }

            if (e.Key == Key.Escape)
            {
                if (CancelButton.Visibility == Visibility.Visible)
                    Result = MessageBoxResult.Cancel;
                else if (NoButton.Visibility == Visibility.Visible)
                    Result = MessageBoxResult.No;
                else
                    Result = MessageBoxResult.OK;

                DialogResult = false;
                e.Handled = true;
            }
        }
    }

    public static class AppDialog
    {
        public static MessageBoxResult Show(
            Window? owner,
            string title,
            string message,
            MessageBoxButton buttons = MessageBoxButton.OK,
            AppDialogKind kind = AppDialogKind.Info)
        {
            var dialog = new AppDialogWindow(title, message, buttons, kind);

            if (owner != null)
                dialog.Owner = owner;

            dialog.ShowDialog();

            return dialog.Result;
        }

        public static MessageBoxResult Info(Window? owner, string message, string title = "Сообщение")
        {
            return Show(owner, title, message, MessageBoxButton.OK, AppDialogKind.Info);
        }

        public static MessageBoxResult Success(Window? owner, string message, string title = "Готово")
        {
            return Show(owner, title, message, MessageBoxButton.OK, AppDialogKind.Success);
        }

        public static MessageBoxResult Warning(Window? owner, string message, string title = "Внимание")
        {
            return Show(owner, title, message, MessageBoxButton.OK, AppDialogKind.Warning);
        }

        public static MessageBoxResult Error(Window? owner, string message, string title = "Ошибка")
        {
            return Show(owner, title, message, MessageBoxButton.OK, AppDialogKind.Error);
        }

        public static MessageBoxResult Question(Window? owner, string message, string title = "Подтверждение")
        {
            return Show(owner, title, message, MessageBoxButton.YesNo, AppDialogKind.Question);
        }
    }
}