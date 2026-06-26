using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EsgSignalCreator.Ui.Pipeline
{
    /// <summary>
    /// ARB play-state values, echoing the AWG70000 run-state semantics
    /// (issue #31): the generator is either stopped, actively playing a
    /// waveform, armed and waiting for its trigger, or busy switching states.
    /// </summary>
    public enum PlayState
    {
        Idle,
        Playing,
        WaitingForTrigger,
        Busy
    }

    /// <summary>
    /// A compact status indicator that shows the current ARB <see cref="PlayState"/>
    /// as a coloured dot plus a text label. Built entirely in code (no designer/resx);
    /// drawn with GDI+ in <see cref="OnPaint"/>. Default size ~160x24.
    /// </summary>
    public sealed class PlayStateIndicator : UserControl
    {
        private PlayState _state = PlayState.Idle;

        public PlayStateIndicator()
        {
            // Smooth dot + text; reduce flicker on repaint.
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);

            Size = new Size(160, 24);
            BackColor = Color.Transparent;
        }

        /// <summary>
        /// The play state shown by this control. Setting it repaints the dot and label.
        /// </summary>
        public PlayState State
        {
            get { return _state; }
            set
            {
                if (_state == value)
                {
                    return;
                }

                _state = value;
                Invalidate();
            }
        }

        private static Color ColorFor(PlayState state)
        {
            switch (state)
            {
                case PlayState.Playing:
                    return Color.FromArgb(46, 160, 67);   // green
                case PlayState.WaitingForTrigger:
                    return Color.FromArgb(219, 154, 4);   // amber
                case PlayState.Busy:
                    return Color.FromArgb(31, 111, 235);  // blue
                case PlayState.Idle:
                default:
                    return Color.Gray;
            }
        }

        private static string TextFor(PlayState state)
        {
            switch (state)
            {
                case PlayState.Playing:
                    return "Playing";
                case PlayState.WaitingForTrigger:
                    return "Waiting for trigger";
                case PlayState.Busy:
                    return "Busy";
                case PlayState.Idle:
                default:
                    return "Stopped";
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Color color = ColorFor(_state);
            string text = TextFor(_state);

            // Status dot, vertically centred at the left edge.
            const int diameter = 12;
            const int leftPad = 3;
            int dotY = (Height - diameter) / 2;
            var dotRect = new Rectangle(leftPad, dotY, diameter, diameter);

            using (var fill = new SolidBrush(color))
            {
                g.FillEllipse(fill, dotRect);
            }
            using (var ring = new Pen(ControlPaint.Dark(color, 0.1f)))
            {
                g.DrawEllipse(ring, dotRect);
            }

            // Text label to the right of the dot.
            int textLeft = leftPad + diameter + 6;
            var textRect = new Rectangle(textLeft, 0, Math.Max(0, Width - textLeft), Height);
            TextRenderer.DrawText(
                g,
                text,
                Font,
                textRect,
                ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }
}
