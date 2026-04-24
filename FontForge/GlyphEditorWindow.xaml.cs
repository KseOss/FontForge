using FontForge.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FontForge
{
    public partial class GlyphEditorWindow : Window
    {
        private readonly Guid _fontId;
        private readonly string _ch;
        private readonly Guid _variantId;

        private readonly Stack<StrokeCollection> _undo = new();
        private bool _uiReady = false;

        public string? SavedImagePath { get; private set; }

        public GlyphEditorWindow(Guid fontId, string ch, Guid variantId)
        {
            InitializeComponent();

            _fontId = fontId;
            _ch = ch;
            _variantId = variantId;

            HeaderText.Text = $"Редактирование символа “{_ch}”";
            GuideLetterText.Text = _ch;
        }

        // =========================
        // INIT
        // =========================
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _uiReady = true;

            DrawGuides();
            ApplyGuideVisibility();
            ApplyBrush();

            ModeDrawRadio.IsChecked = true;
            ApplyMode();

            LoadExistingStrokesIfAny();

            PushUndoSnapshot();

            // подправим заливку красивого слайдера (его Fill)
            UpdatePrettySliderFill();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_uiReady) return;
            DrawGuides();
        }

        // =========================
        // LOAD existing (ISF)
        // =========================
        private void LoadExistingStrokesIfAny()
        {
            try
            {
                string pngPath = FontStorage.BuildVariantFilePath(_fontId, _ch, _variantId);
                string isfPath = System.IO.Path.ChangeExtension(pngPath, ".isf");

                if (File.Exists(isfPath))
                {
                    using var fs = new FileStream(isfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    Ink.Strokes = new StrokeCollection(fs);
                }
            }
            catch
            {
                // не падаем — просто откроется пусто
            }
        }

        // =========================
        // FORCE MODE on input
        // =========================
        private void Ink_PreviewMouseDown_ForceMode(object sender, MouseButtonEventArgs e)
        {
            if (!_uiReady) return;
            if (ModeDrawRadio.IsChecked == true)
                ApplyMode();
        }

        private void Ink_PreviewStylusDown_ForceMode(object sender, StylusDownEventArgs e)
        {
            if (!_uiReady) return;
            if (ModeDrawRadio.IsChecked == true)
                ApplyMode();
        }

        // =========================
        // MODE
        // =========================
        private void ModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;
            ApplyMode();
        }

        private void ApplyMode()
        {
            bool edit = ModeEditRadio.IsChecked == true;

            if (edit)
            {
                Ink.EditingMode = InkCanvasEditingMode.Select;
                Ink.EditingModeInverted = InkCanvasEditingMode.EraseByStroke;
                Ink.Cursor = Cursors.Arrow;
                DeleteSelectedButton.Visibility = Visibility.Visible;
            }
            else
            {
                Ink.Select(new StrokeCollection());
                Ink.EditingMode = InkCanvasEditingMode.Ink;
                Ink.EditingModeInverted = InkCanvasEditingMode.EraseByStroke;
                Ink.Cursor = Cursors.Pen;
                DeleteSelectedButton.Visibility = Visibility.Collapsed;
            }
        }

        // =========================
        // GUIDE VISIBILITY
        // =========================
        private void ShowGuideCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;
            ApplyGuideVisibility();
        }

        private void ApplyGuideVisibility()
        {
            GuideViewbox.Visibility = ShowGuideCheck.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // =========================
        // GUIDES
        // =========================
        private void DrawGuides()
        {
            GuideLinesCanvas.Children.Clear();

            double w = Math.Max(1, WorkGrid.ActualWidth);
            double h = Math.Max(1, WorkGrid.ActualHeight);

            var border = new Rectangle
            {
                Width = Math.Max(1, w - 2),
                Height = Math.Max(1, h - 2),
                Stroke = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 3 },
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(border, 1);
            Canvas.SetTop(border, 1);
            GuideLinesCanvas.Children.Add(border);

            double[] ys = { h * 0.18, h * 0.38, h * 0.62, h * 0.82 };
            foreach (double y in ys)
            {
                var line = new Line
                {
                    X1 = 0,
                    X2 = w,
                    Y1 = y,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 4 }
                };
                GuideLinesCanvas.Children.Add(line);
            }
        }

        // =========================
        // BRUSH (only size)
        // =========================
        private void BrushSettings_Changed(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;
            ApplyBrush();
            UpdatePrettySliderFill();
        }

        private void ApplyBrush()
        {
            double final = Math.Max(1, BrushSizeSlider.Value);

            Ink.DefaultDrawingAttributes = new DrawingAttributes
            {
                Color = Colors.Black,
                Width = final,
                Height = final,
                FitToCurve = true,
                IgnorePressure = true
            };
        }

        // попытка красиво подкрашивать Fill-трек внутри шаблона
        private void UpdatePrettySliderFill()
        {
            // Шаблонный Border TrackFill мы не ищем через визуальное дерево (дорого),
            // но можно обновлять ширину через ValueChanged только если шаблон применён.
            if (BrushSizeSlider.Template == null) return;

            var track = BrushSizeSlider.Template.FindName("PART_Track", BrushSizeSlider) as Track;
            var fill = BrushSizeSlider.Template.FindName("TrackFill", BrushSizeSlider) as Border;

            if (track == null || fill == null) return;

            double range = Math.Max(1, BrushSizeSlider.Maximum - BrushSizeSlider.Minimum);
            double k = (BrushSizeSlider.Value - BrushSizeSlider.Minimum) / range;

            // ширина заполнения = ширина трека * k
            double trackWidth = Math.Max(0, track.ActualWidth);
            fill.Width = trackWidth * k;
        }

        // =========================
        // UNDO
        // =========================
        private void Ink_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            PushUndoSnapshot();
        }

        private void PushUndoSnapshot()
        {
            _undo.Push(Ink.Strokes.Clone());

            if (_undo.Count > 60)
            {
                var arr = _undo.Reverse().Take(60).Reverse().ToArray();
                _undo.Clear();
                foreach (var s in arr) _undo.Push(s);
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_undo.Count <= 1) return;

            _undo.Pop();
            Ink.Strokes = _undo.Peek().Clone();
            Ink.Select(new StrokeCollection());
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            Ink.Strokes.Clear();
            Ink.Select(new StrokeCollection());
            PushUndoSnapshot();
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = Ink.GetSelectedStrokes();
            if (selected == null || selected.Count == 0) return;

            foreach (var s in selected.ToList())
                Ink.Strokes.Remove(s);

            Ink.Select(new StrokeCollection());
            PushUndoSnapshot();
        }

        private void Ink_SelectionChanged(object sender, EventArgs e)
        {
            if (!_uiReady) return;

            // чтобы в режиме рисования не было ощущения "лассо"
            if (ModeDrawRadio.IsChecked == true)
                Ink.Select(new StrokeCollection());
        }

        // =========================
        // SAVE (PNG transparent + ISF)
        // =========================
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            FontStorage.EnsureFolders(_fontId);

            string preferredPng = FontStorage.BuildVariantFilePath(_fontId, _ch, _variantId);
            string preferredIsf = System.IO.Path.ChangeExtension(preferredPng, ".isf");

            SafeWriteIsf(preferredIsf);

            string finalPng = SafeWriteTransparentPng(preferredPng);

            if (!string.Equals(finalPng, preferredPng, StringComparison.OrdinalIgnoreCase))
            {
                string finalIsf = System.IO.Path.ChangeExtension(finalPng, ".isf");
                SafeWriteIsf(finalIsf);
            }

            SavedImagePath = finalPng;
            DialogResult = true;
        }

        private void SafeWriteIsf(string isfPath)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(isfPath) ?? "");
                string tmp = isfPath + ".tmp";

                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Ink.Strokes.Save(fs);
                }

                TryMoveReplace(tmp, isfPath);
            }
            catch { }
        }

        private string SafeWriteTransparentPng(string preferredPath)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(preferredPath) ?? "");

            var oldGuideVis = GuideViewbox.Visibility;
            var oldLinesVis = GuideLinesCanvas.Visibility;

            GuideViewbox.Visibility = Visibility.Collapsed;
            GuideLinesCanvas.Visibility = Visibility.Collapsed;

            Ink.UpdateLayout();

            int width = (int)Math.Max(1, Ink.ActualWidth);
            int height = (int)Math.Max(1, Ink.ActualHeight);

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(Ink);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            GuideViewbox.Visibility = oldGuideVis;
            GuideLinesCanvas.Visibility = oldLinesVis;

            string tmp = preferredPath + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.Read))
                encoder.Save(fs);

            if (TryMoveReplace(tmp, preferredPath))
                return preferredPath;

            string alt = BuildAlternativePngPath(preferredPath);
            if (TryMoveReplace(tmp, alt))
                return alt;

            try
            {
                File.Copy(tmp, alt, true);
                try { File.Delete(tmp); } catch { }
                return alt;
            }
            catch
            {
                return preferredPath;
            }
        }

        private static bool TryMoveReplace(string tmp, string target)
        {
            try
            {
                if (File.Exists(target))
                {
                    try { File.Delete(target); }
                    catch { return false; }
                }

                File.Move(tmp, target);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildAlternativePngPath(string preferredPath)
        {
            string dir = System.IO.Path.GetDirectoryName(preferredPath) ?? "";
            string name = System.IO.Path.GetFileNameWithoutExtension(preferredPath);
            string ext = System.IO.Path.GetExtension(preferredPath);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return System.IO.Path.Combine(dir, $"{name}_{stamp}{ext}");
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
