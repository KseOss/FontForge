using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace FontForge
{
    public partial class MainWindow : Window
    {
        private bool _isThemeAnimating = false;

        public MainWindow()
        {
            InitializeComponent();

            Height += 20;
            Width += 20;

            // IsChecked == true => светлая (🔆)
            // IsChecked == false => тёмная (🌙)
            ThemeToggleButton.IsChecked = !App.IsDarkTheme;

            App.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            ThemeToggleButton.IsChecked = !App.IsDarkTheme;
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isThemeAnimating) return;

            _isThemeAnimating = true;
            ThemeToggleButton.IsEnabled = false;

            bool wantLightTheme = ThemeToggleButton.IsChecked == true;
            bool wantDarkTheme = !wantLightTheme;

            var fadeToBlack = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            fadeToBlack.Completed += (_, __) =>
            {
                App.SetTheme(wantDarkTheme);

                var fadeBack = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                fadeBack.Completed += (___, ____) =>
                {
                    ThemeToggleButton.IsEnabled = true;
                    _isThemeAnimating = false;
                };

                FadeOverlay.BeginAnimation(OpacityProperty, fadeBack);
            };

            FadeOverlay.BeginAnimation(OpacityProperty, fadeToBlack);
        }

        private void CreateFontButton_Click(object sender, RoutedEventArgs e)
        {
            // плавно "уходим" с главного
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
            fadeOut.Completed += (_, __) =>
            {
                var wnd = new FontsWindow
                {
                    Owner = this,
                    Opacity = 0
                };

                // когда окно шрифтов закроется — вернём главное
                wnd.Closed += (_, __) =>
                {
                    this.Show();
                    this.Opacity = 0;
                    this.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
                };

                wnd.Show();
                this.Hide(); // "перекинули" пользователя
                wnd.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }


        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            // Открываем окно "О программе" (страница 1)
            var about = new AboutMessage.AboutWindow1
            {
                Owner = this,
                Opacity = 0
            };

            // Плавное появление
            about.Loaded += (_, __) =>
            {
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                about.BeginAnimation(OpacityProperty, fadeIn);
            };

            // Если хочешь "модально" — ShowDialog().
            // Тогда пользователь не сможет нажимать главное окно, пока открыто "О программе".
            about.ShowDialog();
        }

        protected override void OnClosed(EventArgs e)
        {
            App.ThemeChanged -= OnThemeChanged;
            base.OnClosed(e);
        }
    }
}
