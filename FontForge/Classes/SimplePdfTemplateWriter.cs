using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FontForge.Classes
{
    public static class SimplePdfTemplateWriter
    {
        private const double PdfPageWidth = 595.0;
        private const double PdfPageHeight = 842.0;

        private const int PagePixelWidth = 2480;
        private const int PagePixelHeight = 3508;

        private const double Margin = 120.0;
        private const double HeaderTop = 105.0;
        private const double GridTop = 430.0;
        private const double GridBottom = 155.0;

        private const int Columns = 5;
        private const int Rows = 7;
        private const int ItemsPerPage = Columns * Rows;

        private const char SymbolSeparator = '\u001F';

        public static void SaveTemplatePdf(string filePath, string fontName, List<string> symbols)
        {
            if (symbols == null || symbols.Count == 0)
                throw new InvalidOperationException("Нет символов для создания шаблона.");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? "");

            symbols = symbols
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            if (symbols.Count == 0)
                throw new InvalidOperationException("Нет символов для создания шаблона.");

            int pageCount = (int)Math.Ceiling(symbols.Count / (double)ItemsPerPage);

            var pages = new List<byte[]>();

            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                BitmapSource pageBitmap = RenderTemplatePage(fontName, symbols, pageIndex, pageCount);
                byte[] jpegBytes = EncodeBitmapToJpeg(pageBitmap);

                pages.Add(jpegBytes);
            }

            WriteImagePdf(filePath, pages, symbols, fontName);
        }

        public static List<string> TryReadTemplateSymbols(string pdfPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
                    return new List<string>();

                byte[] bytes = File.ReadAllBytes(pdfPath);
                string text = Encoding.Latin1.GetString(bytes);

                List<string> fromComment = TryReadSymbolsFromComment(text);

                if (fromComment.Count > 0)
                    return fromComment;

                List<string> fromInfo = TryReadSymbolsFromInfoDictionary(text);

                if (fromInfo.Count > 0)
                    return fromInfo;

                return new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static List<string> TryReadSymbolsFromComment(string text)
        {
            string marker = "% FontForgeSymbols:";

            int start = text.IndexOf(marker, StringComparison.Ordinal);

            if (start < 0)
                return new List<string>();

            start += marker.Length;

            int end = text.IndexOf('\n', start);

            if (end < 0)
                end = text.Length;

            string base64 = text.Substring(start, end - start).Trim();

            return DecodeSymbolsBase64(base64);
        }

        private static List<string> TryReadSymbolsFromInfoDictionary(string text)
        {
            string marker = "/FontForgeSymbols";

            int markerIndex = text.IndexOf(marker, StringComparison.Ordinal);

            if (markerIndex < 0)
                return new List<string>();

            int index = markerIndex + marker.Length;

            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;

            if (index >= text.Length || text[index] != '(')
                return new List<string>();

            index++;

            string base64 = ReadPdfLiteralString(text, index);

            return DecodeSymbolsBase64(base64);
        }

        private static string ReadPdfLiteralString(string text, int start)
        {
            var builder = new StringBuilder();

            bool escaped = false;

            for (int i = start; i < text.Length; i++)
            {
                char ch = text[i];

                if (escaped)
                {
                    builder.Append(ch);
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == ')')
                    break;

                builder.Append(ch);
            }

            return builder.ToString();
        }

        private static List<string> DecodeSymbolsBase64(string base64)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(base64))
                    return new List<string>();

                byte[] bytes = Convert.FromBase64String(base64.Trim());
                string raw = Encoding.UTF8.GetString(bytes);

                return raw
                    .Split(SymbolSeparator)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static BitmapSource RenderTemplatePage(
            string fontName,
            List<string> symbols,
            int pageIndex,
            int pageCount)
        {
            var visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                DrawPageBackground(dc);
                DrawHeader(dc, fontName, pageIndex, pageCount);
                DrawCells(dc, symbols, pageIndex);
                DrawBottomHint(dc);
            }

            var bitmap = new RenderTargetBitmap(
                PagePixelWidth,
                PagePixelHeight,
                96,
                96,
                PixelFormats.Pbgra32);

            bitmap.Render(visual);
            bitmap.Freeze();

            return bitmap;
        }

        private static void DrawPageBackground(DrawingContext dc)
        {
            dc.DrawRectangle(
                Brushes.White,
                null,
                new Rect(0, 0, PagePixelWidth, PagePixelHeight));

            dc.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(248, 249, 251)),
                null,
                new Rect(65, 65, PagePixelWidth - 130, PagePixelHeight - 130));
        }

        private static void DrawHeader(
            DrawingContext dc,
            string fontName,
            int pageIndex,
            int pageCount)
        {
            string safeFontName = string.IsNullOrWhiteSpace(fontName)
                ? "Без названия"
                : fontName;

            DrawText(
                dc,
                "Кузница шрифтов — шаблон",
                Margin,
                HeaderTop,
                76,
                Brushes.Black,
                FontWeights.SemiBold);

            DrawText(
                dc,
                $"Шрифт: {safeFontName}",
                Margin,
                HeaderTop + 88,
                34,
                new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                FontWeights.Normal);

            DrawText(
                dc,
                "Заполняйте символы чёрной ручкой внутри клеток. Не выходите за рамки области письма.",
                Margin,
                HeaderTop + 142,
                30,
                new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                FontWeights.Normal);

            DrawText(
                dc,
                "После заполнения отсканируйте лист обратно в PDF и загрузите его в программу.",
                Margin,
                HeaderTop + 186,
                28,
                new SolidColorBrush(Color.FromRgb(95, 95, 95)),
                FontWeights.Normal);

            DrawText(
                dc,
                $"Страница {pageIndex + 1} из {pageCount}",
                Margin,
                HeaderTop + 250,
                42,
                new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                FontWeights.SemiBold);
        }

        private static void DrawCells(DrawingContext dc, List<string> symbols, int pageIndex)
        {
            double cellGap = 26.0;

            double gridHeight = PagePixelHeight - GridTop - GridBottom;

            double cellWidth =
                (PagePixelWidth - Margin * 2 - cellGap * (Columns - 1)) / Columns;

            double cellHeight =
                (gridHeight - cellGap * (Rows - 1)) / Rows;

            int start = pageIndex * ItemsPerPage;

            for (int i = 0; i < ItemsPerPage; i++)
            {
                int symbolIndex = start + i;

                if (symbolIndex >= symbols.Count)
                    break;

                string symbol = symbols[symbolIndex];

                int col = i % Columns;
                int row = i / Columns;

                double x = Margin + col * (cellWidth + cellGap);
                double y = GridTop + row * (cellHeight + cellGap);

                DrawCell(dc, x, y, cellWidth, cellHeight, symbol);
            }
        }

        private static void DrawCell(
            DrawingContext dc,
            double x,
            double y,
            double width,
            double height,
            string symbol)
        {
            var outerRect = new Rect(x, y, width, height);

            Pen outerPen = new Pen(
                new SolidColorBrush(Color.FromRgb(65, 65, 65)),
                4);

            dc.DrawRoundedRectangle(
                Brushes.White,
                outerPen,
                outerRect,
                26,
                26);

            DrawCornerMarks(dc, x, y, width, height);

            DrawText(
                dc,
                symbol,
                x + 32,
                y + 24,
                70,
                Brushes.Black,
                FontWeights.SemiBold);

            double areaX = x + 34;
            double areaY = y + 118;
            double areaWidth = width - 68;
            double areaHeight = height - 154;

            var writingArea = new Rect(areaX, areaY, areaWidth, areaHeight);

            dc.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(252, 252, 252)),
                new Pen(new SolidColorBrush(Color.FromRgb(205, 205, 205)), 2),
                writingArea);

            DrawText(
                dc,
                "пишите здесь",
                areaX + 18,
                areaY + 26,
                26,
                new SolidColorBrush(Color.FromRgb(168, 168, 168)),
                FontWeights.Normal);

            Pen dashPen = new Pen(
                new SolidColorBrush(Color.FromRgb(178, 178, 178)),
                2);

            dashPen.DashStyle = new DashStyle(new double[] { 12, 12 }, 0);

            double guide1 = areaY + areaHeight * 0.25;
            double guide2 = areaY + areaHeight * 0.52;
            double guide3 = areaY + areaHeight * 0.80;

            dc.DrawLine(dashPen, new Point(areaX, guide1), new Point(areaX + areaWidth, guide1));
            dc.DrawLine(dashPen, new Point(areaX, guide2), new Point(areaX + areaWidth, guide2));
            dc.DrawLine(dashPen, new Point(areaX, guide3), new Point(areaX + areaWidth, guide3));
        }

        private static void DrawCornerMarks(
            DrawingContext dc,
            double x,
            double y,
            double width,
            double height)
        {
            Pen pen = new Pen(Brushes.Black, 4);

            double offset = 15;
            double len = 34;

            dc.DrawLine(pen, new Point(x + offset, y + offset), new Point(x + offset + len, y + offset));
            dc.DrawLine(pen, new Point(x + offset, y + offset), new Point(x + offset, y + offset + len));

            dc.DrawLine(pen, new Point(x + width - offset, y + offset), new Point(x + width - offset - len, y + offset));
            dc.DrawLine(pen, new Point(x + width - offset, y + offset), new Point(x + width - offset, y + offset + len));

            dc.DrawLine(pen, new Point(x + offset, y + height - offset), new Point(x + offset + len, y + height - offset));
            dc.DrawLine(pen, new Point(x + offset, y + height - offset), new Point(x + offset, y + height - offset - len));

            dc.DrawLine(pen, new Point(x + width - offset, y + height - offset), new Point(x + width - offset - len, y + height - offset));
            dc.DrawLine(pen, new Point(x + width - offset, y + height - offset), new Point(x + width - offset, y + height - offset - len));
        }

        private static void DrawBottomHint(DrawingContext dc)
        {
            DrawText(
                dc,
                "Совет: пишите крупно, чёрной ручкой, не касайтесь рамок клетки. Для лучшего результата сканируйте лист ровно.",
                Margin,
                PagePixelHeight - 105,
                25,
                new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                FontWeights.Normal);
        }

        private static void DrawText(
            DrawingContext dc,
            string text,
            double x,
            double y,
            double size,
            Brush brush,
            FontWeight weight)
        {
            var formattedText = new FormattedText(
                text ?? "",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    new FontFamily("Segoe UI"),
                    FontStyles.Normal,
                    weight,
                    FontStretches.Normal),
                size,
                brush,
                1.0);

            formattedText.Trimming = TextTrimming.CharacterEllipsis;
            formattedText.MaxTextWidth = PagePixelWidth - x - Margin;

            dc.DrawText(formattedText, new Point(x, y));
        }

        private static byte[] EncodeBitmapToJpeg(BitmapSource bitmap)
        {
            BitmapSource converted = ConvertToBgr24(bitmap);

            var encoder = new JpegBitmapEncoder
            {
                QualityLevel = 95
            };

            encoder.Frames.Add(BitmapFrame.Create(converted));

            using var ms = new MemoryStream();
            encoder.Save(ms);

            return ms.ToArray();
        }

        private static BitmapSource ConvertToBgr24(BitmapSource source)
        {
            if (source.Format == PixelFormats.Bgr24)
                return source;

            var converted = new FormatConvertedBitmap();

            converted.BeginInit();
            converted.Source = source;
            converted.DestinationFormat = PixelFormats.Bgr24;
            converted.EndInit();
            converted.Freeze();

            return converted;
        }

        private static void WriteImagePdf(
            string filePath,
            List<byte[]> pageImages,
            List<string> symbols,
            string fontName)
        {
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);

            string symbolsRaw = string.Join(SymbolSeparator, symbols);
            string symbolsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(symbolsRaw));
            string fontNameBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(fontName ?? ""));

            WriteAscii(fs, "%PDF-1.4\n");
            WriteAscii(fs, "% FontForge image template PDF\n");

            // Главная защита от ошибки А, Б, В:
            // список выбранных символов хранится прямо внутри PDF простым комментарием.
            WriteAscii(fs, "% FontForgeSymbols: " + symbolsBase64 + "\n");
            WriteAscii(fs, "% FontForgeFontName: " + fontNameBase64 + "\n");

            var offsets = new List<long>();

            int objectCount = 3 + pageImages.Count * 3;

            int catalogObject = 1;
            int pagesObject = 2;
            int infoObject = 3;

            WriteObject(fs, offsets, catalogObject,
                $"<< /Type /Catalog /Pages {pagesObject} 0 R >>");

            var pageObjectNumbers = new List<int>();

            for (int i = 0; i < pageImages.Count; i++)
            {
                int pageObject = 4 + i * 3;
                int contentObject = pageObject + 1;
                int imageObject = pageObject + 2;

                pageObjectNumbers.Add(pageObject);

                string page =
                    "<< /Type /Page " +
                    $"/Parent {pagesObject} 0 R " +
                    $"/MediaBox [0 0 {F(PdfPageWidth)} {F(PdfPageHeight)}] " +
                    $"/Resources << /XObject << /Im{i + 1} {imageObject} 0 R >> >> " +
                    $"/Contents {contentObject} 0 R " +
                    ">>";

                WriteObject(fs, offsets, pageObject, page);

                string content =
                    "q\n" +
                    $"{F(PdfPageWidth)} 0 0 {F(PdfPageHeight)} 0 0 cm\n" +
                    $"/Im{i + 1} Do\n" +
                    "Q\n";

                WriteStreamObject(fs, offsets, contentObject, Encoding.ASCII.GetBytes(content), "");

                byte[] imageBytes = pageImages[i];

                string imageDictionary =
                    $"/Type /XObject /Subtype /Image " +
                    $"/Width {PagePixelWidth} " +
                    $"/Height {PagePixelHeight} " +
                    "/ColorSpace /DeviceRGB " +
                    "/BitsPerComponent 8 " +
                    "/Filter /DCTDecode ";

                WriteStreamObject(fs, offsets, imageObject, imageBytes, imageDictionary);
            }

            string kids = string.Join(" ", pageObjectNumbers.ConvertAll(x => $"{x} 0 R"));

            WriteObject(fs, offsets, pagesObject,
                $"<< /Type /Pages /Kids [{kids}] /Count {pageImages.Count} >>");

            WriteObject(fs, offsets, infoObject,
                $"<< /Producer (FontForge) /FontForgeSymbols ({symbolsBase64}) /FontForgeFontName ({fontNameBase64}) >>");

            long xrefOffset = fs.Position;

            WriteAscii(fs, "xref\n");
            WriteAscii(fs, $"0 {objectCount + 1}\n");
            WriteAscii(fs, "0000000000 65535 f \n");

            for (int i = 1; i <= objectCount; i++)
            {
                long offset = offsets[i - 1];
                WriteAscii(fs, $"{offset:0000000000} 00000 n \n");
            }

            WriteAscii(fs, "trailer\n");
            WriteAscii(fs, $"<< /Size {objectCount + 1} /Root 1 0 R /Info {infoObject} 0 R >>\n");
            WriteAscii(fs, "startxref\n");
            WriteAscii(fs, $"{xrefOffset}\n");
            WriteAscii(fs, "%%EOF");
        }

        private static void WriteObject(
            FileStream fs,
            List<long> offsets,
            int objectNumber,
            string content)
        {
            EnsureOffsetSlot(offsets, objectNumber);

            offsets[objectNumber - 1] = fs.Position;

            WriteAscii(fs, $"{objectNumber} 0 obj\n");
            WriteAscii(fs, content);
            WriteAscii(fs, "\nendobj\n");
        }

        private static void WriteStreamObject(
            FileStream fs,
            List<long> offsets,
            int objectNumber,
            byte[] streamBytes,
            string extraDictionary)
        {
            EnsureOffsetSlot(offsets, objectNumber);

            offsets[objectNumber - 1] = fs.Position;

            WriteAscii(fs, $"{objectNumber} 0 obj\n");
            WriteAscii(fs, $"<< {extraDictionary}/Length {streamBytes.Length} >>\n");
            WriteAscii(fs, "stream\n");

            fs.Write(streamBytes, 0, streamBytes.Length);

            WriteAscii(fs, "\nendstream\n");
            WriteAscii(fs, "endobj\n");
        }

        private static void EnsureOffsetSlot(List<long> offsets, int objectNumber)
        {
            while (offsets.Count < objectNumber)
                offsets.Add(0);
        }

        private static void WriteAscii(FileStream fs, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            fs.Write(bytes, 0, bytes.Length);
        }

        private static string F(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}