using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace FontForge.AboutMessage
{
    public partial class AboutWindow2 : Window
    {
        public AboutWindow2()
        {
            InitializeComponent();

            Height += 20;
            Width += 20;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(new AboutWindow3());
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(new AboutWindow1());
        }

        private void NavigateTo(Window next)
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseIn
                }
            };

            fadeOut.Completed += (_, __) =>
            {
                next.Owner = Owner;
                next.Opacity = 0;
                next.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                Hide();

                next.ShowDialog();

                Close();
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}