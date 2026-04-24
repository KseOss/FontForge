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
            SetPreviewInputTextFromCreatedGlyphsIfNeeded();
            RefreshGlyphTiles();
            RebuildPreview();
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            App.ThemeChanged -= OnThemeChanged;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnThemeChanged()
        {
            ThemeToggleButton.IsChecked = !App.IsDarkTheme;
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isThemeAnimating)
                return;

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

            foreach (var glyph in _font.Glyphs)
                FontStorage.NormalizeDefaults(_font, glyph.Char);

            SaveFont();
        }

        private void SaveFont()
        {
            if (_font == null)
                return;

            int idx = _allFonts.FindIndex(f => f.Id == _font.Id);

            if (idx >= 0)
                _allFonts[idx] = _font;

            FontStorage.SaveFonts(_allFonts);
        }

        private void RefreshGlyphTiles()
        {
            Glyphs.Clear();

            if (_font == null)
                return;

            foreach (var glyph in _font.Glyphs.OrderBy(x => x.Char, StringComparer.Ordinal))
            {
                string imagePath = GlyphImageResolver.FindGlyphImagePath(
                    _font,
                    glyph.Char,
                    allowLookAlikeFallback: false) ?? "";

                bool hasDefaultVariant = glyph.Variants.Any(v => v.IsDefault);

                Glyphs.Add(new GlyphTileVm
                {
                    Char = glyph.Char,
                    DefaultImagePath = imagePath,
                    IsDefault = hasDefaultVariant
                });
            }

            GlyphCountText.Text = $"Символов: {_font.Glyphs.Count}";
        }

        private void AddGlyph_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null)
                return;

            var used = new HashSet<string>(_font.Glyphs.Select(g => g.Char));
            var dlg = new SelectLetterDialog(used) { Owner = this };

            if (dlg.ShowDialog() != true)
                return;

            string selected = dlg.SelectedChar ?? "";

            if (string.IsNullOrWhiteSpace(selected))
                return;

            var entry = new GlyphEntry
            {
                Char = selected,
                UpdatedAt = DateTime.Now
            };

            _font.Glyphs.Add(entry);

            SaveFont();
            RefreshGlyphTiles();
            SetPreviewInputTextFromCreatedGlyphsIfNeeded();
            RebuildPreview();

            OpenVariants(selected);
        }

        private void GlyphTile_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null)
                return;

            if (sender is Button button && button.DataContext is GlyphTileVm vm)
                OpenVariants(vm.Char);
        }

        private void OpenVariants(string ch)
        {
            if (_font == null)
                return;

            var win = new GlyphVariantsWindow(_font.Id, ch) { Owner = this };
            win.ShowDialog();

            LoadFont();
            SetPreviewInputTextFromCreatedGlyphsIfNeeded();
            RefreshGlyphTiles();
            RebuildPreview();
        }

        private void PreviewInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            RebuildPreview();
        }

        private void PreviewSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            RebuildPreview();
        }

        private void RebuildPreview()
        {
            if (PreviewRenderPanel == null || PreviewInput == null || PreviewSizeSlider == null)
                return;

            PreviewRenderPanel.Children.Clear();

            if (_font == null)
                return;

            string text = PreviewInput.Text ?? "";
            double size = PreviewSizeSlider.Value;

            var root = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            string[] lines = SplitLines(text);

            foreach (string line in lines)
            {
                var linePanel = new WrapPanel
                {
                    Margin = new Thickness(0, 0, 0, size * 0.20)
                };

                if (line.Length == 0)
                {
                    root.Children.Add(new Border
                    {
                        Height = size * 0.8
                    });

                    continue;
                }

                foreach (char ch in line)
                {
                    if (ch == ' ')
                    {
                        linePanel.Children.Add(new Border
                        {
                            Width = size * 0.45,
                            Height = size
                        });

                        continue;
                    }

                    if (ch == '\t')
                    {
                        linePanel.Children.Add(new Border
                        {
                            Width = size * 1.5,
                            Height = size
                        });

                        continue;
                    }

                    string symbol = ch.ToString();
                    string? imagePath = GlyphImageResolver.FindGlyphImagePath(_font, symbol);

                    if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                    {
                        linePanel.Children.Add(CreatePngGlyphPreviewOrFallback(symbol, imagePath, size));
                    }
                    else
                    {
                        linePanel.Children.Add(CreateFallbackText(symbol, size));
                    }
                }

                root.Children.Add(linePanel);
            }

            PreviewRenderPanel.Children.Add(root);
        }

        private static FrameworkElement CreatePngGlyphPreviewOrFallback(string symbol, string imagePath, double size)
        {
            try
            {
                var border = new Border
                {
                    Width = size,
                    Height = size,
                    Margin = new Thickness(2, 0, 2, 0),
                    Background = Brushes.Transparent,
                    ToolTip = $"Символ: {symbol}\nPNG:\n{imagePath}"
                };

                var image = new Image
                {
                    Source = LoadBitmapForPreview(imagePath),
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };

                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                border.Child = image;
                return border;
            }
            catch
            {
                return CreateFallbackText(symbol, size);
            }
        }

        private static TextBlock CreateFallbackText(string symbol, double size)
        {
            return new TextBlock
            {
                Text = symbol,
                FontSize = size * 0.72,
                Foreground = (Brush)Application.Current.Resources["PreviewTextBrush"],
                Margin = new Thickness(2, 0, 2, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
        }

        private static BitmapSource LoadBitmapForPreview(string path)
        {
            var bmp = new BitmapImage();

            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();

            return bmp;
        }

        private static string[] SplitLines(string text)
        {
            return (text ?? "")
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');
        }

        private void SetPreviewInputTextFromCreatedGlyphsIfNeeded()
        {
            if (_font == null || PreviewInput == null)
                return;

            string current = PreviewInput.Text ?? "";

            bool shouldReplace =
                string.IsNullOrWhiteSpace(current)
                || current.Contains("АБВГДЕ", StringComparison.Ordinal)
                || current.StartsWith("Символы вашего шрифта:", StringComparison.Ordinal);

            if (!shouldReplace)
                return;

            var chars = GlyphImageResolver.GetDrawableChars(_font);

            if (chars.Count == 0)
                return;

            PreviewInput.Text = string.Join(" ", chars);
        }

        private void ClearText_Click(object sender, RoutedEventArgs e)
        {
            PreviewInput.Clear();
            RebuildPreview();
        }

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