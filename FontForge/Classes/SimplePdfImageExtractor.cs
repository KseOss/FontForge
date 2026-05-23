using PDFtoImage;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FontForge.Classes
{
    public static class SimplePdfImageExtractor
    {
        public static List<BitmapSource> ExtractPageImages(string pdfPath)
        {
            var pages = new List<BitmapSource>();

            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
                return pages;

            byte[] pdfBytes = File.ReadAllBytes(pdfPath);

            var options = new RenderOptions(
                Dpi: 300,
                WithAnnotations: true,
                WithFormFill: true,
                BackgroundColor: SKColors.White);

            foreach (SKBitmap skBitmap in Conversion.ToImages(pdfBytes, options: options))
            {
                using (skBitmap)
                {
                    BitmapSource page = ConvertSkBitmapToBitmapSource(skBitmap);
                    pages.Add(page);
                }
            }

            return pages;
        }

        // Оставляем этот метод, чтобы старый FontEditorWindow.xaml.cs не ругался.
        // Теперь аннотации уже попадают в картинку страницы через PDFtoImage.
        public static List<List<PdfInkStroke>> ExtractInkPages(
            string pdfPath,
            int templateWidth,
            int templateHeight)
        {
            return new List<List<PdfInkStroke>>();
        }

        private static BitmapSource ConvertSkBitmapToBitmapSource(SKBitmap bitmap)
        {
            using SKImage image = SKImage.FromBitmap(bitmap);
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);

            byte[] bytes = data.ToArray();

            using var ms = new MemoryStream(bytes);

            var result = new BitmapImage();

            result.BeginInit();
            result.CacheOption = BitmapCacheOption.OnLoad;
            result.StreamSource = ms;
            result.EndInit();
            result.Freeze();

            return result;
        }
    }

    public class PdfInkStroke
    {
        public List<Point> Points { get; }

        public PdfInkStroke(List<Point> points)
        {
            Points = points ?? new List<Point>();
        }
    }
}