using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QobuzDownloaderX.View
{
    public class ModernProgressBar : UserControl
    {
        private int _value;
        private int _maximum = 100;

        public ModernProgressBar()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent;
        }

        public int Value
        {
            get => _value;
            set
            {
                _value = Math.Max(0, Math.Min(value, Maximum));
                this.Invalidate();
            }
        }

        public int Maximum
        {
            get => _maximum;
            set
            {
                _maximum = Math.Max(1, value);
                this.Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            int radius = 10; // Adjust the radius as needed
            int progressWidth = (int)(((double)Value / Maximum) * this.Width);

            using (var path = new GraphicsPath())
            {
                path.AddArc(0, 0, radius, radius, 180, 90);
                path.AddArc(this.Width - radius, 0, radius, radius, 270, 90);
                path.AddArc(this.Width - radius, this.Height - 1 - radius, radius, radius, 0, 90);
                path.AddArc(0, this.Height - 1 - radius, radius, radius, 90, 90);
                path.CloseFigure();

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(new SolidBrush(Color.FromArgb(66, 66, 66)), path);

                using (var progressPath = new GraphicsPath())
                {
                    progressPath.AddArc(0, 0, radius, radius, 180, 90);
                    progressPath.AddArc(progressWidth - radius, 0, radius, radius, 270, 90);
                    progressPath.AddArc(progressWidth - radius, this.Height - 1 - radius, radius, radius, 0, 90);
                    progressPath.AddArc(0, this.Height - 1 - radius, radius, radius, 90, 90);
                    progressPath.CloseFigure();

                    using (var gradientBrush = new LinearGradientBrush(
                        new Point(0, 0),
                        new Point(progressWidth, 0),
                        Color.FromArgb(0, 112, 239), // Start color
                        Color.FromArgb(0, 191, 255))) // End color
                    {
                        e.Graphics.FillPath(gradientBrush, progressPath);
                    }
                }
            }
        }
    }
}
