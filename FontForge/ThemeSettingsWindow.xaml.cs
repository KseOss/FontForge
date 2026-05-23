using System.Windows;
using System.Windows.Controls;

namespace FontForge
{
    public partial class ThemeSettingsWindow : Window
    {
        private bool _isLoading = true;

        public ThemeSettingsWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentSettingsToControls();

            _isLoading = false;
        }

        private void LoadCurrentSettingsToControls()
        {
            AppThemeSettings settings = App.CurrentThemeSettings.Clone();
            settings.Normalize();

            SelectComboByTag(ThemeModeCombo, settings.ThemeMode);
            SelectComboByTag(AccentCombo, settings.AccentName);
            SelectComboByTag(BackgroundCombo, settings.BackgroundStyle);
            SelectComboByTag(ButtonStyleCombo, settings.ButtonStyle);

            SelectComboByTag(StartWindowCombo, settings.StartWindow);
            SelectComboByTag(AfterCreateFontCombo, settings.AfterCreateFont);
            SelectComboByTag(ConfirmMoveToTrashCombo, settings.ConfirmMoveToTrash ? "True" : "False");
            SelectComboByTag(SplashModeCombo, settings.ShowFullSplashEveryStart ? "AlwaysFull" : "FirstLaunchOnly");
        }

        private void Settings_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading)
                return;

            ApplySettingsFromControls();
        }

        private void ApplySettingsFromControls()
        {
            AppThemeSettings oldSettings = App.CurrentThemeSettings.Clone();

            var settings = new AppThemeSettings
            {
                ThemeMode = GetSelectedTag(ThemeModeCombo, "Light"),
                AccentName = GetSelectedTag(AccentCombo, "Neutral"),
                BackgroundStyle = GetSelectedTag(BackgroundCombo, "Plain"),
                ButtonStyle = GetSelectedTag(ButtonStyleCombo, "Auto"),

                StartWindow = GetSelectedTag(StartWindowCombo, "FontsWindow"),
                AfterCreateFont = GetSelectedTag(AfterCreateFontCombo, "OpenEditor"),
                ConfirmMoveToTrash = GetSelectedTag(ConfirmMoveToTrashCombo, "True") == "True",

                ShowFullSplashEveryStart = GetSelectedTag(SplashModeCombo, "FirstLaunchOnly") == "AlwaysFull",

                HasCompletedFirstLaunch = oldSettings.HasCompletedFirstLaunch
            };

            settings.Normalize();

            App.SetThemeSettings(settings, save: true);
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _isLoading = true;

            var oldSettings = App.CurrentThemeSettings.Clone();
            var settings = new AppThemeSettings
            {
                HasCompletedFirstLaunch = oldSettings.HasCompletedFirstLaunch
            };

            SelectComboByTag(ThemeModeCombo, settings.ThemeMode);
            SelectComboByTag(AccentCombo, settings.AccentName);
            SelectComboByTag(BackgroundCombo, settings.BackgroundStyle);
            SelectComboByTag(ButtonStyleCombo, settings.ButtonStyle);

            SelectComboByTag(StartWindowCombo, settings.StartWindow);
            SelectComboByTag(AfterCreateFontCombo, settings.AfterCreateFont);
            SelectComboByTag(ConfirmMoveToTrashCombo, settings.ConfirmMoveToTrash ? "True" : "False");
            SelectComboByTag(SplashModeCombo, settings.ShowFullSplashEveryStart ? "AlwaysFull" : "FirstLaunchOnly");

            _isLoading = false;

            App.SetThemeSettings(settings, save: true);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static string GetSelectedTag(ComboBox comboBox, string fallback)
        {
            if (comboBox.SelectedItem is ComboBoxItem item &&
                item.Tag is string tag &&
                !string.IsNullOrWhiteSpace(tag))
            {
                return tag;
            }

            return fallback;
        }

        private static void SelectComboByTag(ComboBox comboBox, string tag)
        {
            foreach (object item in comboBox.Items)
            {
                if (item is ComboBoxItem comboBoxItem &&
                    comboBoxItem.Tag is string itemTag &&
                    itemTag == tag)
                {
                    comboBox.SelectedItem = comboBoxItem;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }
    }
}