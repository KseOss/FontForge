using System.Windows;
using System.Windows.Input;

namespace FontForge
{
    public partial class CreateFontNameDialog : Window
    {
        private readonly string _currentName;
        private readonly bool _isRenameMode;

        public string? FontName { get; private set; }

        public CreateFontNameDialog()
            : this("", false)
        {
        }

        public CreateFontNameDialog(string currentName, bool isRenameMode)
        {
            InitializeComponent();

            _currentName = currentName ?? "";
            _isRenameMode = isRenameMode;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isRenameMode)
            {
                Title = "Переименовать шрифт";

                HeaderText.Text = "Переименовать шрифт";
                DescriptionText.Text = "Измените текущее название шрифта или исправьте нужную букву.";
                HintText.Text = "Текущее название уже введено. Можно исправить только нужную часть текста.";
                OkButton.Content = "Сохранить";

                NameBox.Text = _currentName;
                NameBox.Focus();

                // Ставим курсор в конец, чтобы пользователь мог удобно исправить букву.
                NameBox.CaretIndex = NameBox.Text.Length;
            }
            else
            {
                Title = "Новый шрифт";

                HeaderText.Text = "Создать новый шрифт";
                DescriptionText.Text = "Введите название, которое будет отображаться в списке ваших шрифтов.";
                HintText.Text = "Например: Мой почерк, Pisun, Шрифт Ксении";
                OkButton.Content = "Создать";

                NameBox.Text = "";
                NameBox.Focus();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryConfirm();
                e.Handled = true;
            }

            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                e.Handled = true;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            TryConfirm();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void TryConfirm()
        {
            FontName = (NameBox.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(FontName))
            {
                AppDialog.Info(this, "Введите название шрифта.", "Название шрифта");

                NameBox.Focus();
                return;
            }

            DialogResult = true;
        }
    }
}