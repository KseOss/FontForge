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
using System.Windows.Threading;
using Path = System.IO.Path;

namespace FontForge
{
    public partial class GlyphEditorWindow : Window
    {
        private readonly Guid _fontId;
        private readonly string _ch;
        private readonly Guid _variantId;

        private readonly Stack<StrokeCollection> _undo = new();

        private bool _uiReady = false;
        private bool _eraseUndoQueued = false;

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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _uiReady = true;

            DrawGuides();
            ApplyGuideVisibility();
            ApplyBrush();
            ApplyEraser();

            ModeDrawRadio.IsChecked = true;
            ApplyMode();

            LoadExistingStrokesIfAny();

            PushUndoSnapshot();

            UpdatePrettySliderFill(BrushSizeSlider);
            UpdatePrettySliderFill(EraserSizeSlider);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_uiReady)
                return;

            DrawGuides();
            UpdatePrettySliderFill(BrushSizeSlider);
            UpdatePrettySliderFill(EraserSizeSlider);
        }

        private void LoadExistingStrokesIfAny()
        {
            try
            {
                string pngPath = FontStorage.BuildVariantFilePath(_fontId, _ch, _variantId);
                string isfPath = Path.ChangeExtension(pngPath, ".isf");

                if (File.Exists(isfPath))
                {
                    using var fs = new FileStream(isfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    Ink.Strokes = new StrokeCollection(fs);
                }
            }
            catch
            {
                // Если ISF не прочитался, открываем пустой холст.
            }
        }

        private void Ink_PreviewMouseDown_ForceMode(object sender, MouseButtonEventArgs e)
        {
            if (!_uiReady)
                return;

            ApplyMode();
        }

        private void Ink_PreviewStylusDown_ForceMode(object sender, StylusDownEventArgs e)
        {
            if (!_uiReady)
                return;

            ApplyMode();
        }

        private void ModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_uiReady)
                return;

            ApplyMode();
        }

        private void ApplyMode()
        {
            if (ModeEditRadio.IsChecked == true)
            {
                Ink.EditingMode = InkCanvasEditingMode.Select;
                Ink.EditingModeInverted = InkCanvasEditingMode.EraseByStroke;
                Ink.Cursor = Cursors.Arrow;
                DeleteSelectedButton.Visibility = Visibility.Visible;
                return;
            }

            if (ModeEraseRadio.IsChecked == true)
            {
                Ink.Select(new StrokeCollection());
                Ink.EditingMode = InkCanvasEditingMode.EraseByPoint;
                Ink.EditingModeInverted = InkCanvasEditingMode.Ink;
                Ink.Cursor = Cursors.Cross;
                DeleteSelectedButton.Visibility = Visibility.Collapsed;
                ApplyEraser();
                return;
            }

            Ink.Select(new StrokeCollection());
            Ink.EditingMode = InkCanvasEditingMode.Ink;
            Ink.EditingModeInverted = InkCanvasEditingMode.EraseByStroke;
            Ink.Cursor = Cursors.Pen;
            DeleteSelectedButton.Visibility = Visibility.Collapsed;
            ApplyBrush();
        }

        private void ShowGuideCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (!_uiReady)
                return;

            ApplyGuideVisibility();
        }

        private void ShowConnectGuidesCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (!_uiReady)
                return;

            DrawGuides();
        }

        private void ApplyGuideVisibility()
        {
            GuideViewbox.Visibility = ShowGuideCheck.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void DrawGuides()
        {
            GuideLinesCanvas.Children.Clear();

            double w = Math.Max(1, WorkGrid.ActualWidth);
            double h = Math.Max(1, WorkGrid.ActualHeight);

            DrawConnectionZones(w, h);
            DrawMainGuides(w, h);
        }

        private void DrawMainGuides(double w, double h)
        {
            var border = new Rectangle
            {
                Width = Math.Max(1, w - 2),
                Height = Math.Max(1, h - 2),
                Stroke = new SolidColorBrush(Color.FromArgb(115, 60, 64, 60)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 3 },
                Fill = Brushes.Transparent
            };

            Canvas.SetLeft(border, 1);
            Canvas.SetTop(border, 1);
            GuideLinesCanvas.Children.Add(border);

            double[] ys =
            {
                h * 0.18,
                h * 0.38,
                h * 0.62,
                h * 0.82
            };

            foreach (double y in ys)
            {
                var line = new Line
                {
                    X1 = 0,
                    X2 = w,
                    Y1 = y,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(105, 70, 76, 70)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 5 }
                };

                GuideLinesCanvas.Children.Add(line);
            }
        }

        private void DrawConnectionZones(double w, double h)
        {
            if (ShowConnectGuidesCheck == null || ShowConnectGuidesCheck.IsChecked != true)
                return;

            Color accent = GetAccentColor();

            double leftX = w * 0.18;
            double rightX = w * 0.82;

            var zoneBrush = new SolidColorBrush(Color.FromArgb(16, accent.R, accent.G, accent.B));
            var lineBrush = new SolidColorBrush(Color.FromArgb(175, accent.R, accent.G, accent.B));

            var leftZone = new Rectangle
            {
                Width = leftX,
                Height = h,
                Fill = zoneBrush
            };

            Canvas.SetLeft(leftZone, 0);
            Canvas.SetTop(leftZone, 0);
            GuideLinesCanvas.Children.Add(leftZone);

            var rightZone = new Rectangle
            {
                Width = w - rightX,
                Height = h,
                Fill = zoneBrush
            };

            Canvas.SetLeft(rightZone, rightX);
            Canvas.SetTop(rightZone, 0);
            GuideLinesCanvas.Children.Add(rightZone);

            AddVerticalConnectionLine(leftX, h, lineBrush);
            AddVerticalConnectionLine(rightX, h, lineBrush);

            AddConnectionLabel("вход соединения", leftX + 8, 14, lineBrush);
            AddConnectionLabel("выход соединения", Math.Max(8, rightX - 128), 14, lineBrush);
        }

        private void AddVerticalConnectionLine(double x, double h, Brush stroke)
        {
            var line = new Line
            {
                X1 = x,
                X2 = x,
                Y1 = 0,
                Y2 = h,
                Stroke = stroke,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 7, 6 }
            };

            GuideLinesCanvas.Children.Add(line);
        }

        private void AddConnectionLabel(string text, double x, double y, Brush foreground)
        {
            var label = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(215, 243, 244, 239)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, 80, 86, 80)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = foreground
                }
            };

            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);
            GuideLinesCanvas.Children.Add(label);
        }

        private Color GetAccentColor()
        {
            try
            {
                if (Application.Current.Resources["AccentBrush"] is SolidColorBrush brush)
                    return brush.Color;
            }
            catch
            {
                // ignore
            }

            return Color.FromRgb(90, 90, 90);
        }

        private void BrushSettings_Changed(object sender, RoutedEventArgs e)
        {
            if (!_uiReady)
                return;

            ApplyBrush();
            UpdatePrettySliderFill(BrushSizeSlider);
        }

        private void EraserSettings_Changed(object sender, RoutedEventArgs e)
        {
            if (!_uiReady)
                return;

            ApplyEraser();
            UpdatePrettySliderFill(EraserSizeSlider);
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

        private void ApplyEraser()
        {
            double size = Math.Max(4, EraserSizeSlider.Value);

            Ink.EraserShape = new EllipseStylusShape(size, size);
        }

        private void UpdatePrettySliderFill(Slider slider)
        {
            if (slider == null || slider.Template == null)
                return;

            var track = slider.Template.FindName("PART_Track", slider) as Track;
            var fill = slider.Template.FindName("TrackFill", slider) as Border;

            if (track == null || fill == null)
                return;

            double range = Math.Max(1, slider.Maximum - slider.Minimum);
            double k = (slider.Value - slider.Minimum) / range;

            double trackWidth = Math.Max(0, track.ActualWidth);
            fill.Width = trackWidth * k;
        }

        private void Ink_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            PushUndoSnapshot();
        }

        private void Ink_StrokeErasing(object sender, InkCanvasStrokeErasingEventArgs e)
        {
            if (!_uiReady)
                return;

            if (_eraseUndoQueued)
                return;

            _eraseUndoQueued = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _eraseUndoQueued = false;
                PushUndoSnapshot();

            }), DispatcherPriority.Background);
        }

        private void PushUndoSnapshot()
        {
            _undo.Push(Ink.Strokes.Clone());

            if (_undo.Count > 60)
            {
                StrokeCollection[] arr = _undo.Reverse().Take(60).Reverse().ToArray();

                _undo.Clear();

                foreach (StrokeCollection s in arr)
                    _undo.Push(s);
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_undo.Count <= 1)
                return;

            _undo.Pop();

            Ink.Strokes = _undo.Peek().Clone();
            Ink.Select(new StrokeCollection());

            ApplyMode();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            Ink.Strokes.Clear();
            Ink.Select(new StrokeCollection());

            PushUndoSnapshot();
            ApplyMode();
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            StrokeCollection selected = Ink.GetSelectedStrokes();

            if (selected == null || selected.Count == 0)
                return;

            foreach (Stroke stroke in selected.ToList())
                Ink.Strokes.Remove(stroke);

            Ink.Select(new StrokeCollection());

            PushUndoSnapshot();
            ApplyMode();
        }

        private void Ink_SelectionChanged(object sender, EventArgs e)
        {
            if (!_uiReady)
                return;

            if (ModeDrawRadio.IsChecked == true || ModeEraseRadio.IsChecked == true)
                Ink.Select(new StrokeCollection());
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            FontStorage.EnsureFolders(_fontId);

            string preferredPng = FontStorage.BuildVariantFilePath(_fontId, _ch, _variantId);
            string preferredIsf = Path.ChangeExtension(preferredPng, ".isf");

            SafeWriteIsf(preferredIsf);

            string finalPng = SafeWriteTransparentPng(preferredPng);

            if (!string.Equals(finalPng, preferredPng, StringComparison.OrdinalIgnoreCase))
            {
                string finalIsf = Path.ChangeExtension(finalPng, ".isf");
                SafeWriteIsf(finalIsf);
            }

            SavedImagePath = finalPng;
            DialogResult = true;
        }

        private void SafeWriteIsf(string isfPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(isfPath) ?? "");

                string tmp = isfPath + ".tmp";

                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Ink.Strokes.Save(fs);
                }

                TryMoveReplace(tmp, isfPath);
            }
            catch
            {
                // ISF не обязателен для PNG.
            }
        }

        private string SafeWriteTransparentPng(string preferredPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(preferredPath) ?? "");

            Visibility oldGuideVis = GuideViewbox.Visibility;
            Visibility oldLinesVis = GuideLinesCanvas.Visibility;

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
            {
                encoder.Save(fs);
            }

            if (TryMoveReplace(tmp, preferredPath))
                return preferredPath;

            string alt = BuildAlternativePngPath(preferredPath);

            if (TryMoveReplace(tmp, alt))
                return alt;

            try
            {
                File.Copy(tmp, alt, true);

                try
                {
                    File.Delete(tmp);
                }
                catch
                {
                    // ignore
                }

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
                    try
                    {
                        File.Delete(target);
                    }
                    catch
                    {
                        return false;
                    }
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
            string dir = Path.GetDirectoryName(preferredPath) ?? "";
            string name = Path.GetFileNameWithoutExtension(preferredPath);
            string ext = Path.GetExtension(preferredPath);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            return Path.Combine(dir, $"{name}_{stamp}{ext}");
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}