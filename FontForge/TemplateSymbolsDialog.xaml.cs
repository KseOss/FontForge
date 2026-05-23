using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace FontForge
{
    public partial class TemplateSymbolsDialog : Window
    {
        private readonly List<string> _orderedSymbols;
        private readonly List<string> _initiallySelected;
        private readonly bool _selectAllByDefault;

        private readonly Dictionary<string, CheckBox> _boxes = new();

        public List<string> SelectedSymbols { get; private set; } = new();

        public TemplateSymbolsDialog(List<string> symbols)
            : this(symbols, null, true)
        {
        }

        public TemplateSymbolsDialog(
            List<string> symbols,
            IEnumerable<string>? initiallySelected,
            bool selectAllByDefault)
        {
            InitializeComponent();

            _orderedSymbols = symbols
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            _initiallySelected = initiallySelected?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList() ?? new List<string>();

            _selectAllByDefault = selectAllByDefault;

            BuildSymbols();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyInitialSelection();
            UpdateCount();
        }

        private void BuildSymbols()
        {
            SymbolsHost.Children.Clear();
            _boxes.Clear();

            AddGroup("Русские буквы", BuildChars("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя"));
            AddGroup("Английские буквы", BuildChars("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"));
            AddGroup("Цифры", BuildChars("0123456789"));
            AddGroup("Специальные символы", BuildSpecialSymbols());
        }

        private void ApplyInitialSelection()
        {
            if (_initiallySelected.Count > 0)
            {
                var set = new HashSet<string>(_initiallySelected);

                foreach (var pair in _boxes)
                {
                    pair.Value.IsChecked = set.Contains(pair.Key);
                }

                return;
            }

            if (_selectAllByDefault)
            {
                SetAll(true);
            }
            else
            {
                SetAll(false);
            }
        }

        private static List<string> BuildChars(string text)
        {
            return text.Select(c => c.ToString()).ToList();
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
                .Where(x => _orderedSymbols.Contains(x))
                .ToList();

            if (symbols.Count == 0)
                return;

            var titleBlock = new TextBlock
            {
                Text = title,
                Style = (Style)FindResource("GroupTitleText")
            };

            SymbolsHost.Children.Add(titleBlock);

            var grid = new UniformGrid
            {
                Columns = 9,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            foreach (string symbol in symbols)
            {
                var box = new CheckBox
                {
                    Content = symbol,
                    Tag = symbol,
                    Style = (Style)FindResource("SymbolCheckBox")
                };

                box.Checked += SymbolCheck_Changed;
                box.Unchecked += SymbolCheck_Changed;

                _boxes[symbol] = box;
                grid.Children.Add(box);
            }

            SymbolsHost.Children.Add(grid);
        }

        private void SymbolCheck_Changed(object sender, RoutedEventArgs e)
        {
            UpdateCount();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            SetAll(true);
            UpdateCount();
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            SetAll(false);
            UpdateCount();
        }

        private void OnlyRussian_Click(object sender, RoutedEventArgs e)
        {
            SetOnly(BuildChars("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя"));
        }

        private void OnlyEnglish_Click(object sender, RoutedEventArgs e)
        {
            SetOnly(BuildChars("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"));
        }

        private void OnlyDigits_Click(object sender, RoutedEventArgs e)
        {
            SetOnly(BuildChars("0123456789"));
        }

        private void OnlySpecial_Click(object sender, RoutedEventArgs e)
        {
            SetOnly(BuildSpecialSymbols());
        }

        private void SetOnly(List<string> symbols)
        {
            var set = new HashSet<string>(symbols);

            foreach (var pair in _boxes)
            {
                pair.Value.IsChecked = set.Contains(pair.Key);
            }

            UpdateCount();
        }

        private void SetAll(bool value)
        {
            foreach (CheckBox box in _boxes.Values)
            {
                box.IsChecked = value;
            }
        }

        private void UpdateCount()
        {
            int count = _boxes.Values.Count(x => x.IsChecked == true);
            CountText.Text = $"Выбрано символов: {count}";
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            SelectedSymbols = _orderedSymbols
                .Where(symbol => _boxes.TryGetValue(symbol, out CheckBox? box) && box.IsChecked == true)
                .ToList();

            if (SelectedSymbols.Count == 0)
            {
                AppDialog.Info(this, "Выберите хотя бы один символ.", "PDF-шаблон");
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