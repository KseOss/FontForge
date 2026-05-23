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
using System.Windows.Threading;

using IOPath = System.IO.Path;
using WpfPath = System.Windows.Shapes.Path;
using WpfLine = System.Windows.Shapes.Line;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfRectangle = System.Windows.Shapes.Rectangle;

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

        private Stroke? _curveSelectedStroke;

        // Теперь это НЕ точки исходного штриха, а управляющие точки кривой
        private List<Point> _curveControlPoints = new();

        private WpfEllipse? _activeCurveHandle;
        private int _activeCurveControlIndex = -1;
        private Point _lastCurveDragPoint;
        private bool _curveDragChanged = false;

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
            BrushNormalRadio.IsChecked = true;

            ApplyMode();

            LoadExistingStrokesIfAny();

            PushUndoSnapshot();

            UpdatePrettySliderFill(BrushSizeSlider);
            UpdatePrettySliderFill(CalligraphyAngleSlider);
            UpdatePrettySliderFill(EraserSizeSlider);
            UpdatePrettySliderFill(CurvePointDensitySlider);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_uiReady)
                return;

            DrawGuides();

            UpdatePrettySliderFill(BrushSizeSlider);
            UpdatePrettySliderFill(CalligraphyAngleSlider);
            UpdatePrettySliderFill(EraserSizeSlider);
            UpdatePrettySliderFill(CurvePointDensitySlider);

            RebuildCurveOverlay();
        }

        private void LoadExistingStrokesIfAny()
        {
            try
            {
                string pngPath = FontStorage.BuildVariantFilePath(_fontId, _ch, _variantId);
                string isfPath = IOPath.ChangeExtension(pngPath, ".isf");

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

        private void BrushTypeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_uiReady)
                return;

            ApplyBrush();

            if (ModeDrawRadio.IsChecked == true)
                ApplyMode();
        }

        private void ApplyMode()
        {
            if (ModeEditRadio.IsChecked == true)
            {
                CurveEditCanvas.Visibility = Visibility.Collapsed;
                ClearCurveSelection();

                Ink.EditingMode = InkCanvasEditingMode.Select;
                Ink.EditingModeInverted = InkCanvasEditingMode.EraseByStroke;
                Ink.Cursor = Cursors.Arrow;

                DeleteSelectedButton.Visibility = Visibility.Visible;
                DeleteCurveButton.Visibility = Visibility.Collapsed;
                return;
            }

            if (ModeEraseRadio.IsChecked == true)
            {
                CurveEditCanvas.Visibility = Visibility.Collapsed;
                ClearCurveSelection();

                Ink.Select(new StrokeCollection());
                Ink.EditingMode = InkCanvasEditingMode.EraseByPoint;
                Ink.EditingModeInverted = InkCanvasEditingMode.Ink;
                Ink.Cursor = Cursors.Cross;

                DeleteSelectedButton.Visibility = Visibility.Collapsed;
                DeleteCurveButton.Visibility = Visibility.Collapsed;

                ApplyEraser();
                return;
            }

            if (ModeCurveRadio.IsChecked == true)
            {
                Ink.Select(new StrokeCollection());
                Ink.EditingMode = InkCanvasEditingMode.None;
                Ink.Cursor = Cursors.Arrow;

                CurveEditCanvas.Visibility = Visibility.Visible;
                CurveEditCanvas.Cursor = Cursors.Cross;

                DeleteSelectedButton.Visibility = Visibility.Collapsed;
                DeleteCurveButton.Visibility = _curveSelectedStroke == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;

                RebuildCurveOverlay();
                return;
            }

            CurveEditCanvas.Visibility = Visibility.Collapsed;
            ClearCurveSelection();

            Ink.Select(new StrokeCollection());
            Ink.EditingMode = InkCanvasEditingMode.Ink;
            Ink.EditingModeInverted = InkCanvasEditingMode.EraseByStroke;
            Ink.Cursor = Cursors.Pen;

            DeleteSelectedButton.Visibility = Visibility.Collapsed;
            DeleteCurveButton.Visibility = Visibility.Collapsed;

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
            var border = new WpfRectangle
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
                var line = new WpfLine
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

            var leftZone = new WpfRectangle
            {
                Width = leftX,
                Height = h,
                Fill = zoneBrush
            };

            Canvas.SetLeft(leftZone, 0);
            Canvas.SetTop(leftZone, 0);
            GuideLinesCanvas.Children.Add(leftZone);

            var rightZone = new WpfRectangle
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
            var line = new WpfLine
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
            UpdatePrettySliderFill(CalligraphyAngleSlider);
        }

        private void EraserSettings_Changed(object sender, RoutedEventArgs e)
        {
            if (!_uiReady)
                return;

            ApplyEraser();
            UpdatePrettySliderFill(EraserSizeSlider);
        }

        private void CurvePointDensity_Changed(object sender, RoutedEventArgs e)
        {
            if (!_uiReady)
                return;

            UpdatePrettySliderFill(CurvePointDensitySlider);

            if (_curveSelectedStroke != null)
                _curveControlPoints = CreateControlPointsFromStroke(_curveSelectedStroke);

            RebuildCurveOverlay();
        }

        private void ApplyBrush()
        {
            double size = Math.Max(1, BrushSizeSlider.Value);

            if (BrushCalligraphyRadio != null && BrushCalligraphyRadio.IsChecked == true)
            {
                double angle = CalligraphyAngleSlider?.Value ?? -35;
                double radians = angle * Math.PI / 180.0;

                double cos = Math.Cos(radians);
                double sin = Math.Sin(radians);

                var attributes = new DrawingAttributes
                {
                    Color = Colors.Black,
                    Width = size * 1.95,
                    Height = Math.Max(2, size * 0.45),
                    FitToCurve = true,
                    IgnorePressure = true,
                    StylusTip = StylusTip.Ellipse
                };

                attributes.StylusTipTransform = new Matrix(
                    cos,
                    sin,
                    -sin,
                    cos,
                    0,
                    0);

                Ink.DefaultDrawingAttributes = attributes;
            }
            else
            {
                Ink.DefaultDrawingAttributes = new DrawingAttributes
                {
                    Color = Colors.Black,
                    Width = size,
                    Height = size,
                    FitToCurve = true,
                    IgnorePressure = true,
                    StylusTip = StylusTip.Ellipse,
                    StylusTipTransform = Matrix.Identity
                };
            }
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

            ClearCurveSelection();
            ApplyMode();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            Ink.Strokes.Clear();
            Ink.Select(new StrokeCollection());

            ClearCurveSelection();

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

        private void DeleteCurve_Click(object sender, RoutedEventArgs e)
        {
            if (_curveSelectedStroke == null)
                return;

            if (Ink.Strokes.Contains(_curveSelectedStroke))
            {
                Ink.Strokes.Remove(_curveSelectedStroke);
                ClearCurveSelection();
                PushUndoSnapshot();
                ApplyMode();
            }
        }

        private void Ink_SelectionChanged(object sender, EventArgs e)
        {
            if (!_uiReady)
                return;

            if (ModeDrawRadio.IsChecked == true ||
                ModeEraseRadio.IsChecked == true ||
                ModeCurveRadio.IsChecked == true)
            {
                Ink.Select(new StrokeCollection());
            }
        }

        // =========================
        // КОРРЕКЦИЯ КРИВОЙ
        // =========================

        private void CurveEditCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ModeCurveRadio.IsChecked != true)
                return;

            Point point = e.GetPosition(CurveEditCanvas);

            if (e.OriginalSource is WpfEllipse handle && handle.Tag is int controlIndex)
            {
                _activeCurveHandle = handle;
                _activeCurveControlIndex = controlIndex;
                _lastCurveDragPoint = point;
                _curveDragChanged = false;

                handle.CaptureMouse();
                e.Handled = true;
                return;
            }

            Stroke? found = FindNearestStroke(point, maxDistance: 34);

            _curveSelectedStroke = found;

            if (_curveSelectedStroke != null)
                _curveControlPoints = CreateControlPointsFromStroke(_curveSelectedStroke);
            else
                _curveControlPoints.Clear();

            RebuildCurveOverlay();

            DeleteCurveButton.Visibility = _curveSelectedStroke == null
                ? Visibility.Collapsed
                : Visibility.Visible;

            e.Handled = true;
        }

        private void CurveEditCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_activeCurveHandle == null ||
                _activeCurveControlIndex < 0 ||
                _curveSelectedStroke == null ||
                e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            Point current = e.GetPosition(CurveEditCanvas);
            Vector delta = current - _lastCurveDragPoint;

            if (Math.Abs(delta.X) < 0.01 && Math.Abs(delta.Y) < 0.01)
                return;

            MoveControlPointAndRebuildStroke(_activeCurveControlIndex, delta);

            _lastCurveDragPoint = current;
            _curveDragChanged = true;

            RebuildCurveOverlay();

            e.Handled = true;
        }

        private void CurveEditCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_activeCurveHandle != null)
            {
                _activeCurveHandle.ReleaseMouseCapture();
                _activeCurveHandle = null;
            }

            _activeCurveControlIndex = -1;

            if (_curveDragChanged)
            {
                _curveDragChanged = false;
                PushUndoSnapshot();
            }

            e.Handled = true;
        }

        private Stroke? FindNearestStroke(Point point, double maxDistance)
        {
            Stroke? bestStroke = null;
            double bestDistance = maxDistance * maxDistance;

            foreach (Stroke stroke in Ink.Strokes)
            {
                List<Point> points = GetStrokePoints(stroke);

                for (int i = 0; i < points.Count; i++)
                {
                    double d = DistanceSquared(point, points[i]);

                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        bestStroke = stroke;
                    }
                }

                for (int i = 0; i < points.Count - 1; i++)
                {
                    double d = DistancePointToSegmentSquared(point, points[i], points[i + 1]);

                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        bestStroke = stroke;
                    }
                }
            }

            return bestStroke;
        }

        private static double DistanceSquared(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;

            return dx * dx + dy * dy;
        }

        private static double DistancePointToSegmentSquared(Point p, Point a, Point b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;

            if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                return DistanceSquared(p, a);

            double t =
                ((p.X - a.X) * dx + (p.Y - a.Y) * dy) /
                (dx * dx + dy * dy);

            t = Math.Clamp(t, 0, 1);

            Point projection = new Point(
                a.X + dx * t,
                a.Y + dy * t);

            return DistanceSquared(p, projection);
        }

        private void ClearCurveSelection()
        {
            _curveSelectedStroke = null;
            _curveControlPoints.Clear();

            _activeCurveHandle = null;
            _activeCurveControlIndex = -1;
            _curveDragChanged = false;

            if (CurveEditCanvas != null)
                CurveEditCanvas.Children.Clear();
        }

        private void RebuildCurveOverlay()
        {
            if (CurveEditCanvas == null)
                return;

            CurveEditCanvas.Children.Clear();

            if (ModeCurveRadio == null || ModeCurveRadio.IsChecked != true)
                return;

            if (_curveSelectedStroke == null)
                return;

            if (!Ink.Strokes.Contains(_curveSelectedStroke))
            {
                ClearCurveSelection();
                return;
            }

            if (_curveControlPoints.Count < 2)
                _curveControlPoints = CreateControlPointsFromStroke(_curveSelectedStroke);

            DrawSelectedCurvePreview();
            DrawCurveHandles();
        }

        private List<Point> CreateControlPointsFromStroke(Stroke stroke)
        {
            List<Point> source = GetStrokePoints(stroke);

            if (source.Count <= 2)
                return source;

            int targetCount = (int)Math.Round(CurvePointDensitySlider.Value);
            targetCount = Math.Clamp(targetCount, 4, 80);

            if (source.Count <= targetCount)
                return source;

            return ResamplePolylineByLength(source, targetCount);
        }

        private static List<Point> GetStrokePoints(Stroke stroke)
        {
            var points = new List<Point>();

            foreach (StylusPoint p in stroke.StylusPoints)
                points.Add(new Point(p.X, p.Y));

            return points;
        }

        private static List<Point> ResamplePolylineByLength(List<Point> points, int targetCount)
        {
            var result = new List<Point>();

            if (points.Count == 0)
                return result;

            if (points.Count == 1 || targetCount <= 1)
            {
                result.Add(points[0]);
                return result;
            }

            double totalLength = 0;

            for (int i = 0; i < points.Count - 1; i++)
                totalLength += Distance(points[i], points[i + 1]);

            if (totalLength <= 0.001)
            {
                result.Add(points[0]);
                result.Add(points[^1]);
                return result;
            }

            result.Add(points[0]);

            double step = totalLength / (targetCount - 1);
            double nextDistance = step;
            double walked = 0;

            int segmentIndex = 0;

            while (result.Count < targetCount - 1 && segmentIndex < points.Count - 1)
            {
                Point a = points[segmentIndex];
                Point b = points[segmentIndex + 1];

                double segmentLength = Distance(a, b);

                if (segmentLength <= 0.001)
                {
                    segmentIndex++;
                    continue;
                }

                if (walked + segmentLength >= nextDistance)
                {
                    double local = (nextDistance - walked) / segmentLength;

                    Point p = new Point(
                        a.X + (b.X - a.X) * local,
                        a.Y + (b.Y - a.Y) * local);

                    result.Add(p);
                    nextDistance += step;
                }
                else
                {
                    walked += segmentLength;
                    segmentIndex++;
                }
            }

            result.Add(points[^1]);

            return result;
        }

        private static double Distance(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        private void MoveControlPointAndRebuildStroke(int controlIndex, Vector delta)
        {
            if (_curveSelectedStroke == null)
                return;

            if (controlIndex < 0 || controlIndex >= _curveControlPoints.Count)
                return;

            Point old = _curveControlPoints[controlIndex];

            double newX = old.X + delta.X;
            double newY = old.Y + delta.Y;

            newX = Math.Clamp(newX, 0, Math.Max(1, Ink.ActualWidth));
            newY = Math.Clamp(newY, 0, Math.Max(1, Ink.ActualHeight));

            _curveControlPoints[controlIndex] = new Point(newX, newY);

            StylusPointCollection rebuilt = BuildStylusPointsFromSmoothCurve(_curveControlPoints);

            if (rebuilt.Count >= 2)
                _curveSelectedStroke.StylusPoints = rebuilt;
        }

        private StylusPointCollection BuildStylusPointsFromSmoothCurve(List<Point> controls)
        {
            var result = new StylusPointCollection();

            if (controls.Count == 0)
                return result;

            if (controls.Count == 1)
            {
                result.Add(new StylusPoint(controls[0].X, controls[0].Y));
                return result;
            }

            if (controls.Count == 2)
            {
                AddLinearSamples(result, controls[0], controls[1], 18);
                return result;
            }

            int samplesPerSegment = 18;

            for (int i = 0; i < controls.Count - 1; i++)
            {
                Point p0 = i == 0 ? controls[i] : controls[i - 1];
                Point p1 = controls[i];
                Point p2 = controls[i + 1];
                Point p3 = i + 2 < controls.Count ? controls[i + 2] : p2;

                for (int s = 0; s <= samplesPerSegment; s++)
                {
                    if (i > 0 && s == 0)
                        continue;

                    double t = s / (double)samplesPerSegment;
                    Point p = CatmullRom(p0, p1, p2, p3, t);

                    result.Add(new StylusPoint(p.X, p.Y));
                }
            }

            return result;
        }

        private static void AddLinearSamples(StylusPointCollection result, Point a, Point b, int samples)
        {
            for (int i = 0; i <= samples; i++)
            {
                double t = i / (double)samples;

                Point p = new Point(
                    a.X + (b.X - a.X) * t,
                    a.Y + (b.Y - a.Y) * t);

                result.Add(new StylusPoint(p.X, p.Y));
            }
        }

        private static Point CatmullRom(Point p0, Point p1, Point p2, Point p3, double t)
        {
            double t2 = t * t;
            double t3 = t2 * t;

            double x =
                0.5 * (
                    2 * p1.X +
                    (-p0.X + p2.X) * t +
                    (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 +
                    (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);

            double y =
                0.5 * (
                    2 * p1.Y +
                    (-p0.Y + p2.Y) * t +
                    (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 +
                    (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);

            return new Point(x, y);
        }

        private void DrawSelectedCurvePreview()
        {
            if (_curveControlPoints.Count < 2)
                return;

            Geometry geometry = BuildSmoothCurveGeometry(_curveControlPoints);

            var path = new WpfPath
            {
                Data = geometry,
                Stroke = GetOverlayAccentBrush(0.88),
                StrokeThickness = 2.2,
                StrokeDashArray = new DoubleCollection { 5, 4 },
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                IsHitTestVisible = false
            };

            CurveEditCanvas.Children.Add(path);
        }

        private static Geometry BuildSmoothCurveGeometry(List<Point> controls)
        {
            if (controls.Count == 0)
                return Geometry.Empty;

            if (controls.Count == 1)
                return new EllipseGeometry(controls[0], 1, 1);

            var geometry = new PathGeometry();

            var figure = new PathFigure
            {
                StartPoint = controls[0],
                IsClosed = false,
                IsFilled = false
            };

            if (controls.Count == 2)
            {
                figure.Segments.Add(new LineSegment(controls[1], true));
                geometry.Figures.Add(figure);
                return geometry;
            }

            for (int i = 0; i < controls.Count - 1; i++)
            {
                Point p0 = i == 0 ? controls[i] : controls[i - 1];
                Point p1 = controls[i];
                Point p2 = controls[i + 1];
                Point p3 = i + 2 < controls.Count ? controls[i + 2] : p2;

                Point c1 = new Point(
                    p1.X + (p2.X - p0.X) / 6.0,
                    p1.Y + (p2.Y - p0.Y) / 6.0);

                Point c2 = new Point(
                    p2.X - (p3.X - p1.X) / 6.0,
                    p2.Y - (p3.Y - p1.Y) / 6.0);

                figure.Segments.Add(new BezierSegment(c1, c2, p2, true));
            }

            geometry.Figures.Add(figure);
            return geometry;
        }

        private void DrawCurveHandles()
        {
            for (int i = 0; i < _curveControlPoints.Count; i++)
            {
                Point p = _curveControlPoints[i];

                bool edgePoint = i == 0 || i == _curveControlPoints.Count - 1;

                double size = edgePoint ? 15 : 12;

                var handle = new WpfEllipse
                {
                    Width = size,
                    Height = size,
                    Fill = edgePoint
                        ? GetOverlayAccentBrush(0.96)
                        : new SolidColorBrush(Color.FromArgb(240, 255, 235, 243)),
                    Stroke = GetOverlayAccentBrush(0.98),
                    StrokeThickness = edgePoint ? 2.8 : 2,
                    Cursor = Cursors.SizeAll,
                    Tag = i,
                    ToolTip = edgePoint
                        ? "Конечная точка кривой"
                        : "Управляющая точка кривой"
                };

                Canvas.SetLeft(handle, p.X - size / 2);
                Canvas.SetTop(handle, p.Y - size / 2);

                CurveEditCanvas.Children.Add(handle);
            }
        }

        private Brush GetOverlayAccentBrush(double opacity)
        {
            Color accent = GetAccentColor();

            return new SolidColorBrush(Color.FromArgb(
                (byte)Math.Clamp(255 * opacity, 0, 255),
                accent.R,
                accent.G,
                accent.B));
        }

        // =========================
        // СОХРАНЕНИЕ
        // =========================

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            FontStorage.EnsureFolders(_fontId);

            string preferredPng = FontStorage.BuildVariantFilePath(_fontId, _ch, _variantId);
            string preferredIsf = IOPath.ChangeExtension(preferredPng, ".isf");

            SafeWriteIsf(preferredIsf);

            string finalPng = SafeWriteTransparentPng(preferredPng);

            if (!string.Equals(finalPng, preferredPng, StringComparison.OrdinalIgnoreCase))
            {
                string finalIsf = IOPath.ChangeExtension(finalPng, ".isf");
                SafeWriteIsf(finalIsf);
            }

            SavedImagePath = finalPng;
            DialogResult = true;
        }

        private void SafeWriteIsf(string isfPath)
        {
            try
            {
                Directory.CreateDirectory(IOPath.GetDirectoryName(isfPath) ?? "");

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
            Directory.CreateDirectory(IOPath.GetDirectoryName(preferredPath) ?? "");

            Visibility oldGuideVis = GuideViewbox.Visibility;
            Visibility oldLinesVis = GuideLinesCanvas.Visibility;
            Visibility oldCurveVis = CurveEditCanvas.Visibility;

            GuideViewbox.Visibility = Visibility.Collapsed;
            GuideLinesCanvas.Visibility = Visibility.Collapsed;
            CurveEditCanvas.Visibility = Visibility.Collapsed;

            Ink.UpdateLayout();

            int width = (int)Math.Max(1, Ink.ActualWidth);
            int height = (int)Math.Max(1, Ink.ActualHeight);

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(Ink);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            GuideViewbox.Visibility = oldGuideVis;
            GuideLinesCanvas.Visibility = oldLinesVis;
            CurveEditCanvas.Visibility = oldCurveVis;

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
            string dir = IOPath.GetDirectoryName(preferredPath) ?? "";
            string name = IOPath.GetFileNameWithoutExtension(preferredPath);
            string ext = IOPath.GetExtension(preferredPath);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            return IOPath.Combine(dir, $"{name}_{stamp}{ext}");
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}