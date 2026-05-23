using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FontForge
{
    public partial class SplashWindow : Window
    {
        private const double ProgressBarMaxWidth = 270;

        private readonly bool _fullLoading;

        public SplashWindow()
            : this(true)
        {
        }

        public SplashWindow(bool fullLoading)
        {
            InitializeComponent();

            _fullLoading = fullLoading;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PrepareGlowColors();
            StartSoftGlowAnimation();
            PlayIntroAnimation();

            if (_fullLoading)
                await RunFullLoadingAsync();
            else
                await RunQuickLoadingAsync();

            App.MarkFirstLaunchCompleted();

            await Task.Delay(300);

            PlayOutroAnimation();
        }

        private async Task RunFullLoadingAsync()
        {
            await SetLoadingStep(
                "Сбор данных приложения...",
                "Проверка структуры проекта и локальных ресурсов",
                6,
                1300);

            await SetLoadingStep(
                "Подготовка интерфейса...",
                "Загрузка окон, стилей и элементов управления",
                15,
                1500);

            await SetLoadingStep(
                "Проверка пользовательских шрифтов...",
                "Поиск созданных гарнитур и сохранённых символов",
                27,
                1600);

            await SetLoadingStep(
                "Чтение вариантов букв...",
                "Подготовка PNG-символов для предпросмотра",
                39,
                1600);

            await SetLoadingStep(
                "Настройка редактора символов...",
                "Подготовка холста, кистей и направляющих",
                52,
                1700);

            await SetLoadingStep(
                "Подготовка текстового редактора...",
                "Загрузка параметров интервалов, отступов и предпросмотра",
                65,
                1600);

            await SetLoadingStep(
                "Настройка PDF-экспорта...",
                "Подготовка страниц, изображений и параметров печати",
                78,
                1600);

            await SetLoadingStep(
                "Синхронизация темы оформления...",
                "Применение выбранной палитры и фона",
                89,
                1400);

            await SetLoadingStep(
                "Финальная сборка рабочего пространства...",
                "Открываем программу",
                100,
                1300);
        }

        private async Task RunQuickLoadingAsync()
        {
            await SetLoadingStep(
                "Быстрый запуск...",
                "Загружаем сохранённые настройки оформления",
                20,
                1000);

            await SetLoadingStep(
                "Проверка шрифтов...",
                "Подготовка списка созданных шрифтов",
                45,
                1200);

            await SetLoadingStep(
                "Подготовка интерфейса...",
                "Открываем рабочее окно",
                70,
                1200);

            await SetLoadingStep(
                "Готово",
                "Переход к вашим шрифтам",
                100,
                1600);
        }

        private async Task SetLoadingStep(string title, string detail, int percent, int delayMilliseconds)
        {
            LoadingText.Text = title;
            LoadingDetailText.Text = detail;
            LoadingPercentText.Text = $"{percent}%";

            AnimateProgress(percent);
            AnimateTextChange();

            await Task.Delay(delayMilliseconds);
        }

        private void PrepareGlowColors()
        {
            bool dark = App.IsDarkTheme;

            Color accent = GetResourceColor(
                "AccentBrush",
                dark ? "#D1D5DB" : "#2F2F2F");

            Color glowOne = Mix(
                accent,
                ColorFromHex("#FFFFFF"),
                dark ? 0.18 : 0.45);

            Color glowTwo = Mix(
                accent,
                ColorFromHex("#FFFFFF"),
                dark ? 0.10 : 0.30);

            GlowOneColor.Color = Color.FromArgb(255, glowOne.R, glowOne.G, glowOne.B);
            GlowTwoColor.Color = Color.FromArgb(255, glowTwo.R, glowTwo.G, glowTwo.B);

            GlowOne.Opacity = dark ? 0.20 : 0.28;
            GlowTwo.Opacity = dark ? 0.18 : 0.24;
        }

        private void StartSoftGlowAnimation()
        {
            AnimateGlow(
                GlowOneTransform,
                fromX: -20,
                toX: 50,
                fromY: -10,
                toY: 35,
                seconds: 5.8);

            AnimateGlow(
                GlowTwoTransform,
                fromX: 30,
                toX: -55,
                fromY: 15,
                toY: -40,
                seconds: 6.6);

            AnimateOpacity(GlowOne, from: GlowOne.Opacity * 0.75, to: GlowOne.Opacity, seconds: 3.6);
            AnimateOpacity(GlowTwo, from: GlowTwo.Opacity * 0.70, to: GlowTwo.Opacity, seconds: 4.2);
        }

        private static void AnimateGlow(
            TranslateTransform transform,
            double fromX,
            double toX,
            double fromY,
            double toY,
            double seconds)
        {
            var animX = new DoubleAnimation
            {
                From = fromX,
                To = toX,
                Duration = TimeSpan.FromSeconds(seconds),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseInOut
                }
            };

            var animY = new DoubleAnimation
            {
                From = fromY,
                To = toY,
                Duration = TimeSpan.FromSeconds(seconds + 0.8),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseInOut
                }
            };

            transform.BeginAnimation(TranslateTransform.XProperty, animX);
            transform.BeginAnimation(TranslateTransform.YProperty, animY);
        }

        private static void AnimateOpacity(UIElement element, double from, double to, double seconds)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromSeconds(seconds),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase
                {
                    EasingMode = EasingMode.EaseInOut
                }
            };

            element.BeginAnimation(OpacityProperty, animation);
        }

        private void AnimateProgress(int percent)
        {
            percent = Math.Clamp(percent, 0, 100);

            double targetWidth = ProgressBarMaxWidth * percent / 100.0;

            var animation = new DoubleAnimation
            {
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(520),
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            ProgressFill.BeginAnimation(WidthProperty, animation);
        }

        private void AnimateTextChange()
        {
            var fade = new DoubleAnimation
            {
                From = 0.35,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(280),
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            LoadingText.BeginAnimation(OpacityProperty, fade);
            LoadingDetailText.BeginAnimation(OpacityProperty, fade);
            LoadingPercentText.BeginAnimation(OpacityProperty, fade);
        }

        private void PlayIntroAnimation()
        {
            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(650),
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            var scaleX = new DoubleAnimation
            {
                From = 0.96,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(650),
                EasingFunction = new BackEase
                {
                    EasingMode = EasingMode.EaseOut,
                    Amplitude = 0.20
                }
            };

            var scaleY = new DoubleAnimation
            {
                From = 0.96,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(650),
                EasingFunction = new BackEase
                {
                    EasingMode = EasingMode.EaseOut,
                    Amplitude = 0.20
                }
            };

            RootCard.BeginAnimation(OpacityProperty, fade);
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        }

        private void PlayOutroAnimation()
        {
            var fade = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(380),
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseIn
                }
            };

            fade.Completed += (_, __) =>
            {
                Window nextWindow;

                if (!_fullLoading)
                {
                    nextWindow = new FontsWindow();
                }
                else if (App.CurrentThemeSettings.StartWindow == "MainWindow")
                {
                    nextWindow = new MainWindow();
                }
                else
                {
                    nextWindow = new FontsWindow();
                }

                nextWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                nextWindow.Show();

                Close();
            };

            RootCard.BeginAnimation(OpacityProperty, fade);
        }

        private static Color GetResourceColor(string key, string fallbackHex)
        {
            try
            {
                if (Application.Current.Resources.Contains(key) &&
                    Application.Current.Resources[key] is SolidColorBrush brush)
                {
                    return brush.Color;
                }
            }
            catch
            {
                // ignore
            }

            return ColorFromHex(fallbackHex);
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
    }
}