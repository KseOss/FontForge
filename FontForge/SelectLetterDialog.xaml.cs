using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace FontForge
{
    public partial class SelectLetterDialog : Window
    {
        private readonly HashSet<string> _used;
        private Button? _selectedButton;

        public string? SelectedChar { get; private set; }

        public SelectLetterDialog(HashSet<string> usedChars)
        {
            InitializeComponent();

            Height += 20;
            Width += 20;

            _used = usedChars ?? new HashSet<string>();

            BuildLetters();
        }

        private void BuildLetters()
        {
            LettersHost.Children.Clear();

            AddGroup(
                "Русские буквы",
                BuildCharsFromString("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя"));

            AddGroup(
                "Английские буквы",
                BuildCharsFromString("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"));

            AddGroup(
                "Цифры",
                BuildCharsFromString("0123456789"));

            AddGroup(
                "Специальные символы",
                BuildSpecialSymbols());
        }

        private static List<string> BuildCharsFromString(string text)
        {
            return text
                .Select(c => c.ToString())
                .ToList();
        }

        private static List<string> BuildSpecialSymbols()
        {
            return new List<string>
            {
                "!",
                "\"",
                "№",
                ";",
                "%",
                ":",
                "?",
                "*",
                "(",
                ")",
                "~",
                "`",
                "@",
                "#",
                "$",
                "^",
                "&",
                "|",
                "\\",
                "/",
                "<",
                ">",
                ".",
                "+"
            };
        }

        private void AddGroup(string title, List<string> symbols)
        {
            symbols = symbols
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();

            if (symbols.Count == 0)
                return;

            var titleText = new TextBlock
            {
                Text = title,
                Style = (Style)FindResource("GroupTitleText")
            };

            LettersHost.Children.Add(titleText);

            var grid = new UniformGrid
            {
                Columns = 8,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 6)
            };

            foreach (string ch in symbols)
            {
                grid.Children.Add(CreateSymbolButton(ch));
            }

            LettersHost.Children.Add(grid);
        }

        private Button CreateSymbolButton(string ch)
        {
            bool isUsed = _used.Contains(ch);

            var letterText = new TextBlock
            {
                Text = ch,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            if (isUsed)
            {
                letterText.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
                letterText.Opacity = 0.85;
            }
            else
            {
                letterText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
                letterText.Opacity = 1;
            }

            var button = new Button
            {
                Content = letterText,
                Tag = ch,
                Style = (Style)FindResource("LetterTileButton"),
                Opacity = isUsed ? 0.55 : 1,
                Cursor = isUsed
                    ? System.Windows.Input.Cursors.Arrow
                    : System.Windows.Input.Cursors.Hand,
                ToolTip = isUsed
                    ? $"Символ «{ch}» уже добавлен"
                    : $"Выбрать символ «{ch}»"
            };

            if (isUsed)
            {
                button.SetResourceReference(Button.BackgroundProperty, "InputBackgroundBrush");
                button.SetResourceReference(Button.BorderBrushProperty, "MutedBorderBrush");
                button.IsEnabled = false;
            }
            else
            {
                button.SetResourceReference(Button.BackgroundProperty, "SurfaceAltBrush");
                button.SetResourceReference(Button.BorderBrushProperty, "MutedBorderBrush");
                button.Click += Letter_Click;
            }

            return button;
        }

        private void Letter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            string? ch = button.Tag?.ToString();

            if (string.IsNullOrEmpty(ch))
                return;

            if (_used.Contains(ch))
                return;

            if (_selectedButton != null)
                ResetButtonVisual(_selectedButton);

            _selectedButton = button;
            SelectedChar = ch;

            ApplySelectedVisual(button);
        }

        private void ApplySelectedVisual(Button button)
        {
            button.Opacity = 1;
            button.SetResourceReference(Button.BackgroundProperty, "ButtonBackgroundBrush");
            button.SetResourceReference(Button.ForegroundProperty, "ButtonTextBrush");
            button.SetResourceReference(Button.BorderBrushProperty, "ButtonHoverBrush");
            button.BorderThickness = new Thickness(2);

            if (button.Content is TextBlock text)
            {
                text.SetResourceReference(TextBlock.ForegroundProperty, "ButtonTextBrush");
                text.Opacity = 1;
            }
        }

        private void ResetButtonVisual(Button button)
        {
            button.Opacity = 1;
            button.SetResourceReference(Button.BackgroundProperty, "SurfaceAltBrush");
            button.SetResourceReference(Button.ForegroundProperty, "TextBrush");
            button.SetResourceReference(Button.BorderBrushProperty, "MutedBorderBrush");
            button.BorderThickness = new Thickness(1);

            if (button.Content is TextBlock text)
            {
                text.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
                text.Opacity = 1;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedChar))
            {
                AppDialog.Info(
                    this,
                    "Выберите символ.",
                    "Выбор символа");

                return;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}