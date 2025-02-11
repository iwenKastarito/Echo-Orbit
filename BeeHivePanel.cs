using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EchoOrbit.Controls
{
    /// <summary>
    /// A custom control that draws a dynamic, shimmering honeycomb background.
    /// Each hexagon features a drop shadow, a dynamic radial gradient fill with a shimmering effect,
    /// and a refined glow effect that responds to mouse proximity.
    /// </summary>
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

        private Point mousePosition = new Point(-1000, -1000);

        public BeeHivePanel()
        {
            // Update _time on each rendering pass.
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            _time += 0.02; // Adjust speed as desired.
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // Always get the current mouse position relative to this panel.
            Point mousePos = Mouse.GetPosition(this);

            // Draw overall background.
            dc.DrawRectangle(BaseFill, null, new Rect(0, 0, ActualWidth, ActualHeight));

            int colCount = (int)Math.Ceiling(ActualWidth / HorizontalSpacing) + 1;
            int rowCount = (int)Math.Ceiling(ActualHeight / VerticalSpacing) + 1;

            // Get the base fill color.
            Color baseFillColor = Colors.Gray;
            if (BaseFill is SolidColorBrush scb)
                baseFillColor = scb.Color;

            for (int col = 0; col < colCount; col++)
            {
                for (int row = 0; row < rowCount; row++)
                {
                    double centerX = col * HorizontalSpacing + R;
                    double centerY = row * VerticalSpacing + HexHeight / 2;
                    if (col % 2 == 1)
                        centerY += VerticalSpacing / 2;

                    // Use mousePos instead of a stored mousePosition.
                    double dx = centerX - mousePos.X;
                    double dy = centerY - mousePos.Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    double t = Math.Max(0, 1 - (distance / HighlightThreshold));
                    t = t * t;

                    // Define hexagon vertices (flat‑topped).
                    Point v0 = new Point(centerX - R, centerY);
                    Point v1 = new Point(centerX - R / 2, centerY - (R * Math.Sqrt(3) / 2));
                    Point v2 = new Point(centerX + R / 2, centerY - (R * Math.Sqrt(3) / 2));
                    Point v3 = new Point(centerX + R, centerY);
                    Point v4 = new Point(centerX + R / 2, centerY + (R * Math.Sqrt(3) / 2));
                    Point v5 = new Point(centerX - R / 2, centerY + (R * Math.Sqrt(3) / 2));

                    StreamGeometry hexGeometry = new StreamGeometry();
                    using (StreamGeometryContext ctx = hexGeometry.Open())
                    {
                        ctx.BeginFigure(v0, true, true);
                        ctx.LineTo(v1, true, false);
                        ctx.LineTo(v2, true, false);
                        ctx.LineTo(v3, true, false);
                        ctx.LineTo(v4, true, false);
                        ctx.LineTo(v5, true, false);
                    }
                    hexGeometry.Freeze();

                    // 1. Draw drop shadow.
                    dc.PushTransform(new TranslateTransform(ShadowOffset.X, ShadowOffset.Y));
                    SolidColorBrush shadowBrush = new SolidColorBrush(Color.FromArgb((byte)(ShadowOpacity * 255), 0, 0, 0));
                    shadowBrush.Freeze();
                    dc.DrawGeometry(shadowBrush, null, hexGeometry);
                    dc.Pop();

                    // 2. Calculate a shimmer offset for a dynamic radial gradient.
                    // Use the grid position (col, row) to vary each hexagon's phase.
                    double phase = _time + (col + row) * ShimmerFrequency;
                    double offsetX = ShimmerAmplitude * Math.Sin(phase);
                    double offsetY = ShimmerAmplitude * Math.Cos(phase);

                    // 3. Create a dynamic radial gradient fill with a subtle shimmer.
                    RadialGradientBrush gradientBrush = new RadialGradientBrush
                    {
                        // Animate the gradient origin for a shimmering effect.
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

                    dc.DrawGeometry(gradientBrush, null, hexGeometry);

                    // 4. Draw a refined glow effect.
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
                        dc.DrawGeometry(null, glowPen, hexGeometry);
                    }

                    // 5. Draw the hexagon outline.
                    Color strokeColor = InterpolateColor(BaseStrokeColor, HighlightStrokeColor, t);
                    double strokeThickness = 1 + 3 * t;
                    Pen pen = new Pen(new SolidColorBrush(strokeColor), strokeThickness)
                    {
                        LineJoin = PenLineJoin.Round,
                        StartLineCap = PenLineCap.Round,
                        EndLineCap = PenLineCap.Round
                    };
                    pen.Freeze();
                    dc.DrawGeometry(null, pen, hexGeometry);
                }
            }
        }

        /// <summary>
        /// Linearly interpolates between two colors.
        /// </summary>
        private Color InterpolateColor(Color from, Color to, double t)
        {
            byte a = (byte)(from.A + (to.A - from.A) * t);
            byte r = (byte)(from.R + (to.R - from.R) * t);
            byte g = (byte)(from.G + (to.G - from.G) * t);
            byte b = (byte)(from.B + (to.B - from.B) * t);
            return Color.FromArgb(a, r, g, b);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            mousePosition = e.GetPosition(this);
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            mousePosition = new Point(-1000, -1000);
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize) => availableSize;
    }
}
