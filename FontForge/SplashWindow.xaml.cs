using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace FontForge
{
    public partial class SplashWindow : Window
    {
        private const double ProgressBarMaxWidth = 270;

        public SplashWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PlayIntroAnimation();

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
                "Применение светлой и тёмной палитры",
                89,
                1400);

            await SetLoadingStep(
                "Финальная сборка рабочего пространства...",
                "Открываем главное окно программы",
                100,
                1300);

            await Task.Delay(500);

            PlayOutroAnimation();
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
            RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleX);
            RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleY);
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
                var main = new MainWindow();
                main.Show();

                Close();
            };

            RootCard.BeginAnimation(OpacityProperty, fade);
        }
    }
}