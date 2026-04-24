using System;
using System.Windows;

namespace FontForge
{
    public partial class App : Application
    {
        // true = тёмная, false = светлая
        public static bool IsDarkTheme { get; private set; } = false;

        public static event Action? ThemeChanged;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // применяем тему при запуске
            ApplyCurrentTheme();
        }

        /// <summary>
        /// Установить тему: true = тёмная, false = светлая
        /// </summary>
        public static void SetTheme(bool dark)
        {
            IsDarkTheme = dark;
            ApplyCurrentTheme();
        }

        public static void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            ApplyCurrentTheme();
        }

        private static void ApplyCurrentTheme()
        {
            if (Current == null) return;

            if (IsDarkTheme)
            {
                Current.Resources["BackgroundBrush"] = Current.Resources["DarkBackgroundBrush"];
                Current.Resources["TextBrush"] = Current.Resources["DarkTextBrush"];
                Current.Resources["SecondaryTextBrush"] = Current.Resources["DarkSecondaryTextBrush"];
                Current.Resources["ButtonBackgroundBrush"] = Current.Resources["DarkButtonBackgroundBrush"];
                Current.Resources["ButtonTextBrush"] = Current.Resources["DarkButtonTextBrush"];
            }
            else
            {
                Current.Resources["BackgroundBrush"] = Current.Resources["LightBackgroundBrush"];
                Current.Resources["TextBrush"] = Current.Resources["LightTextBrush"];
                Current.Resources["SecondaryTextBrush"] = Current.Resources["LightSecondaryTextBrush"];
                Current.Resources["ButtonBackgroundBrush"] = Current.Resources["LightButtonBackgroundBrush"];
                Current.Resources["ButtonTextBrush"] = Current.Resources["LightButtonTextBrush"];
            }

            ThemeChanged?.Invoke();
        }
    }
}
