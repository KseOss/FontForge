using FontForge.Classes;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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

        private const int TemplateWidth = 2480;
        private const int TemplateHeight = 3508;

        private const double TemplateMargin = 120.0;
        private const double TemplateGridTop = 430.0;
        private const double TemplateGridBottom = 155.0;

        private const int TemplateColumns = 5;
        private const int TemplateRows = 7;
        private const int TemplateItemsPerPage = TemplateColumns * TemplateRows;

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

            if (_font.Glyphs == null)
                _font.Glyphs = new List<GlyphEntry>();

            foreach (GlyphEntry glyph in _font.Glyphs)
            {
                if (glyph.Variants == null)
                    glyph.Variants = new List<GlyphVariant>();

                FontStorage.NormalizeDefaults(_font, glyph.Char);
            }

            SaveFont();
        }

        private void ReloadFontFromStorage()
        {
            _allFonts = FontStorage.LoadFonts();
            _font = _allFonts.FirstOrDefault(f => f.Id == _fontId);

            if (_font == null)
                return;

            if (_font.Glyphs == null)
                _font.Glyphs = new List<GlyphEntry>();

            foreach (GlyphEntry glyph in _font.Glyphs)
            {
                if (glyph.Variants == null)
                    glyph.Variants = new List<GlyphVariant>();

                FontStorage.NormalizeDefaults(_font, glyph.Char);
            }

            FontNameText.Text = _font.Name;
        }

        private void SaveFont()
        {
            if (_font == null)
                return;

            if (_font.Glyphs == null)
                _font.Glyphs = new List<GlyphEntry>();

            int index = _allFonts.FindIndex(f => f.Id == _font.Id);

            if (index >= 0)
                _allFonts[index] = _font;
            else
                _allFonts.Add(_font);

            FontStorage.SaveFonts(_allFonts);
        }

        private void RefreshGlyphTiles()
        {
            Glyphs.Clear();

            if (_font == null || _font.Glyphs == null)
            {
                GlyphCountText.Text = "Символов: 0";
                UpdateSelectedGlyphUi();
                return;
            }

            foreach (GlyphEntry glyph in _font.Glyphs.OrderBy(x => x.Char, StringComparer.Ordinal))
            {
                if (glyph.Variants == null)
                    glyph.Variants = new List<GlyphVariant>();

                string imagePath = GlyphImageResolver.FindGlyphImagePath(
                    _font,
                    glyph.Char,
                    allowLookAlikeFallback: false) ?? "";

                bool hasDefaultVariant = glyph.Variants.Any(v => v.IsDefault);

                Glyphs.Add(new GlyphTileVm
                {
                    Char = glyph.Char,
                    DefaultImagePath = imagePath,
                    IsDefault = hasDefaultVariant,
                    IsSelected = false
                });
            }

            GlyphCountText.Text = $"Символов: {_font.Glyphs.Count}";
            UpdateSelectedGlyphUi();
        }

        private void UpdateSelectedGlyphUi()
        {
            if (SelectedGlyphCountText == null || DeleteSelectedButton == null || ClearSelectionButton == null)
                return;

            int selected = Glyphs.Count(x => x.IsSelected);

            SelectedGlyphCountText.Text = selected == 0
                ? "Выбранных символов нет"
                : $"Выбрано символов: {selected}";

            DeleteSelectedButton.IsEnabled = selected > 0;
            ClearSelectionButton.IsEnabled = selected > 0;
        }

        private void GlyphCheckBox_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            UpdateSelectedGlyphUi();
        }

        private void SelectAllGlyphs_Click(object sender, RoutedEventArgs e)
        {
            foreach (GlyphTileVm glyph in Glyphs)
                glyph.IsSelected = true;

            UpdateSelectedGlyphUi();
        }

        private void ClearGlyphSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (GlyphTileVm glyph in Glyphs)
                glyph.IsSelected = false;

            UpdateSelectedGlyphUi();
        }

        private void DeleteSelectedGlyphs_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null || _font.Glyphs == null)
                return;

            List<string> selectedChars = Glyphs
                .Where(x => x.IsSelected)
                .Select(x => x.Char)
                .Distinct()
                .ToList();

            if (selectedChars.Count == 0)
            {
                AppDialog.Info(this, "Выберите хотя бы один символ.", "Удаление символов");
                return;
            }

            string charsText = string.Join(", ", selectedChars.Select(x => $"«{x}»"));

            MessageBoxResult result = AppDialog.Show(
                this,
                "Удаление символов",
                $"Удалить выбранные символы?\n\n{charsText}\n\nВсе варианты этих символов тоже будут удалены.",
                MessageBoxButton.YesNo,
                AppDialogKind.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            foreach (string ch in selectedChars)
            {
                GlyphEntry? glyph = _font.Glyphs.FirstOrDefault(g => g.Char == ch);

                if (glyph == null)
                    continue;

                DeleteGlyphFiles(_font, glyph);
                _font.Glyphs.Remove(glyph);
            }

            SaveFont();
            RefreshGlyphTiles();
            RebuildPreview();

            AppDialog.Success(this, $"Удалено символов: {selectedChars.Count}", "Удаление символов");
        }

        private void EditGlyphFromTile_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is Button button && button.DataContext is GlyphTileVm vm)
            {
                OpenVariants(vm.Char);
            }
        }

        private void ForceRefreshAfterImport()
        {
            SaveFont();
            ReloadFontFromStorage();
            RefreshGlyphTiles();
            SetPreviewInputTextFromCreatedGlyphsIfNeeded();
            RebuildPreview();
        }

        private void AddGlyph_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null)
                return;

            if (_font.Glyphs == null)
                _font.Glyphs = new List<GlyphEntry>();

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
                UpdatedAt = DateTime.Now,
                Variants = new List<GlyphVariant>()
            };

            _font.Glyphs.Add(entry);

            SaveFont();
            RefreshGlyphTiles();
            SetPreviewInputTextFromCreatedGlyphsIfNeeded();
            RebuildPreview();

            OpenVariants(selected);
        }

        private void DeleteGlyph_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (_font == null || _font.Glyphs == null)
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
            }
        }

        private void OpenVariants(string ch)
        {
            if (_font == null)
                return;

            var win = new GlyphVariantsWindow(_font.Id, ch)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false
            };

            try
            {
                Hide();
                win.ShowDialog();
            }
            finally
            {
                LoadFont();
                SetPreviewInputTextFromCreatedGlyphsIfNeeded();
                RefreshGlyphTiles();
                RebuildPreview();

                Show();
                WindowState = WindowState.Normal;
                Activate();
            }
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
                    return brush;
            }
            catch
            {
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

        // ============================================================
        // PDF-ШАБЛОНЫ
        // ============================================================

        private void CreateTemplatePdf_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null)
            {
                AppDialog.Warning(this, "Шрифт не загружен.", "PDF-шаблон");
                return;
            }

            List<string> allSymbols = BuildTemplateSymbols();
            List<string> lastSymbols = LoadLastTemplateSymbols();

            var symbolDialog = new TemplateSymbolsDialog(
                allSymbols,
                lastSymbols.Count > 0 ? lastSymbols : null,
                selectAllByDefault: lastSymbols.Count == 0)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Title = "Выбор символов для PDF"
            };

            if (symbolDialog.ShowDialog() != true)
                return;

            List<string> selectedSymbols = symbolDialog.SelectedSymbols;

            if (selectedSymbols.Count == 0)
            {
                AppDialog.Warning(this, "Выберите хотя бы один символ.", "PDF-шаблон");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Title = "Сохранить PDF-шаблон",
                Filter = "PDF шаблон (*.pdf)|*.pdf",
                FileName = BuildSafeFontFileName(_font.Name) + "_template.pdf",
                DefaultExt = ".pdf",
                AddExtension = true
            };

            if (saveDialog.ShowDialog() != true)
                return;

            try
            {
                SimplePdfTemplateWriter.SaveTemplatePdf(
                    saveDialog.FileName,
                    _font.Name,
                    selectedSymbols);

                SaveLastTemplateSymbols(selectedSymbols);
                SaveSidecarSymbols(saveDialog.FileName, selectedSymbols);

                AppDialog.Success(
                    this,
                    "PDF-шаблон успешно создан.\n\n" +
                    $"Символов в шаблоне: {selectedSymbols.Count}\n\n" +
                    "После заполнения нажмите «Загрузить PDF».",
                    "PDF-шаблон");
            }
            catch (Exception ex)
            {
                AppDialog.Error(this, "Ошибка при создании PDF-шаблона:\n" + ex.Message, "PDF-шаблон");
            }
        }

        private void ImportTemplatePdf_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null)
            {
                AppDialog.Warning(this, "Шрифт не загружен.", "Загрузка PDF");
                return;
            }

            var openDialog = new OpenFileDialog
            {
                Title = "Загрузить заполненный PDF-шаблон",
                Filter = "PDF-шаблон (*.pdf)|*.pdf",
                Multiselect = false
            };

            if (openDialog.ShowDialog() != true)
                return;

            try
            {
                List<BitmapSource> pages = SimplePdfImageExtractor.ExtractPageImages(openDialog.FileName);
                List<List<PdfInkStroke>> inkPages = SimplePdfImageExtractor.ExtractInkPages(
                    openDialog.FileName,
                    TemplateWidth,
                    TemplateHeight);

                if (pages.Count == 0 && inkPages.Count == 0)
                {
                    AppDialog.Warning(
                        this,
                        "Не удалось найти ни изображение страницы, ни нарисованные линии внутри PDF.",
                        "Загрузка PDF");

                    return;
                }

                List<string> symbols = ResolveTemplateSymbolsForImport(openDialog.FileName);

                if (symbols.Count == 0)
                    return;

                MessageBoxResult confirm = AppDialog.Show(
                    this,
                    "Загрузка PDF",
                    $"Символов для загрузки: {symbols.Count}\n\nПродолжить?",
                    MessageBoxButton.YesNo,
                    AppDialogKind.Question);

                if (confirm != MessageBoxResult.Yes)
                    return;

                int imported = 0;
                int skipped = 0;

                if (_font.Glyphs == null)
                    _font.Glyphs = new List<GlyphEntry>();

                int pageCount = Math.Max(pages.Count, inkPages.Count);

                for (int page = 0; page < pageCount; page++)
                {
                    BitmapSource? pageImage = null;

                    if (page < pages.Count)
                        pageImage = NormalizeTemplatePageImage(pages[page]);

                    List<PdfInkStroke> pageInk = page < inkPages.Count
                        ? inkPages[page]
                        : new List<PdfInkStroke>();

                    int pageStartIndex = page * TemplateItemsPerPage;

                    for (int localIndex = 0; localIndex < TemplateItemsPerPage; localIndex++)
                    {
                        int symbolIndex = pageStartIndex + localIndex;

                        if (symbolIndex >= symbols.Count)
                            break;

                        string symbol = symbols[symbolIndex];

                        BitmapSource? glyphImage = ExtractGlyphFromInkAnnotations(pageInk, localIndex);

                        if (glyphImage == null && pageImage != null)
                            glyphImage = ExtractGlyphFromTemplateCell(pageImage, localIndex, out int _);

                        if (glyphImage == null)
                        {
                            skipped++;
                            continue;
                        }

                        AddTemplateGlyphToFont(symbol, glyphImage);
                        imported++;
                    }
                }

                ForceRefreshAfterImport();

                string loadedSymbolsText = symbols.Count <= 40
                    ? string.Join(", ", symbols.Select(x => $"«{x}»"))
                    : string.Join(", ", symbols.Take(40).Select(x => $"«{x}»")) + " ...";

                AppDialog.Success(
                    this,
                    "Загрузка PDF завершена.\n\n" +
                    $"Добавлено символов: {imported}\n" +
                    $"Пропущено: {skipped}\n\n" +
                    "Символы, которые использовались при загрузке:\n" +
                    loadedSymbolsText,
                    "Загрузка PDF");
            }
            catch (Exception ex)
            {
                AppDialog.Error(this, "Ошибка при загрузке PDF:\n" + ex.Message, "Загрузка PDF");
            }
        }

        private BitmapSource? ExtractGlyphFromInkAnnotations(List<PdfInkStroke> strokes, int localIndex)
        {
            if (strokes == null || strokes.Count == 0)
                return null;

            Rect area = GetTemplateCellDrawArea(localIndex);

            var selectedStrokes = new List<PdfInkStroke>();

            foreach (PdfInkStroke stroke in strokes)
            {
                if (stroke.Points.Any(p => area.Contains(p)))
                    selectedStrokes.Add(stroke);
            }

            if (selectedStrokes.Count == 0)
                return null;

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (PdfInkStroke stroke in selectedStrokes)
            {
                foreach (Point p in stroke.Points)
                {
                    if (!area.Contains(p))
                        continue;

                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                }
            }

            if (maxX <= minX || maxY <= minY)
                return null;

            int padding = 35;

            minX = Math.Max(area.Left, minX - padding);
            minY = Math.Max(area.Top, minY - padding);
            maxX = Math.Min(area.Right, maxX + padding);
            maxY = Math.Min(area.Bottom, maxY + padding);

            int width = Math.Max(1, (int)Math.Ceiling(maxX - minX));
            int height = Math.Max(1, (int)Math.Ceiling(maxY - minY));

            var visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                Pen pen = new Pen(Brushes.Black, 12)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };

                foreach (PdfInkStroke stroke in selectedStrokes)
                {
                    List<Point> points = stroke.Points
                        .Where(p => area.Contains(p))
                        .Select(p => new Point(p.X - minX, p.Y - minY))
                        .ToList();

                    if (points.Count < 2)
                        continue;

                    for (int i = 1; i < points.Count; i++)
                    {
                        dc.DrawLine(pen, points[i - 1], points[i]);
                    }
                }
            }

            var bitmap = new RenderTargetBitmap(
                width,
                height,
                96,
                96,
                PixelFormats.Pbgra32);

            bitmap.Render(visual);
            bitmap.Freeze();

            return bitmap;
        }

        private List<string> ResolveTemplateSymbolsForImport(string pdfPath)
        {
            List<string> symbolsFromPdf = SimplePdfTemplateWriter.TryReadTemplateSymbols(pdfPath);

            if (symbolsFromPdf.Count > 0)
            {
                SaveLastTemplateSymbols(symbolsFromPdf);
                SaveSidecarSymbols(pdfPath, symbolsFromPdf);
                return symbolsFromPdf;
            }

            List<string> symbolsFromSidecar = LoadSidecarSymbols(pdfPath);

            if (symbolsFromSidecar.Count > 0)
            {
                SaveLastTemplateSymbols(symbolsFromSidecar);
                return symbolsFromSidecar;
            }

            MessageBoxResult result = AppDialog.Show(
                this,
                "Символы PDF",
                "Программа не смогла автоматически найти список символов внутри PDF.\n\n" +
                "Это бывает, если PDF был пересохранён в другой программе, отсканирован или рядом с ним нет файла .symbols.txt.\n\n" +
                "Сейчас нужно вручную отметить ТОЛЬКО те символы, которые есть в этом PDF-шаблоне.\n\n" +
                "Важно: не нажимайте «Выбрать всё», если в PDF нет всех букв.",
                MessageBoxButton.OKCancel,
                AppDialogKind.Question);

            if (result != MessageBoxResult.OK)
                return new List<string>();

            var selectDialog = new TemplateSymbolsDialog(
                BuildTemplateSymbols(),
                null,
                selectAllByDefault: false)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Title = "Какие символы есть в PDF?"
            };

            if (selectDialog.ShowDialog() != true)
                return new List<string>();

            List<string> selected = selectDialog.SelectedSymbols;

            if (selected.Count == 0)
            {
                AppDialog.Warning(this, "Не выбран список символов для загрузки.", "Загрузка PDF");
                return new List<string>();
            }

            SaveLastTemplateSymbols(selected);
            SaveSidecarSymbols(pdfPath, selected);

            return selected;
        }

        private static List<string> BuildTemplateSymbols()
        {
            var symbols = new List<string>();

            symbols.AddRange("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ".Select(c => c.ToString()));
            symbols.AddRange("абвгдеёжзийклмнопрстуфхцчшщъыьэюя".Select(c => c.ToString()));

            symbols.AddRange("ABCDEFGHIJKLMNOPQRSTUVWXYZ".Select(c => c.ToString()));
            symbols.AddRange("abcdefghijklmnopqrstuvwxyz".Select(c => c.ToString()));
            symbols.AddRange("0123456789".Select(c => c.ToString()));

            symbols.AddRange(new[]
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
            });

            return symbols
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
        }

        private static BitmapSource NormalizeTemplatePageImage(BitmapSource source)
        {
            BitmapSource result = source;

            if (result.PixelWidth > result.PixelHeight)
            {
                var rotate = new TransformedBitmap();

                rotate.BeginInit();
                rotate.Source = result;
                rotate.Transform = new RotateTransform(90);
                rotate.EndInit();
                rotate.Freeze();

                result = rotate;
            }

            if (result.PixelWidth == TemplateWidth && result.PixelHeight == TemplateHeight)
                return EnsureBgra32(result);

            var visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, TemplateWidth, TemplateHeight));

                double scale = Math.Min(
                    TemplateWidth / (double)result.PixelWidth,
                    TemplateHeight / (double)result.PixelHeight);

                double width = result.PixelWidth * scale;
                double height = result.PixelHeight * scale;

                double x = (TemplateWidth - width) / 2.0;
                double y = (TemplateHeight - height) / 2.0;

                dc.DrawImage(result, new Rect(x, y, width, height));
            }

            var bitmap = new RenderTargetBitmap(
                TemplateWidth,
                TemplateHeight,
                96,
                96,
                PixelFormats.Pbgra32);

            bitmap.Render(visual);
            bitmap.Freeze();

            return bitmap;
        }

        private BitmapSource? ExtractGlyphFromTemplateCell(BitmapSource source, int localIndex, out int darkPixels)
        {
            Rect drawArea = GetTemplateCellDrawArea(localIndex);

            double scaleX = source.PixelWidth / (double)TemplateWidth;
            double scaleY = source.PixelHeight / (double)TemplateHeight;

            int x = ClampToInt(drawArea.X * scaleX, 0, source.PixelWidth - 1);
            int y = ClampToInt(drawArea.Y * scaleY, 0, source.PixelHeight - 1);
            int width = ClampToInt(drawArea.Width * scaleX, 1, source.PixelWidth - x);
            int height = ClampToInt(drawArea.Height * scaleY, 1, source.PixelHeight - y);

            CroppedBitmap crop = new CroppedBitmap(
                source,
                new Int32Rect(x, y, width, height));

            BitmapSource transparent = ConvertTemplateCropToTransparentGlyph(crop, out darkPixels);

            if (darkPixels < 3)
                return null;

            BitmapSource cleaned = RemoveTinyNoise(transparent);
            BitmapSource trimmed = TrimTransparentBitmap(cleaned);

            return trimmed;
        }

        private static Rect GetTemplateCellDrawArea(int localIndex)
        {
            double cellGap = 26.0;

            double gridHeight = TemplateHeight - TemplateGridTop - TemplateGridBottom;

            double cellWidth =
                (TemplateWidth - TemplateMargin * 2 - cellGap * (TemplateColumns - 1)) / TemplateColumns;

            double cellHeight =
                (gridHeight - cellGap * (TemplateRows - 1)) / TemplateRows;

            int col = localIndex % TemplateColumns;
            int row = localIndex / TemplateColumns;

            double cellX = TemplateMargin + col * (cellWidth + cellGap);
            double cellY = TemplateGridTop + row * (cellHeight + cellGap);

            return new Rect(
                cellX + 34,
                cellY + 118,
                cellWidth - 68,
                cellHeight - 154);
        }

        private static int ClampToInt(double value, int min, int max)
        {
            int result = (int)Math.Round(value);

            if (result < min)
                return min;

            if (result > max)
                return max;

            return result;
        }

        private static BitmapSource ConvertTemplateCropToTransparentGlyph(BitmapSource source, out int darkPixels)
        {
            BitmapSource formatted = EnsureBgra32(source);

            int width = formatted.PixelWidth;
            int height = formatted.PixelHeight;
            int stride = width * 4;

            byte[] pixels = new byte[stride * height];
            byte[] output = new byte[stride * height];

            formatted.CopyPixels(pixels, stride, 0);

            darkPixels = 0;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i + 0];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];
                byte a = pixels[i + 3];

                if (a < 20)
                {
                    MakeTransparent(output, i);
                    continue;
                }

                int max = Math.Max(r, Math.Max(g, b));
                int min = Math.Min(r, Math.Min(g, b));
                int brightness = (r + g + b) / 3;
                int saturation = max - min;

                bool isAlmostGray = saturation <= 12;
                bool isTemplateGray = isAlmostGray && brightness >= 105;
                bool isDarkInk = brightness < 135;
                bool isColoredInk = saturation > 35 && brightness < 235;

                bool isRealInk =
                    !isTemplateGray &&
                    (isDarkInk || isColoredInk);

                if (isRealInk)
                {
                    output[i + 0] = 0;
                    output[i + 1] = 0;
                    output[i + 2] = 0;
                    output[i + 3] = 255;

                    darkPixels++;
                }
                else
                {
                    MakeTransparent(output, i);
                }
            }

            WriteableBitmap wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            wb.WritePixels(new Int32Rect(0, 0, width, height), output, stride, 0);
            wb.Freeze();

            return wb;
        }

        private static void MakeTransparent(byte[] output, int index)
        {
            output[index + 0] = 0;
            output[index + 1] = 0;
            output[index + 2] = 0;
            output[index + 3] = 0;
        }

        private static BitmapSource RemoveTinyNoise(BitmapSource source)
        {
            BitmapSource formatted = EnsureBgra32(source);

            int width = formatted.PixelWidth;
            int height = formatted.PixelHeight;
            int stride = width * 4;

            byte[] pixels = new byte[stride * height];
            byte[] output = new byte[stride * height];

            formatted.CopyPixels(pixels, stride, 0);

            bool[,] visited = new bool[width, height];

            int[] dx = { -1, 0, 1, 0 };
            int[] dy = { 0, -1, 0, 1 };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (visited[x, y])
                        continue;

                    int startIndex = y * stride + x * 4;

                    if (pixels[startIndex + 3] <= 20)
                    {
                        visited[x, y] = true;
                        continue;
                    }

                    var queue = new Queue<PointInt>();
                    var component = new List<PointInt>();

                    queue.Enqueue(new PointInt(x, y));
                    visited[x, y] = true;

                    int minX = x;
                    int maxX = x;
                    int minY = y;
                    int maxY = y;

                    while (queue.Count > 0)
                    {
                        PointInt p = queue.Dequeue();
                        component.Add(p);

                        if (p.X < minX) minX = p.X;
                        if (p.X > maxX) maxX = p.X;
                        if (p.Y < minY) minY = p.Y;
                        if (p.Y > maxY) maxY = p.Y;

                        for (int i = 0; i < 4; i++)
                        {
                            int nx = p.X + dx[i];
                            int ny = p.Y + dy[i];

                            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                                continue;

                            if (visited[nx, ny])
                                continue;

                            int ni = ny * stride + nx * 4;

                            if (pixels[ni + 3] <= 20)
                            {
                                visited[nx, ny] = true;
                                continue;
                            }

                            visited[nx, ny] = true;
                            queue.Enqueue(new PointInt(nx, ny));
                        }
                    }

                    int compWidth = maxX - minX + 1;
                    int compHeight = maxY - minY + 1;
                    int area = component.Count;

                    bool looksLikeGuideLine =
                        compWidth > 40 &&
                        compHeight <= 3;

                    bool looksLikeTinyNoise =
                        area < 8;

                    if (looksLikeGuideLine || looksLikeTinyNoise)
                        continue;

                    foreach (PointInt p in component)
                    {
                        int pi = p.Y * stride + p.X * 4;

                        output[pi + 0] = pixels[pi + 0];
                        output[pi + 1] = pixels[pi + 1];
                        output[pi + 2] = pixels[pi + 2];
                        output[pi + 3] = pixels[pi + 3];
                    }
                }
            }

            WriteableBitmap wb = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            wb.WritePixels(new Int32Rect(0, 0, width, height), output, stride, 0);
            wb.Freeze();

            return wb;
        }

        private static BitmapSource EnsureBgra32(BitmapSource source)
        {
            if (source.Format == PixelFormats.Bgra32)
                return source;

            FormatConvertedBitmap converted = new FormatConvertedBitmap();
            converted.BeginInit();
            converted.Source = source;
            converted.DestinationFormat = PixelFormats.Bgra32;
            converted.EndInit();
            converted.Freeze();

            return converted;
        }

        private static BitmapSource TrimTransparentBitmap(BitmapSource source)
        {
            BitmapSource formatted = EnsureBgra32(source);

            int width = formatted.PixelWidth;
            int height = formatted.PixelHeight;
            int stride = width * 4;

            byte[] pixels = new byte[stride * height];

            formatted.CopyPixels(pixels, stride, 0);

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                int row = y * stride;

                for (int x = 0; x < width; x++)
                {
                    int index = row + x * 4;
                    byte a = pixels[index + 3];

                    if (a > 20)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < minX || maxY < minY)
                return source;

            int padding = 18;

            minX = Math.Max(0, minX - padding);
            minY = Math.Max(0, minY - padding);
            maxX = Math.Min(width - 1, maxX + padding);
            maxY = Math.Min(height - 1, maxY + padding);

            int cropWidth = Math.Max(1, maxX - minX + 1);
            int cropHeight = Math.Max(1, maxY - minY + 1);

            CroppedBitmap cropped = new CroppedBitmap(
                formatted,
                new Int32Rect(minX, minY, cropWidth, cropHeight));

            cropped.Freeze();

            return cropped;
        }

        private void AddTemplateGlyphToFont(string symbol, BitmapSource glyphImage)
        {
            if (_font == null)
                return;

            if (_font.Glyphs == null)
                _font.Glyphs = new List<GlyphEntry>();

            GlyphEntry? glyph = _font.Glyphs.FirstOrDefault(x => x.Char == symbol);

            if (glyph == null)
            {
                glyph = new GlyphEntry
                {
                    Char = symbol,
                    UpdatedAt = DateTime.Now,
                    Variants = new List<GlyphVariant>()
                };

                _font.Glyphs.Add(glyph);
            }

            if (glyph.Variants == null)
                glyph.Variants = new List<GlyphVariant>();

            foreach (GlyphVariant existing in glyph.Variants)
            {
                existing.IsDefault = false;
            }

            var variant = new GlyphVariant
            {
                Id = Guid.NewGuid(),
                IsDefault = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            string path = FontStorage.BuildVariantFilePath(_font.Id, symbol, variant.Id);
            SaveBitmapAsPng(glyphImage, path);

            variant.ImagePath = path;

            glyph.Variants.Add(variant);
            glyph.UpdatedAt = DateTime.Now;

            FontStorage.NormalizeDefaults(_font, symbol);
        }

        private static void SaveBitmapAsPng(BitmapSource source, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "");

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));

            using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            encoder.Save(fs);
        }

        private string GetLastTemplateSymbolsPath()
        {
            string folder = FontStorage.GetGlyphsFolder(_fontId);
            Directory.CreateDirectory(folder);

            return Path.Combine(folder, "_last_template_symbols.txt");
        }

        private void SaveLastTemplateSymbols(List<string> symbols)
        {
            try
            {
                string path = GetLastTemplateSymbolsPath();
                File.WriteAllLines(path, symbols.Select(ToBase64), Encoding.UTF8);
            }
            catch
            {
            }
        }

        private List<string> LoadLastTemplateSymbols()
        {
            try
            {
                string path = GetLastTemplateSymbolsPath();

                if (!File.Exists(path))
                    return new List<string>();

                return File.ReadAllLines(path, Encoding.UTF8)
                    .Select(FromBase64)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static void SaveSidecarSymbols(string pdfPath, List<string> symbols)
        {
            try
            {
                string sidecar = pdfPath + ".symbols.txt";
                File.WriteAllLines(sidecar, symbols.Select(ToBase64), Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static List<string> LoadSidecarSymbols(string pdfPath)
        {
            try
            {
                string sidecar = pdfPath + ".symbols.txt";

                if (!File.Exists(sidecar))
                    return new List<string>();

                return File.ReadAllLines(sidecar, Encoding.UTF8)
                    .Select(FromBase64)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static string ToBase64(string text)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text ?? ""));
        }

        private static string FromBase64(string text)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(text));
            }
            catch
            {
                return "";
            }
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
                    $"Экспортировано символов: {result.ExportedGlyphCount}\n" +
                    $"Из редактора линий: {result.ExportedFromIsfCount}\n" +
                    $"Из PNG/PDF-шаблонов: {result.ExportedFromPngCount}";

                if (result.SkippedGlyphCount > 0)
                {
                    message +=
                        "\n\nПропущено символов: " + result.SkippedGlyphCount;

                    if (result.SkippedChars.Count > 0)
                        message += "\nСимволы: " + string.Join(", ", result.SkippedChars.Select(x => $"«{x}»"));
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
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false
            };

            try
            {
                Hide();
                win.ShowDialog();
            }
            finally
            {
                LoadFont();
                SetPreviewInputTextFromCreatedGlyphsIfNeeded();
                RefreshGlyphTiles();
                RebuildPreview();

                Show();
                WindowState = WindowState.Normal;
                Activate();
            }
        }

        private struct PointInt
        {
            public int X { get; }
            public int Y { get; }

            public PointInt(int x, int y)
            {
                X = x;
                Y = y;
            }
        }
    }

    public class GlyphTileVm : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Char { get; set; } = "";

        public string DefaultImagePath { get; set; } = "";

        public bool IsDefault { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}