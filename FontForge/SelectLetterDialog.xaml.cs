using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FontForge
{
    public partial class SelectLetterDialog : Window
    {
        private readonly HashSet<string> _used;
        private Button? _selectedButton;

        public string? SelectedChar { get; private set; }

        // Цвета для выделения
        private static readonly Brush SelectedBackground = new SolidColorBrush(Color.FromRgb(0x23, 0xC5, 0x5E)); // зелёный
        private static readonly Brush SelectedBorder = new SolidColorBrush(Color.FromRgb(0x12, 0x9A, 0x47));     // темнее
        private static readonly Brush DefaultBackground = (Brush)new BrushConverter().ConvertFromString("#0A000000");
        private static readonly Brush DefaultBorder = (Brush)new BrushConverter().ConvertFromString("#12000000");

        public SelectLetterDialog(HashSet<string> usedChars)
        {
            InitializeComponent();
            _used = usedChars;

            BuildLetters();
        }

        private void BuildLetters()
        {
            var letters = new List<string>();

            letters.AddRange("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ".Select(c => c.ToString()));
            letters.AddRange("абвгдеёжзийклмнопрстуфхцчшщъыьэюя".Select(c => c.ToString()));
            letters.AddRange("0123456789".Select(c => c.ToString()));

            LettersGrid.Children.Clear();

            foreach (var ch in letters)
            {
                var btn = new Button
                {
                    Content = ch,
                    Tag = ch,
                    Style = (Style)FindResource("Tile"),
                    IsEnabled = !_used.Contains(ch)
                };

                btn.Click += Letter_Click;
                LettersGrid.Children.Add(btn);
            }
        }

        private void Letter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (!btn.IsEnabled) return;

            // Снять выделение с прошлой кнопки
            if (_selectedButton != null)
            {
                _selectedButton.Background = DefaultBackground;
                _selectedButton.BorderBrush = DefaultBorder;
                _selectedButton.BorderThickness = new Thickness(1);
            }

            // Выделить новую
            _selectedButton = btn;
            SelectedChar = btn.Tag?.ToString();

            btn.Background = SelectedBackground;
            btn.BorderBrush = SelectedBorder;
            btn.BorderThickness = new Thickness(2);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectedChar))
            {
                MessageBox.Show("Выберите символ.");
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
