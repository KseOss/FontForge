using FontForge.Classes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace FontForge
{
    public partial class FontEditorWindow : Window
    {
        private readonly Guid _fontId;
        private List<CreatedFont> _allFonts = new();
        private CreatedFont? _font;

        public ObservableCollection<GlyphTileVm> Glyphs { get; } = new ObservableCollection<GlyphTileVm>();

        private bool _isThemeAnimating = false;

        public FontEditorWindow(Guid fontId)
        {
            InitializeComponent();
            DataContext = this;

            _fontId = fontId;

            ThemeToggleButton.IsChecked = !App.IsDarkTheme;
            App.ThemeChanged += OnThemeChanged;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadFont();
            RefreshGlyphTiles();
            RebuildPreview();
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            App.ThemeChanged -= OnThemeChanged;
        }

        private void Back_Click(object sender, RoutedEventArgs e) => Close();

        private void OnThemeChanged()
        {
            ThemeToggleButton.IsChecked = !App.IsDarkTheme;
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isThemeAnimating) return;
            _isThemeAnimating = true;
            ThemeToggleButton.IsEnabled = false;

            bool wantLight = ThemeToggleButton.IsChecked == true;
            bool wantDark = !wantLight;

            var fadeTo = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
            fadeTo.Completed += (_, __) =>
            {
                App.SetTheme(wantDark);

                var fadeBack = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
                fadeBack.Completed += (___, ____) =>
                {
                    ThemeToggleButton.IsEnabled = true;
                    _isThemeAnimating = false;
                };

                FadeOverlay.BeginAnimation(OpacityProperty, fadeBack);
            };

            FadeOverlay.BeginAnimation(OpacityProperty, fadeTo);
        }

        private void LoadFont()
        {
            _allFonts = FontStorage.LoadFonts();
            _font = _allFonts.FirstOrDefault(f => f.Id == _fontId);

            if (_font == null)
            {
                MessageBox.Show("Шрифт не найден.");
                Close();
                return;
            }

            FontNameText.Text = _font.Name;

            foreach (var g in _font.Glyphs)
                FontStorage.NormalizeDefaults(_font, g.Char);

            SaveFont();
        }

        private void SaveFont()
        {
            if (_font == null) return;

            var idx = _allFonts.FindIndex(f => f.Id == _font.Id);
            if (idx >= 0) _allFonts[idx] = _font;

            FontStorage.SaveFonts(_allFonts);
        }

        // ===== Плитки символов =====
        private void RefreshGlyphTiles()
        {
            Glyphs.Clear();
            if (_font == null) return;

            foreach (var g in _font.Glyphs.OrderBy(x => x.Char))
            {
                var def = g.Variants.FirstOrDefault(v => v.IsDefault) ?? g.Variants.FirstOrDefault();
                bool isDefaultExists = def != null && def.IsDefault;

                Glyphs.Add(new GlyphTileVm
                {
                    Char = g.Char,
                    DefaultImagePath = def?.ImagePath ?? "",
                    IsDefault = isDefaultExists
                });
            }

            GlyphCountText.Text = $"Символов: {_font.Glyphs.Count}";
        }

        private void AddGlyph_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null) return;

            var used = new HashSet<string>(_font.Glyphs.Select(g => g.Char));
            var dlg = new SelectLetterDialog(used) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            string selected = dlg.SelectedChar ?? "";
            if (string.IsNullOrWhiteSpace(selected)) return;

            var entry = new GlyphEntry
            {
                Char = selected,
                UpdatedAt = DateTime.Now
            };
            _font.Glyphs.Add(entry);

            SaveFont();
            RefreshGlyphTiles();
            RebuildPreview();

            OpenVariants(selected);
        }

        private void GlyphTile_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null) return;

            if (sender is Button b && b.DataContext is GlyphTileVm vm)
                OpenVariants(vm.Char);
        }

        private void OpenVariants(string ch)
        {
            if (_font == null) return;

            var win = new GlyphVariantsWindow(_font.Id, ch) { Owner = this };
            win.ShowDialog();

            LoadFont();
            RefreshGlyphTiles();
            RebuildPreview();
        }

        // ===== Предпросмотр =====
        private void PreviewInput_TextChanged(object sender, TextChangedEventArgs e) => RebuildPreview();
        private void PreviewSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => RebuildPreview();

        private void RebuildPreview()
        {
            if (PreviewRenderPanel == null || PreviewInput == null || PreviewSizeSlider == null)
                return;

            PreviewRenderPanel.Children.Clear();
            if (_font == null) return;

            string text = PreviewInput.Text ?? "";
            double size = PreviewSizeSlider.Value;

            foreach (char ch in text)
            {
                string s = ch.ToString();

                if (ch == ' ')
                {
                    PreviewRenderPanel.Children.Add(new Border { Width = size * 0.35 });
                    continue;
                }

                var glyph = _font.Glyphs.FirstOrDefault(g => g.Char == s);
                var variant = glyph?.Variants.FirstOrDefault(v => v.IsDefault) ?? glyph?.Variants.FirstOrDefault();
                string path = variant?.ImagePath ?? "";

                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    var img = new Image
                    {
                        Width = size,
                        Height = size,
                        Stretch = Stretch.Uniform,
                        Margin = new Thickness(2, 0, 2, 0),
                    };

                    try
                    {
                        img.Source = LoadBitmapNoLock(path);
                        PreviewRenderPanel.Children.Add(img);
                    }
                    catch
                    {
                        PreviewRenderPanel.Children.Add(MakeFallbackText(s, size));
                    }
                }
                else
                {
                    PreviewRenderPanel.Children.Add(MakeFallbackText(s, size));
                }
            }
        }

        private static BitmapSource LoadBitmapNoLock(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.StreamSource = fs;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private TextBlock MakeFallbackText(string s, double size)
        {
            return new TextBlock
            {
                Text = s,
                FontSize = size,
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                Margin = new Thickness(2, 0, 2, 0)
            };
        }

        private void ClearText_Click(object sender, RoutedEventArgs e)
        {
            PreviewInput.Clear();
            RebuildPreview();
        }

        // ✅ ОТДЕЛЬНОЕ ОКНО “как Word” + PDF
        private void OpenDocumentEditor_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null)
            {
                MessageBox.Show("Шрифт не загружен.");
                return;
            }

            var win = new DocumentEditorWindow(_font.Id)
            {
                Owner = this
            };
            win.ShowDialog();
        }
    }

    public class GlyphTileVm
    {
        public string Char { get; set; } = "";
        public string DefaultImagePath { get; set; } = "";
        public bool IsDefault { get; set; }
    }
}
