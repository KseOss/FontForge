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
            ApplyCurrentTheme();
        }

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
            if (Current == null)
                return;

            if (IsDarkTheme)
            {
                ApplyBrushSet("Dark");
            }
            else
            {
                ApplyBrushSet("Light");
            }

            ThemeChanged?.Invoke();
        }

        private static void ApplyBrushSet(string prefix)
        {
            SetBrush("BackgroundBrush", $"{prefix}BackgroundBrush");
            SetBrush("SurfaceBrush", $"{prefix}SurfaceBrush");
            SetBrush("SurfaceAltBrush", $"{prefix}SurfaceAltBrush");
            SetBrush("CardBrush", $"{prefix}CardBrush");
            SetBrush("InputBackgroundBrush", $"{prefix}InputBackgroundBrush");
            SetBrush("PreviewBackgroundBrush", $"{prefix}PreviewBackgroundBrush");

            SetBrush("TextBrush", $"{prefix}TextBrush");
            SetBrush("SecondaryTextBrush", $"{prefix}SecondaryTextBrush");
            SetBrush("PreviewTextBrush", $"{prefix}PreviewTextBrush");

            SetBrush("BorderBrush", $"{prefix}BorderBrush");
            SetBrush("MutedBorderBrush", $"{prefix}MutedBorderBrush");

            SetBrush("ButtonBackgroundBrush", $"{prefix}ButtonBackgroundBrush");
            SetBrush("ButtonHoverBrush", $"{prefix}ButtonHoverBrush");
            SetBrush("ButtonTextBrush", $"{prefix}ButtonTextBrush");

            SetBrush("SecondaryButtonBackgroundBrush", $"{prefix}SecondaryButtonBackgroundBrush");
            SetBrush("SecondaryButtonTextBrush", $"{prefix}SecondaryButtonTextBrush");

            SetBrush("DangerBrush", $"{prefix}DangerBrush");
            SetBrush("DangerHoverBrush", $"{prefix}DangerHoverBrush");
            SetBrush("DangerTextBrush", $"{prefix}DangerTextBrush");

            SetBrush("AccentBrush", $"{prefix}AccentBrush");
        }

        private static void SetBrush(string targetKey, string sourceKey)
        {
            if (Current.Resources.Contains(sourceKey))
                Current.Resources[targetKey] = Current.Resources[sourceKey];
        }
    }
}