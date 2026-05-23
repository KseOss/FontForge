using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using FontForge.Classes;

namespace FontForge
{
    public partial class FontsWindow : Window
    {
        private readonly List<CreatedFont> _allFonts = new List<CreatedFont>();

        public ObservableCollection<CreatedFont> ActiveFonts { get; } = new ObservableCollection<CreatedFont>();
        public ObservableCollection<CreatedFont> TrashFonts { get; } = new ObservableCollection<CreatedFont>();

        public ICommand OpenEditorCommand { get; }

        public FontsWindow()
        {
            InitializeComponent();

            DataContext = this;

            OpenEditorCommand = new RelayCommand<CreatedFont>(OpenEditor);

            LoadAll();
            RefreshViews();
        }

        private void LoadAll()
        {
            _allFonts.Clear();
            _allFonts.AddRange(FontStorage.LoadFonts());
        }

        private void SaveAll()
        {
            FontStorage.SaveFonts(_allFonts);
        }

        private void RefreshViews()
        {
            ActiveFonts.Clear();
            TrashFonts.Clear();

            foreach (var font in _allFonts.Where(x => !x.IsDeleted).OrderByDescending(x => x.CreatedAt))
                ActiveFonts.Add(font);

            foreach (var font in _allFonts.Where(x => x.IsDeleted).OrderByDescending(x => x.DeletedAt ?? DateTime.MinValue))
                TrashFonts.Add(font);

            EmptyActivePanel.Visibility = ActiveFonts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ActiveListPanel.Visibility = ActiveFonts.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            EmptyTrashPanel.Visibility = TrashFonts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TrashListPanel.Visibility = TrashFonts.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)));
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new ThemeSettingsWindow
            {
                Owner = this
            };

            settingsWindow.ShowDialog();
        }

        private void CreateNewFont_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CreateFontNameDialog
            {
                Owner = this
            };

            bool? ok = dialog.ShowDialog();

            if (ok != true)
                return;

            string name = (dialog.FontName ?? "").Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                AppDialog.Info(this, "Введите название шрифта.");
                return;
            }

            if (_allFonts.Any(f => !f.IsDeleted && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Шрифт с таким названием уже существует.");
                return;
            }

            var created = new CreatedFont
            {
                Name = name,
                CreatedAt = DateTime.Now,
                IsDeleted = false,
                DeletedAt = null
            };

            _allFonts.Add(created);

            SaveAll();
            RefreshViews();

            if (App.CurrentThemeSettings.AfterCreateFont == "OpenEditor")
            {
                OpenEditor(created);

                LoadAll();
                RefreshViews();
            }
        }

        private void EditFont_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is CreatedFont font)
                OpenEditor(font);
        }

        private void OpenEditor(CreatedFont? font)
        {
            if (font == null)
                return;

            var found = _allFonts.FirstOrDefault(x => x.Id == font.Id);

            if (found == null)
                return;

            if (found.IsDeleted)
            {
                MessageBox.Show("Этот шрифт находится в корзине. Сначала восстановите его.");
                return;
            }

            var editor = new FontEditorWindow(found.Id)
            {
                Owner = this
            };

            editor.ShowDialog();

            LoadAll();
            RefreshViews();
        }

        private void RenameFont_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not CreatedFont font)
                return;

            var found = _allFonts.FirstOrDefault(x => x.Id == font.Id);

            if (found == null)
                return;

            if (found.IsDeleted)
            {
                AppDialog.Warning(this, "Этот шрифт находится в корзине. Сначала восстановите его.");
                return;
            }

            var dialog = new CreateFontNameDialog(found.Name, true)
            {
                Owner = this
            };

            bool? ok = dialog.ShowDialog();

            if (ok != true)
                return;

            string newName = (dialog.FontName ?? "").Trim();

            if (string.IsNullOrWhiteSpace(newName))
            {
                AppDialog.Info(this, "Введите название шрифта.", "Переименование");
                return;
            }

            if (string.Equals(found.Name, newName, StringComparison.Ordinal))
                return;

            if (_allFonts.Any(f =>
                    !f.IsDeleted &&
                    f.Id != found.Id &&
                    string.Equals(f.Name, newName, StringComparison.OrdinalIgnoreCase)))
            {
                AppDialog.Warning(this, "Шрифт с таким названием уже существует.", "Переименование");
                return;
            }

            found.Name = newName;

            SaveAll();
            RefreshViews();
        }

        private void MoveToTrash_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not CreatedFont font)
                return;

            var found = _allFonts.FirstOrDefault(x => x.Id == font.Id);

            if (found == null)
                return;

            if (App.CurrentThemeSettings.ConfirmMoveToTrash)
            {
                MessageBoxResult result = AppDialog.Question(
                    this,
                    $"Переместить шрифт «{found.Name}» в корзину?",
                    "Подтверждение");

                if (result != MessageBoxResult.Yes)
                    return;
            }

            found.IsDeleted = true;
            found.DeletedAt = DateTime.Now;

            SaveAll();
            RefreshViews();
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not CreatedFont font)
                return;

            var found = _allFonts.FirstOrDefault(x => x.Id == font.Id);

            if (found == null)
                return;

            found.IsDeleted = false;
            found.DeletedAt = null;

            SaveAll();
            RefreshViews();
        }

        private void DeleteForever_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not CreatedFont font)
                return;

            MessageBoxResult result = AppDialog.Show(
                this,
                "Удаление",
                "Удалить шрифт навсегда? Это действие нельзя отменить.",
                MessageBoxButton.YesNo,
                AppDialogKind.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            _allFonts.RemoveAll(x => x.Id == font.Id);

            SaveAll();
            RefreshViews();
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            if (_canExecute == null)
                return true;

            if (parameter is T value)
                return _canExecute(value);

            return _canExecute(default);
        }

        public void Execute(object? parameter)
        {
            if (parameter is T value)
                _execute(value);
            else
                _execute(default);
        }

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}