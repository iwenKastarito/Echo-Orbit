using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EchoOrbit.Controls
{
    public class BeeHivePanel : FrameworkElement
    {
        // Hexagon geometry parameters.
        public double R { get; set; } = 20;
        public double HexWidth => 2 * R;
        public double HexHeight => Math.Sqrt(3) * R;

        // Spacing between hexagon centers.
        public double HorizontalSpacing => 1.5 * R;
        public double VerticalSpacing => HexHeight;

        // Base appearance.
        public Brush BaseFill { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4D4D4D"));
        public Color BaseStrokeColor { get; set; } = Colors.DarkGray;
        public Color HighlightStrokeColor { get; set; } = Colors.LightGoldenrodYellow;
        public double HighlightThreshold { get; set; } = 80;

        // Glow effect parameters.
        public double GlowMultiplier { get; set; } = 3;
        public double GlowMaxOpacity { get; set; } = 0.7;

        // Drop shadow parameters.
        public Vector ShadowOffset { get; set; } = new Vector(3, 3);
        public double ShadowOpacity { get; set; } = 0.3;

        // Shimmer effect parameters.
        private double _time = 0;
        public double ShimmerAmplitude { get; set; } = 0.07; // maximum offset as fraction of bounding box
        public double ShimmerFrequency { get; set; } = 0.5;   // frequency multiplier

        // Cache the base hexagon geometry (centered at 0,0).
        private Geometry _baseHexagonGeometry;

        // Cache for drop shadow brush.
        private SolidColorBrush _shadowBrush;

        // Timestamp for update frequency control (target ~30 FPS).
        private DateTime _lastUpdate = DateTime.MinValue;

        #region Dependency Property: IsBeehiveActive
        public static readonly DependencyProperty IsBeehiveActiveProperty =
            DependencyProperty.Register(
                nameof(IsBeehiveActive),
                typeof(bool),
                typeof(BeeHivePanel),
                new PropertyMetadata(true));

        public bool IsBeehiveActive
        {
            get => (bool)GetValue(IsBeehiveActiveProperty);
            set => SetValue(IsBeehiveActiveProperty, value);
        }
        #endregion

        public BeeHivePanel()
        {
            Loaded += BeeHivePanel_Loaded;
            Unloaded += BeeHivePanel_Unloaded;
        }

        private void BeeHivePanel_Loaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering += OnRendering;
        }

        private void BeeHivePanel_Unloaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (!IsBeehiveActive)
                return;

            Window parentWindow = Window.GetWindow(this);
            if (parentWindow == null || !parentWindow.IsActive || parentWindow.WindowState == WindowState.Minimized)
                return;

            DateTime now = DateTime.UtcNow;
            if ((now - _lastUpdate).TotalMilliseconds < 100) // 100ms for 10 FPS
                return;
            _lastUpdate = now;

            _time += 0.02; // Adjust speed as desired.
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // Create and cache the base hexagon geometry if needed.
            if (_baseHexagonGeometry == null)
            {
                _baseHexagonGeometry = CreateBaseHexagonGeometry();
            }

            // Create and cache the shadow brush if needed.
            if (_shadowBrush == null)
            {
                _shadowBrush = new SolidColorBrush(Color.FromArgb((byte)(ShadowOpacity * 255), 0, 0, 0));
                _shadowBrush.Freeze();
            }

            // Get the current mouse position relative to this control.
            Point mousePos = Mouse.GetPosition(this);

            // Draw the overall background.
            dc.DrawRectangle(BaseFill, null, new Rect(0, 0, ActualWidth, ActualHeight));

            int colCount = (int)Math.Ceiling(ActualWidth / HorizontalSpacing) + 1;
            int rowCount = (int)Math.Ceiling(ActualHeight / VerticalSpacing) + 1;

            // Get the base fill color.
            Color baseFillColor = Colors.Gray;
            if (BaseFill is SolidColorBrush scb)
                baseFillColor = scb.Color;

            // Loop through grid cells.
            for (int col = 0; col < colCount; col++)
            {
                for (int row = 0; row < rowCount; row++)
                {
                    double centerX = col * HorizontalSpacing + R;
                    double centerY = row * VerticalSpacing + HexHeight / 2;
                    if (col % 2 == 1)
                        centerY += VerticalSpacing / 2;

                    // Compute distance from hexagon center to current mouse position.
                    double dx = centerX - mousePos.X;
                    double dy = centerY - mousePos.Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    // Compute interpolation factor (squared for smoother transitions).
                    double t = Math.Max(0, 1 - (distance / HighlightThreshold));
                    t = t * t;

                    // Compute shimmer offset.
                    double phase = _time + (col + row) * ShimmerFrequency;
                    double offsetX = ShimmerAmplitude * Math.Sin(phase);
                    double offsetY = ShimmerAmplitude * Math.Cos(phase);

                    // Create a dynamic radial gradient brush.
                    RadialGradientBrush gradientBrush = new RadialGradientBrush
                    {
                        GradientOrigin = new Point(0.5 + offsetX, 0.5 + offsetY),
                        Center = new Point(0.5, 0.5),
                        RadiusX = 0.5,
                        RadiusY = 0.5,
                        MappingMode = BrushMappingMode.RelativeToBoundingBox
                    };
                    Color innerColor = InterpolateColor(baseFillColor, Colors.White, t * 0.6);
                    gradientBrush.GradientStops.Add(new GradientStop(innerColor, 0.0));
                    gradientBrush.GradientStops.Add(new GradientStop(InterpolateColor(baseFillColor, Colors.White, t * 0.3), 0.7));
                    gradientBrush.GradientStops.Add(new GradientStop(baseFillColor, 1.0));
                    gradientBrush.Freeze();

                    // Draw drop shadow.
                    dc.PushTransform(new TranslateTransform(centerX + ShadowOffset.X, centerY + ShadowOffset.Y));
                    dc.DrawGeometry(_shadowBrush, null, _baseHexagonGeometry);
                    dc.Pop();

                    // Draw the hexagon.
                    dc.PushTransform(new TranslateTransform(centerX, centerY));

                    // Draw dynamic gradient fill.
                    dc.DrawGeometry(gradientBrush, null, _baseHexagonGeometry);

                    // Draw glow effect if applicable.
                    if (t > 0.1)
                    {
                        double glowThickness = (1 + 3 * t) * GlowMultiplier;
                        byte glowAlpha = (byte)(GlowMaxOpacity * 255 * t);
                        Color glowColor = Color.FromArgb(glowAlpha, HighlightStrokeColor.R, HighlightStrokeColor.G, HighlightStrokeColor.B);
                        Pen glowPen = new Pen(new SolidColorBrush(glowColor), glowThickness)
                        {
                            LineJoin = PenLineJoin.Round,
                            StartLineCap = PenLineCap.Round,
                            EndLineCap = PenLineCap.Round
                        };
                        glowPen.Freeze();
                        dc.DrawGeometry(null, glowPen, _baseHexagonGeometry);
                    }

                    // Draw hexagon outline.
                    Color strokeColor = InterpolateColor(BaseStrokeColor, HighlightStrokeColor, t);
                    double strokeThickness = 1 + 3 * t;
                    Pen pen = new Pen(new SolidColorBrush(strokeColor), strokeThickness)
                    {
                        LineJoin = PenLineJoin.Round,
                        StartLineCap = PenLineCap.Round,
                        EndLineCap = PenLineCap.Round
                    };
                    pen.Freeze();
                    dc.DrawGeometry(null, pen, _baseHexagonGeometry);

                    dc.Pop();
                }
            }
        }

        private Geometry CreateBaseHexagonGeometry()
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(-R, 0), true, true);
                ctx.LineTo(new Point(-R / 2, -R * Math.Sqrt(3) / 2), true, false);
                ctx.LineTo(new Point(R / 2, -R * Math.Sqrt(3) / 2), true, false);
                ctx.LineTo(new Point(R, 0), true, false);
                ctx.LineTo(new Point(R / 2, R * Math.Sqrt(3) / 2), true, false);
                ctx.LineTo(new Point(-R / 2, R * Math.Sqrt(3) / 2), true, false);
            }
            geo.Freeze();
            return geo;
        }

        private Color InterpolateColor(Color from, Color to, double t)
        {
            byte a = (byte)(from.A + (to.A - from.A) * t);
            byte r = (byte)(from.R + (to.R - from.R) * t);
            byte g = (byte)(from.G + (to.G - from.G) * t);
            byte b = (byte)(from.B + (to.B - from.B) * t);
            return Color.FromArgb(a, r, g, b);
        }

        protected override Size MeasureOverride(Size availableSize) => availableSize;
    }
}