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

            ThemeToggleButton.IsChecked = !App.IsDarkTheme;

            App.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            ThemeToggleButton.IsChecked = !App.IsDarkTheme;
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isThemeAnimating)
                return;

            _isThemeAnimating = true;
            ThemeToggleButton.IsEnabled = false;

            bool wantLightTheme = ThemeToggleButton.IsChecked == true;
            bool wantDarkTheme = !wantLightTheme;

            var fadeToBlack = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseIn
                }
            };

            fadeToBlack.Completed += (_, __) =>
            {
                App.SetTheme(wantDarkTheme);

                var fadeBack = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase
                    {
                        EasingMode = EasingMode.EaseOut
                    }
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
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseIn
                }
            };

            fadeOut.Completed += (_, __) =>
            {
                var fontsWindow = new FontsWindow
                {
                    Owner = null,
                    Opacity = 0,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                fontsWindow.Closed += (_, __) =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();

                    BeginAnimation(
                        OpacityProperty,
                        new DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = TimeSpan.FromMilliseconds(220),
                            EasingFunction = new QuadraticEase
                            {
                                EasingMode = EasingMode.EaseOut
                            }
                        });
                };

                Hide();

                fontsWindow.Show();

                fontsWindow.BeginAnimation(
                    OpacityProperty,
                    new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(220),
                        EasingFunction = new QuadraticEase
                        {
                            EasingMode = EasingMode.EaseOut
                        }
                    });
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var about = new AboutMessage.AboutWindow1
            {
                Owner = this,
                Opacity = 0,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            about.ShowDialog();

            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        protected override void OnClosed(EventArgs e)
        {
            App.ThemeChanged -= OnThemeChanged;
            base.OnClosed(e);
        }
    }
}