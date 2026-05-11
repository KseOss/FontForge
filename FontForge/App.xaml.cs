using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace FontForge
{
    public partial class App : Application
    {
        public static bool IsDarkTheme { get; private set; } = false;

        public static event Action? ThemeChanged;

        public static AppThemeSettings CurrentThemeSettings { get; private set; } = new AppThemeSettings();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            CurrentThemeSettings = LoadThemeSettings();
            IsDarkTheme = CurrentThemeSettings.ThemeMode == "Dark";

            ApplyCurrentTheme();

            var splash = new SplashWindow();
            splash.Show();
        }

        public static void SetTheme(bool dark)
        {
            CurrentThemeSettings.ThemeMode = dark ? "Dark" : "Light";
            IsDarkTheme = dark;

            SaveThemeSettings(CurrentThemeSettings);
            ApplyCurrentTheme();
        }

        public static void SetThemeSettings(AppThemeSettings settings, bool save)
        {
            if (settings == null)
                settings = new AppThemeSettings();

            settings.Normalize();

            CurrentThemeSettings = settings;
            IsDarkTheme = settings.ThemeMode == "Dark";

            if (save)
                SaveThemeSettings(settings);

            ApplyCurrentTheme();
        }

        public static void ToggleTheme()
        {
            SetTheme(!IsDarkTheme);
        }

        public static void ApplyCurrentTheme()
        {
            if (Current == null)
                return;

            CurrentThemeSettings.Normalize();

            bool dark = CurrentThemeSettings.ThemeMode == "Dark";
            ThemePalette palette = ThemePalette.FromName(CurrentThemeSettings.AccentName, dark);

            Color baseBackground = dark ? ColorFromHex("#101217") : ColorFromHex("#F7F8FB");
            Color baseSurface = dark ? ColorFromHex("#171A21") : ColorFromHex("#FFFFFF");
            Color baseSurfaceAlt = dark ? ColorFromHex("#20242D") : ColorFromHex("#F1F2F5");
            Color baseCard = dark ? ColorFromHex("#1B1F27") : ColorFromHex("#EEF0F3");
            Color baseInput = dark ? ColorFromHex("#252A34") : ColorFromHex("#E9EAEE");

            Brush backgroundBrush = BuildBackgroundBrush(
                baseBackground,
                palette.Accent,
                CurrentThemeSettings.BackgroundStyle,
                dark);

            Color buttonColor = BuildButtonColor(
                CurrentThemeSettings.ButtonStyle,
                CurrentThemeSettings.AccentName,
                palette.Accent,
                dark);

            Color buttonHover = dark
                ? Lighten(buttonColor, 0.12)
                : Darken(buttonColor, 0.12);

            Color secondaryButton = dark
                ? ColorFromHex("#252A34")
                : ColorFromHex("#E4E7EC");

            Color secondaryButtonHover = dark
                ? ColorFromHex("#303744")
                : ColorFromHex("#D7DBE2");

            SetResource("BackgroundBrush", backgroundBrush);
            SetBrush("SurfaceBrush", baseSurface);
            SetBrush("SurfaceAltBrush", baseSurfaceAlt);
            SetBrush("CardBrush", Mix(baseCard, palette.Accent, CurrentThemeSettings.BackgroundStyle == "Plain" ? 0.00 : 0.04));
            SetBrush("InputBackgroundBrush", baseInput);

            SetBrush("PreviewBackgroundBrush", ColorFromHex("#FFFFFF"));
            SetBrush("PreviewTextBrush", ColorFromHex("#101114"));

            SetBrush("TextBrush", dark ? ColorFromHex("#F4F6FA") : ColorFromHex("#101114"));
            SetBrush("SecondaryTextBrush", dark ? ColorFromHex("#AAB2C0") : ColorFromHex("#667085"));

            SetBrush("BorderBrush", dark ? ColorFromHex("#343B49") : ColorFromHex("#D6DAE2"));
            SetBrush("MutedBorderBrush", dark ? ColorFromHex("#2A303B") : ColorFromHex("#E4E7EC"));

            SetBrush("ButtonBackgroundBrush", buttonColor);
            SetBrush("ButtonHoverBrush", buttonHover);
            SetBrush("ButtonTextBrush", GetReadableTextColor(buttonColor));

            SetBrush("SecondaryButtonBackgroundBrush", secondaryButton);
            SetBrush("SecondaryButtonHoverBrush", secondaryButtonHover);
            SetBrush("SecondaryButtonTextBrush", dark ? ColorFromHex("#F4F6FA") : ColorFromHex("#101114"));

            SetBrush("DangerBrush", buttonColor);
            SetBrush("DangerHoverBrush", buttonHover);
            SetBrush("DangerTextBrush", GetReadableTextColor(buttonColor));

            SetBrush("AccentBrush", palette.Accent);

            ThemeChanged?.Invoke();
        }

        private static Brush BuildBackgroundBrush(Color baseBackground, Color accent, string backgroundStyle, bool dark)
        {
            if (backgroundStyle == "SoftGradient")
            {
                var brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };

                brush.GradientStops.Add(new GradientStop(Mix(baseBackground, accent, dark ? 0.18 : 0.12), 0));
                brush.GradientStops.Add(new GradientStop(baseBackground, 0.55));
                brush.GradientStops.Add(new GradientStop(Mix(baseBackground, accent, dark ? 0.10 : 0.08), 1));

                return brush;
            }

            if (backgroundStyle == "StrongGradient")
            {
                var brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };

                brush.GradientStops.Add(new GradientStop(Mix(baseBackground, accent, dark ? 0.34 : 0.22), 0));
                brush.GradientStops.Add(new GradientStop(Mix(baseBackground, accent, dark ? 0.16 : 0.12), 0.45));
                brush.GradientStops.Add(new GradientStop(baseBackground, 1));

                return brush;
            }

            return new SolidColorBrush(baseBackground);
        }

        private static Color BuildButtonColor(string buttonStyle, string accentName, Color accent, bool dark)
        {
            if (buttonStyle == "Neutral")
                return dark ? ColorFromHex("#E5E7EB") : ColorFromHex("#2F2F2F");

            if (buttonStyle == "Accent")
                return accent;

            if (accentName == "Neutral")
                return dark ? ColorFromHex("#E5E7EB") : ColorFromHex("#2F2F2F");

            return accent;
        }

        private static void SetBrush(string key, Color color)
        {
            SetResource(key, new SolidColorBrush(color));
        }

        private static void SetResource(string key, object value)
        {
            if (Current == null)
                return;

            Current.Resources[key] = value;
        }

        private static Color GetReadableTextColor(Color background)
        {
            double luminance =
                (0.299 * background.R +
                 0.587 * background.G +
                 0.114 * background.B) / 255.0;

            return luminance > 0.58
                ? ColorFromHex("#101114")
                : ColorFromHex("#FFFFFF");
        }

        private static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        private static Color Mix(Color a, Color b, double amount)
        {
            amount = Math.Clamp(amount, 0, 1);

            byte r = (byte)Math.Round(a.R + (b.R - a.R) * amount);
            byte g = (byte)Math.Round(a.G + (b.G - a.G) * amount);
            byte bl = (byte)Math.Round(a.B + (b.B - a.B) * amount);

            return Color.FromRgb(r, g, bl);
        }

        private static Color Lighten(Color color, double amount)
        {
            return Mix(color, Color.FromRgb(255, 255, 255), amount);
        }

        private static Color Darken(Color color, double amount)
        {
            return Mix(color, Color.FromRgb(0, 0, 0), amount);
        }

        private static string GetSettingsPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FontForge");

            Directory.CreateDirectory(dir);

            return Path.Combine(dir, "theme_settings.json");
        }

        private static AppThemeSettings LoadThemeSettings()
        {
            try
            {
                string path = GetSettingsPath();

                if (!File.Exists(path))
                    return new AppThemeSettings();

                string json = File.ReadAllText(path);
                AppThemeSettings? settings = JsonSerializer.Deserialize<AppThemeSettings>(json);

                if (settings == null)
                    return new AppThemeSettings();

                settings.Normalize();
                return settings;
            }
            catch
            {
                return new AppThemeSettings();
            }
        }

        private static void SaveThemeSettings(AppThemeSettings settings)
        {
            try
            {
                string path = GetSettingsPath();
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(path, json);
            }
            catch
            {
                // Если настройки не сохранились, приложение всё равно должно работать.
            }
        }
    }

    public class AppThemeSettings
    {
        public string ThemeMode { get; set; } = "Light";

        public string AccentName { get; set; } = "Neutral";

        public string BackgroundStyle { get; set; } = "Plain";

        public string ButtonStyle { get; set; } = "Auto";

        public AppThemeSettings Clone()
        {
            return new AppThemeSettings
            {
                ThemeMode = ThemeMode,
                AccentName = AccentName,
                BackgroundStyle = BackgroundStyle,
                ButtonStyle = ButtonStyle
            };
        }

        public void Normalize()
        {
            if (ThemeMode != "Light" && ThemeMode != "Dark")
                ThemeMode = "Light";

            if (AccentName != "Neutral" &&
                AccentName != "Blue" &&
                AccentName != "Green" &&
                AccentName != "Purple" &&
                AccentName != "Rose" &&
                AccentName != "Orange")
            {
                AccentName = "Neutral";
            }

            if (BackgroundStyle != "Plain" &&
                BackgroundStyle != "SoftGradient" &&
                BackgroundStyle != "StrongGradient")
            {
                BackgroundStyle = "Plain";
            }

            if (ButtonStyle != "Auto" &&
                ButtonStyle != "Neutral" &&
                ButtonStyle != "Accent")
            {
                ButtonStyle = "Auto";
            }
        }
    }

    public class ThemePalette
    {
        public Color Accent { get; set; }

        public static ThemePalette FromName(string name, bool dark)
        {
            return name switch
            {
                "Blue" => new ThemePalette
                {
                    Accent = dark ? ColorFromHex("#8DB7FF") : ColorFromHex("#4F7CCF")
                },

                "Green" => new ThemePalette
                {
                    Accent = dark ? ColorFromHex("#8DD9A3") : ColorFromHex("#3F8F57")
                },

                "Purple" => new ThemePalette
                {
                    Accent = dark ? ColorFromHex("#C2A7FF") : ColorFromHex("#7A5FCB")
                },

                "Rose" => new ThemePalette
                {
                    Accent = dark ? ColorFromHex("#F2A6BE") : ColorFromHex("#C65B7C")
                },

                "Orange" => new ThemePalette
                {
                    Accent = dark ? ColorFromHex("#F2B36D") : ColorFromHex("#C7772E")
                },

                _ => new ThemePalette
                {
                    Accent = dark ? ColorFromHex("#D1D5DB") : ColorFromHex("#2F2F2F")
                }
            };
        }

        private static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
    }
}