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
            BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)));
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
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
            fadeOut.Completed += (_, __) =>
            {
                next.Owner = this.Owner;
                next.Show();
                this.Close();
            };
            BeginAnimation(OpacityProperty, fadeOut);
        }

        private void FadeClose()
        {
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
            fadeOut.Completed += (_, __) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
