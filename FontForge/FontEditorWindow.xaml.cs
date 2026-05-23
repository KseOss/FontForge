using FontForge.Classes;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FontForge
{
    public partial class FontEditorWindow : Window
    {
        private readonly Guid _fontId;
        private List<CreatedFont> _allFonts = new();
        private CreatedFont? _font;

        public ObservableCollection<GlyphTileVm> Glyphs { get; } = new ObservableCollection<GlyphTileVm>();

        public FontEditorWindow(Guid fontId)
        {
            InitializeComponent();

            DataContext = this;

            _fontId = fontId;

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
            RebuildPreview();
        }

        private void LoadFont()
        {
            _allFonts = FontStorage.LoadFonts();
            _font = _allFonts.FirstOrDefault(f => f.Id == _fontId);

            if (_font == null)
            {
                AppDialog.Warning(this, "Шрифт не найден.");
                Close();
                return;
            }

            FontNameText.Text = _font.Name;

            if (_font.Glyphs != null)
            {
                foreach (GlyphEntry glyph in _font.Glyphs)
                {
                    FontStorage.NormalizeDefaults(_font, glyph.Char);
                }
            }

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

            if (_font == null || _font.Glyphs == null)
            {
                GlyphCountText.Text = "Символов: 0";
                return;
            }

            foreach (GlyphEntry glyph in _font.Glyphs.OrderBy(x => x.Char, StringComparer.Ordinal))
            {
                string imagePath = GlyphImageResolver.FindGlyphImagePath(
                    _font,
                    glyph.Char,
                    allowLookAlikeFallback: false) ?? "";

                bool hasDefaultVariant =
                    glyph.Variants != null &&
                    glyph.Variants.Any(v => v.IsDefault);

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

            var dialog = new SelectLetterDialog(used)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() != true)
                return;

            string selected = dialog.SelectedChar ?? "";

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

        private void GlyphCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_font == null)
                return;

            if (sender is Border border && border.DataContext is GlyphTileVm vm)
                OpenVariants(vm.Char);
        }

        private void DeleteGlyph_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (_font == null)
                return;

            if (sender is not Button button || button.DataContext is not GlyphTileVm vm)
                return;

            GlyphEntry? glyph = _font.Glyphs.FirstOrDefault(g => g.Char == vm.Char);

            if (glyph == null)
                return;

            MessageBoxResult result = AppDialog.Show(
                this,
                "Удаление символа",
                $"Удалить символ «{glyph.Char}» из шрифта?\n\nВсе варианты этой буквы тоже будут удалены.",
                MessageBoxButton.YesNo,
                AppDialogKind.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            DeleteGlyphFiles(_font, glyph);

            _font.Glyphs.Remove(glyph);

            SaveFont();
            RefreshGlyphTiles();
            RebuildPreview();
        }

        private static void DeleteGlyphFiles(CreatedFont font, GlyphEntry glyph)
        {
            var pathsToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (glyph.Variants != null)
            {
                foreach (GlyphVariant variant in glyph.Variants)
                {
                    if (!string.IsNullOrWhiteSpace(variant.ImagePath))
                    {
                        pathsToDelete.Add(variant.ImagePath);

                        string isf = Path.ChangeExtension(variant.ImagePath, ".isf");

                        if (!string.IsNullOrWhiteSpace(isf))
                            pathsToDelete.Add(isf);
                    }
                }
            }

            try
            {
                string folder = FontStorage.GetGlyphsFolder(font.Id);

                if (Directory.Exists(folder))
                {
                    string prefix = BuildGlyphFilePrefix(glyph.Char);

                    foreach (string file in Directory.GetFiles(folder, prefix + "*.*"))
                    {
                        string ext = Path.GetExtension(file);

                        if (string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ext, ".isf", StringComparison.OrdinalIgnoreCase))
                        {
                            pathsToDelete.Add(file);
                        }
                    }
                }
            }
            catch
            {
                // Если не получилось найти папку — удалим хотя бы известные пути.
            }

            foreach (string path in pathsToDelete)
            {
                TryDeleteFile(path);
            }
        }

        private static string BuildGlyphFilePrefix(string ch)
        {
            int code = ch.Length > 0
                ? char.ConvertToUtf32(ch, 0)
                : 0;

            return "U" + code.ToString("X4") + "_";
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Не мешаем удалению символа из списка, если какой-то файл временно занят.
            }
        }

        private void OpenVariants(string ch)
        {
            if (_font == null)
                return;

            var win = new GlyphVariantsWindow(_font.Id, ch)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

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

        private static FrameworkElement CreatePngGlyphPreviewOrFallback(
            string symbol,
            string imagePath,
            double size)
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
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
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
                Foreground = GetPreviewTextBrush(),
                Margin = new Thickness(2, 0, 2, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static Brush GetPreviewTextBrush()
        {
            try
            {
                if (Application.Current.Resources.Contains("PreviewTextBrush") &&
                    Application.Current.Resources["PreviewTextBrush"] is Brush brush)
                {
                    return brush;
                }
            }
            catch
            {
                // ignore
            }

            return Brushes.Black;
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

        private void ExportFontFile_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null)
            {
                AppDialog.Warning(this, "Шрифт не загружен.", "Экспорт шрифта");
                return;
            }

            LoadFont();

            if (_font == null)
                return;

            var saveFileDialog = new SaveFileDialog
            {
                Title = "Сохранить шрифт",
                Filter = "TrueType Font (*.ttf)|*.ttf|OpenType Font (*.otf)|*.otf",
                FileName = BuildSafeFontFileName(_font.Name) + ".ttf",
                DefaultExt = ".ttf",
                AddExtension = true
            };

            if (saveFileDialog.ShowDialog() != true)
                return;

            try
            {
                FontExportResult result = TrueTypeFontExporter.Export(_font, saveFileDialog.FileName);

                string message =
                    "Шрифт успешно сохранён.\n\n" +
                    $"Экспортировано символов: {result.ExportedGlyphCount}";

                if (result.SkippedGlyphCount > 0)
                {
                    message +=
                        "\nПропущено символов: " + result.SkippedGlyphCount +
                        "\n\nНекоторые символы могли быть пропущены, если у них не найден .isf-файл с контурами.";
                }

                AppDialog.Success(this, message, "Экспорт шрифта");
            }
            catch (Exception ex)
            {
                AppDialog.Error(this, "Ошибка при экспорте шрифта:\n" + ex.Message, "Экспорт шрифта");
            }
        }

        private static string BuildSafeFontFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "MyFont";

            string safe = name;

            foreach (char c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');

            return string.IsNullOrWhiteSpace(safe)
                ? "MyFont"
                : safe;
        }

        private void OpenDocumentEditor_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null)
            {
                AppDialog.Warning(this, "Шрифт не загружен.");
                return;
            }

            var win = new DocumentEditorWindow(_font.Id)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
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