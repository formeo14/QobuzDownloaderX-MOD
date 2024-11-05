using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QobuzDownloaderX.View
{
    public partial class QueueForm : Form
    {
        private FlowLayoutPanel queueItemsPanel;
        private Button clearButton;
        private Label exitLabel; // Custom close label

        private bool isDragging = false;
        private Point dragStartPoint;

        private void InitializeComponent()
        {
            queueItemsPanel = new FlowLayoutPanel();
            clearButton = new Button();
            exitLabel = new Label(); // Initialize custom close label

            // Suspend layout to optimize performance
            SuspendLayout();

            // 
            // queueItemsPanel
            // 
            queueItemsPanel.AutoScroll = true;
            queueItemsPanel.BackColor = Color.FromArgb(33, 33, 33);
            queueItemsPanel.Dock = DockStyle.Fill;
            queueItemsPanel.FlowDirection = FlowDirection.TopDown;
            queueItemsPanel.Location = new Point(0, 30); // Add margin at the top
            queueItemsPanel.Name = "queueItemsPanel";
            queueItemsPanel.Size = new Size(780, 982); // Adjust size to account for margin
            queueItemsPanel.TabIndex = 0;
            queueItemsPanel.WrapContents = false;

            // 
            // clearButton
            // 
            clearButton.Dock = DockStyle.Bottom;
            clearButton.Location = new Point(10, 1077);
            clearButton.Name = "clearButton";
            clearButton.Size = new Size(760, 60);
            clearButton.TabIndex = 2;
            clearButton.Text = "Clear";
            clearButton.Click += ClearButton_Click;

            // 
            // exitLabel
            // 
            exitLabel.AutoSize = true;
            exitLabel.BackColor = Color.Transparent;
            exitLabel.Font = new Font("Calibri", 13, FontStyle.Bold, GraphicsUnit.Point, 0);
            exitLabel.ForeColor = Color.White;
            exitLabel.Location = new Point(740, 2); // Position at top right
            exitLabel.Margin = new Padding(6, 0, 6, 0);
            exitLabel.Name = "exitLabel";
            exitLabel.Size = new Size(30, 30);
            exitLabel.TabIndex = 3;
            exitLabel.Text = "X";
            exitLabel.TextAlign = ContentAlignment.TopCenter;
            exitLabel.Click += ExitLabel_Click;
            exitLabel.MouseLeave += ExitLabel_MouseLeave;
            exitLabel.MouseHover += ExitLabel_MouseHover;

            // 
            // QueueForm
            // 
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(780, 1156);
            Controls.Add(queueItemsPanel);
            Controls.Add(clearButton);
            Controls.Add(exitLabel); // Add custom close label
            FormBorderStyle = FormBorderStyle.None; // Make form chrome-less
            Name = "QueueForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Queue";
            Padding = new Padding(10, 50, 10, 10); // Set padding

            // Resume layout
            ResumeLayout(false);
            PerformLayout();
        }

        private void InitializeCustomStyles()
        {
            // Set custom styles
            this.BackColor = Color.FromArgb(33, 33, 33);
            this.ForeColor = Color.White;
            this.Font = new Font("Hanken Grotesk", 10);

            this.clearButton.BackColor = Color.FromArgb(66, 66, 66);
            this.clearButton.ForeColor = Color.White;
            this.clearButton.FlatStyle = FlatStyle.Flat;
            this.clearButton.FlatAppearance.BorderSize = 0;
        }

        private void AddQueueItem(QueueItem item)
        {
            var queueItemControl = new QueueItemControl(item);
            this.queueItemsPanel.Controls.Add(queueItemControl);
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            this.queueItemsPanel.Controls.Clear();
        }

        private void ExitLabel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void ExitLabel_MouseHover(object sender, EventArgs e)
        {
            // Change color or style when hovered
            exitLabel.BackColor =  Color.FromArgb(66, 66, 66);
        }

        private void ExitLabel_MouseLeave(object sender, EventArgs e)
        {
            // Revert color or style when not hovered
            exitLabel.BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Draw rounded corners
            using (GraphicsPath path = GetRoundedRectPath(this.ClientRectangle, 20))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                this.Region = new Region(path);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(33, 33, 33)))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }
        }

        private GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
        {
            int diameter = radius;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(rect.Location, size);
            GraphicsPath path = new GraphicsPath();

            // Top left arc  
            path.AddArc(arc, 180, 90);

            // Top right arc  
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom right arc  
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom left arc 
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                dragStartPoint = new Point(e.X, e.Y);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (isDragging)
            {
                Point newPoint = PointToScreen(new Point(e.X, e.Y));
                Location = new Point(newPoint.X - dragStartPoint.X, newPoint.Y - dragStartPoint.Y);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                isDragging = false;
            }
        }
    }
}