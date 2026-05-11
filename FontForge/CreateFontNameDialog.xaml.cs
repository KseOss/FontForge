using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace FontForge
{
    public partial class CreateFontNameDialog : Window
    {
        public string? FontName { get; private set; }

        private bool _isThemeAnimating;

        public CreateFontNameDialog()
        {
            InitializeComponent();

            // IsChecked = true => светлая (🔆), false => тёмная (🌙)


            Loaded += (_, __) => NameBox.Focus();
        }


        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isThemeAnimating) return;

            _isThemeAnimating = true;

            // пользователь переключил тумблер:

            // затемнение
            var fadeTo = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            fadeTo.Completed += (_, __) =>
            {
                // применяем тему когда “закрыто”

                // возвращаем
                var fadeBack = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(180),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                fadeBack.Completed += (___, ____) =>
                {
                    _isThemeAnimating = false;
                };

                FadeOverlay.BeginAnimation(OpacityProperty, fadeBack);
            };

            FadeOverlay.BeginAnimation(OpacityProperty, fadeTo);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            FontName = (NameBox.Text ?? "").Trim();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
