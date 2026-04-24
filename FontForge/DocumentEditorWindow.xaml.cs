using FontForge.Classes;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FontForge
{
    public partial class DocumentEditorWindow : Window
    {
        private readonly Guid? _initialFontId;
        private List<CreatedFont> _fonts = new();
        private CreatedFont? _selectedFont;

        public DocumentEditorWindow() : this(null)
        {
        }

        public DocumentEditorWindow(Guid fontId) : this((Guid?)fontId)
        {
        }

        private DocumentEditorWindow(Guid? fontId)
        {
            InitializeComponent();
            _initialFontId = fontId;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            _fonts = FontStorage.LoadFonts()
                .Where(f => !f.IsDeleted)
                .OrderBy(f => f.Name)
                .ToList();

            FontsCombo.ItemsSource = _fonts;
            FontsCombo.DisplayMemberPath = "Name";

            if (_fonts.Count > 0)
            {
                if (_initialFontId.HasValue)
                {
                    CreatedFont? match = _fonts.FirstOrDefault(f => f.Id == _initialFontId.Value);
                    FontsCombo.SelectedItem = match ?? _fonts[0];
                }
                else
                {
                    FontsCombo.SelectedItem = _fonts[0];
                }
            }

            UpdateTypographyValueLabels();
            RefreshInputTextForSelectedFontIfNeeded();
            RebuildPreview();
        }

        private void FontsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontsCombo.SelectedItem is CreatedFont selected)
            {
                _selectedFont = _fonts.FirstOrDefault(f => f.Id == selected.Id) ?? selected;
                NormalizeFontDefaults(_selectedFont);
            }
            else
            {
                _selectedFont = null;
            }

            RefreshInputTextForSelectedFontIfNeeded();
            RebuildPreview();
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RebuildPreview();
        }

        private void TypographySettings_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateTypographyValueLabels();
            RebuildPreview();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private double PreviewSize => PreviewSizeSlider?.Value ?? 64;
        private double LetterSpacing => LetterSpacingSlider?.Value ?? 0;
        private double WordSpacing => WordSpacingSlider?.Value ?? 32;
        private double LineSpacing => LineSpacingSlider?.Value ?? 1.2;
        private double LeftIndent => LeftIndentSlider?.Value ?? 0;
        private double FirstLineIndent => FirstLineIndentSlider?.Value ?? 0;

        private void UpdateTypographyValueLabels()
        {
            if (PreviewSizeValueText != null)
                PreviewSizeValueText.Text = $"{PreviewSize:0} px";

            if (LetterSpacingValueText != null)
                LetterSpacingValueText.Text = $"{LetterSpacing:+0;-0;0} px";

            if (WordSpacingValueText != null)
                WordSpacingValueText.Text = $"{WordSpacing:0} px";

            if (LineSpacingValueText != null)
                LineSpacingValueText.Text = $"{LineSpacing:0.0}";

            if (LeftIndentValueText != null)
                LeftIndentValueText.Text = $"{LeftIndent:0} px";

            if (FirstLineIndentValueText != null)
                FirstLineIndentValueText.Text = $"{FirstLineIndent:0} px";
        }

        private void SavePdf_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFont == null)
            {
                MessageBox.Show(
                    "Сначала выберите шрифт.",
                    "PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            string text = InputBox.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show(
                    "Введите текст для сохранения в PDF.",
                    "PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = "document.pdf",
                DefaultExt = ".pdf",
                AddExtension = true
            };

            if (sfd.ShowDialog() != true)
                return;

            try
            {
                QuestPDF.Settings.License = LicenseType.Community;

                CreatedFont fontSnapshot = _selectedFont;

                float pdfGlyphSize = (float)Math.Clamp(PreviewSize * 0.70, 14, 72);
                float scale = pdfGlyphSize / (float)Math.Max(1, PreviewSize);

                float pdfLetterSpacing = (float)LetterSpacing * scale;
                float pdfWordSpacing = (float)WordSpacing * scale;
                float pdfLineSpacing = (float)LineSpacing;
                float pdfLeftIndent = (float)LeftIndent * scale;
                float pdfFirstLineIndent = (float)FirstLineIndent * scale;

                GeneratePdfAsRenderedPages(
                    sfd.FileName,
                    fontSnapshot,
                    text,
                    pdfGlyphSize,
                    pdfLetterSpacing,
                    pdfWordSpacing,
                    pdfLineSpacing,
                    pdfLeftIndent,
                    pdfFirstLineIndent);

                MessageBox.Show(
                    "PDF успешно сохранён.",
                    "PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Ошибка при сохранении PDF:\n" + ex.Message,
                    "PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // PDF теперь создаётся из временных PNG-страниц.
        // Так отрицательный межбуквенный интервал работает без ошибки QuestPDF.
        private void GeneratePdfAsRenderedPages(
            string pdfPath,
            CreatedFont font,
            string text,
            float glyphSize,
            float letterSpacing,
            float wordSpacing,
            float lineSpacing,
            float leftIndent,
            float firstLineIndent)
        {
            List<string> temporaryPageImages = RenderTextToTemporaryPageImages(
                font,
                text,
                glyphSize,
                letterSpacing,
                wordSpacing,
                lineSpacing,
                leftIndent,
                firstLineIndent);

            try
            {
                var doc = Document.Create(container =>
                {
                    foreach (string pageImage in temporaryPageImages)
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(40);
                            page.PageColor(QuestPDF.Helpers.Colors.White);

                            page.Content()
                                .AlignTop()
                                .AlignLeft()
                                .Image(pageImage)
                                .FitWidth();
                        });
                    }
                });

                doc.GeneratePdf(pdfPath);
            }
            finally
            {
                foreach (string path in temporaryPageImages)
                {
                    try
                    {
                        if (File.Exists(path))
                            File.Delete(path);
                    }
                    catch
                    {
                        // Не мешаем пользователю, если временный файл не удалился.
                    }
                }

                try
                {
                    string? dir = temporaryPageImages
                        .Select(Path.GetDirectoryName)
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                        Directory.Delete(dir, true);
                }
                catch
                {
                    // Не критично.
                }
            }
        }

        private List<string> RenderTextToTemporaryPageImages(
            CreatedFont font,
            string text,
            float glyphSizePt,
            float letterSpacingPt,
            float wordSpacingPt,
            float lineSpacing,
            float leftIndentPt,
            float firstLineIndentPt)
        {
            const double a4WidthPt = 595.0;
            const double a4HeightPt = 842.0;
            const double marginPt = 40.0;

            double contentWidthPt = a4WidthPt - marginPt * 2.0;
            double contentHeightPt = a4HeightPt - marginPt * 2.0;

            // Чем больше renderScale, тем чётче PDF.
            // 2.0 — хороший баланс качества и размера файла.
            const double renderScale = 2.0;

            double pageWidth = contentWidthPt * renderScale;
            double pageHeight = contentHeightPt * renderScale;

            double glyphSize = Math.Max(8, glyphSizePt * renderScale);
            double letterSpacing = letterSpacingPt * renderScale;
            double wordSpacing = Math.Max(2, wordSpacingPt * renderScale);
            double leftIndent = Math.Max(0, leftIndentPt * renderScale);
            double firstLineIndent = Math.Max(0, firstLineIndentPt * renderScale);

            double lineHeight = glyphSize * Math.Max(0.6, lineSpacing);
            double minAdvance = glyphSize * 0.20;

            string tempDir = Path.Combine(
                Path.GetTempPath(),
                "FontForgePdf_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(tempDir);

            var result = new List<string>();
            var imageCache = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

            DrawingVisual? currentVisual = null;
            DrawingContext? dc = null;

            double x = 0;
            double y = 0;

            void StartPage()
            {
                currentVisual = new DrawingVisual();
                dc = currentVisual.RenderOpen();

                dc.DrawRectangle(
                    Brushes.White,
                    null,
                    new Rect(0, 0, pageWidth, pageHeight));

                x = 0;
                y = 0;
            }

            void FinishPage()
            {
                if (currentVisual == null || dc == null)
                    return;

                dc.Close();

                int pixelWidth = Math.Max(1, (int)Math.Ceiling(pageWidth));
                int pixelHeight = Math.Max(1, (int)Math.Ceiling(pageHeight));

                var rtb = new RenderTargetBitmap(
                    pixelWidth,
                    pixelHeight,
                    96,
                    96,
                    PixelFormats.Pbgra32);

                rtb.Render(currentVisual);
                rtb.Freeze();

                string pagePath = Path.Combine(tempDir, $"page_{result.Count + 1:000}.png");

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using (var fs = new FileStream(pagePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    encoder.Save(fs);
                }

                result.Add(pagePath);

                currentVisual = null;
                dc = null;
            }

            void EnsurePage()
            {
                if (dc == null)
                    StartPage();
            }

            void NewPage()
            {
                FinishPage();
                StartPage();
            }

            void NewLine(double indent)
            {
                y += lineHeight;
                x = indent;

                if (y + glyphSize > pageHeight)
                    NewPage();
            }

            void DrawFallbackSymbol(string symbol, double drawX, double drawY)
            {
                if (dc == null)
                    return;

                var formattedText = new FormattedText(
                    symbol,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    glyphSize * 0.72,
                    Brushes.Black,
                    1.0);

                double textY = drawY + Math.Max(0, (glyphSize - formattedText.Height) / 2.0);

                dc.DrawText(formattedText, new Point(drawX, textY));
            }

            BitmapSource? GetBitmap(string path)
            {
                try
                {
                    if (imageCache.TryGetValue(path, out BitmapSource? cached))
                        return cached;

                    BitmapSource bmp = LoadBitmapForPreview(path);
                    imageCache[path] = bmp;
                    return bmp;
                }
                catch
                {
                    return null;
                }
            }

            void DrawSymbol(string symbol)
            {
                EnsurePage();

                if (dc == null)
                    return;

                if (x + glyphSize > pageWidth && x > 0)
                    NewLine(leftIndent);

                if (y + glyphSize > pageHeight)
                    NewPage();

                string? imagePath = GlyphImageResolver.FindGlyphImagePath(font, symbol);
                BitmapSource? bmp = null;

                if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                    bmp = GetBitmap(imagePath);

                if (bmp != null)
                {
                    dc.DrawImage(
                        bmp,
                        new Rect(x, y, glyphSize, glyphSize));
                }
                else
                {
                    DrawFallbackSymbol(symbol, x, y);
                }

                // ВАЖНО:
                // отрицательный межбуквенный интервал работает здесь:
                // картинка рисуется полного размера, но следующий символ начинается ближе.
                double advance = Math.Max(minAdvance, glyphSize + letterSpacing);
                x += advance;
            }

            EnsurePage();

            string[] paragraphs = SplitLines(text);

            foreach (string paragraph in paragraphs)
            {
                double paragraphIndent = leftIndent + firstLineIndent;
                x = paragraphIndent;

                if (paragraph.Length == 0)
                {
                    y += lineHeight;

                    if (y + glyphSize > pageHeight)
                        NewPage();

                    continue;
                }

                foreach (char ch in paragraph)
                {
                    if (ch == ' ')
                    {
                        EnsurePage();

                        if (x + wordSpacing > pageWidth && x > 0)
                            NewLine(leftIndent);

                        x += wordSpacing;
                        continue;
                    }

                    if (ch == '\t')
                    {
                        EnsurePage();

                        double tabWidth = wordSpacing * 3;

                        if (x + tabWidth > pageWidth && x > 0)
                            NewLine(leftIndent);

                        x += tabWidth;
                        continue;
                    }

                    DrawSymbol(ch.ToString());
                }

                y += lineHeight;

                if (y + glyphSize > pageHeight)
                    NewPage();
            }

            FinishPage();

            if (result.Count == 0)
            {
                StartPage();
                FinishPage();
            }

            return result;
        }

        private void RebuildPreview()
        {
            if (PreviewRoot == null || InputBox == null)
                return;

            PreviewRoot.Children.Clear();

            if (_selectedFont == null)
                return;

            string text = InputBox.Text ?? string.Empty;
            double size = PreviewSize;

            string[] paragraphs = SplitLines(text);

            foreach (string paragraph in paragraphs)
            {
                var wrap = new WrapPanel
                {
                    // Каждый Enter — новый абзац.
                    // Поэтому отступ первой строки применяется к каждой новой строке.
                    Margin = new Thickness(
                        LeftIndent + FirstLineIndent,
                        0,
                        0,
                        size * Math.Max(0.05, LineSpacing - 0.85)),
                    MinHeight = size * LineSpacing
                };

                if (paragraph.Length == 0)
                {
                    PreviewRoot.Children.Add(new Border
                    {
                        Height = size * LineSpacing
                    });

                    continue;
                }

                foreach (char ch in paragraph)
                {
                    if (ch == ' ')
                    {
                        wrap.Children.Add(new Border
                        {
                            Width = WordSpacing,
                            Height = size
                        });

                        continue;
                    }

                    if (ch == '\t')
                    {
                        wrap.Children.Add(new Border
                        {
                            Width = WordSpacing * 3,
                            Height = size
                        });

                        continue;
                    }

                    string symbol = ch.ToString();
                    string? imagePath = GlyphImageResolver.FindGlyphImagePath(_selectedFont, symbol);

                    if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                    {
                        wrap.Children.Add(CreatePngGlyphPreviewOrFallback(symbol, imagePath, size, LetterSpacing));
                    }
                    else
                    {
                        wrap.Children.Add(CreateFallbackText(symbol, size, LetterSpacing));
                    }
                }

                PreviewRoot.Children.Add(wrap);
            }
        }

        private static FrameworkElement CreatePngGlyphPreviewOrFallback(
            string symbol,
            string imagePath,
            double size,
            double letterSpacing)
        {
            try
            {
                var border = new Border
                {
                    Width = size,
                    Height = size,
                    Margin = new Thickness(letterSpacing / 2.0, 0, letterSpacing / 2.0, 0),
                    Background = Brushes.Transparent,
                    ToolTip = $"Символ: {symbol}\nPNG:\n{imagePath}"
                };

                var image = new System.Windows.Controls.Image
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
                return CreateFallbackText(symbol, size, letterSpacing);
            }
        }

        private static TextBlock CreateFallbackText(string symbol, double size, double letterSpacing)
        {
            return new TextBlock
            {
                Text = symbol,
                FontSize = size * 0.72,
                Foreground = (Brush)Application.Current.Resources["PreviewTextBrush"],
                Margin = new Thickness(letterSpacing / 2.0, 0, letterSpacing / 2.0, 0),
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

        private void RefreshInputTextForSelectedFontIfNeeded()
        {
            if (_selectedFont == null || InputBox == null)
                return;

            string current = InputBox.Text ?? string.Empty;

            bool shouldReplace =
                string.IsNullOrWhiteSpace(current)
                || current.Contains("Пример:", StringComparison.Ordinal)
                || current.Contains("Введите свой текст", StringComparison.Ordinal)
                || current.StartsWith("Символы вашего шрифта:", StringComparison.Ordinal);

            if (!shouldReplace)
                return;

            string sample = BuildSampleText(_selectedFont);

            if (!string.IsNullOrWhiteSpace(sample))
                InputBox.Text = sample;
        }

        private static string BuildSampleText(CreatedFont font)
        {
            var chars = GlyphImageResolver.GetDrawableChars(font);

            if (chars.Count == 0)
                return "";

            return string.Join(" ", chars);
        }

        private static void NormalizeFontDefaults(CreatedFont font)
        {
            if (font.Glyphs == null)
                return;

            foreach (var glyph in font.Glyphs)
                FontStorage.NormalizeDefaults(font, glyph.Char);
        }

        private static string[] SplitLines(string text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');
        }
    }
}