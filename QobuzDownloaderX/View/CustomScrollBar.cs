using System;
using System.Drawing;
using System.Windows.Forms;

public class ModernScrollBar : Control
{
    private int maximum = 100;
    private int minimum = 0;
    private int value = 0;
    private int largeChange = 10;
    private int smallChange = 1;
    private Rectangle thumbRect;
    private bool isDragging = false;
    private Point dragStartPoint;

    public event EventHandler<ScrollEventArgs> Scroll;

    public int Maximum
    {
        get { return maximum; }
        set
        {
            maximum = Math.Max(value, minimum); // Ensure maximum is not less than minimum
            Invalidate();
        }
    }

    public int Minimum
    {
        get { return minimum; }
        set
        {
            minimum = Math.Min(value, maximum); // Ensure minimum is not greater than maximum
            Invalidate();
        }
    }

    public int Value
    {
        get { return this.value; }
        set
        {
            int newValue = Math.Max(minimum, Math.Min(maximum, value));
            if (this.value != newValue)
            {
                this.value = newValue;
                Invalidate();
                Scroll?.Invoke(this, new ScrollEventArgs(ScrollEventType.ThumbPosition, this.value));
            }
        }
    }

    public int LargeChange
    {
        get { return largeChange; }
        set { largeChange = value; Invalidate(); }
    }

    public int SmallChange
    {
        get { return smallChange; }
        set { smallChange = value; Invalidate(); }
    }

    public ModernScrollBar()
    {
        this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        this.BackColor = Color.FromArgb(33, 33, 33);
        this.ForeColor = Color.White;
        this.Width = 15;
        this.Height = 200; // Set a default height for the scrollbar
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (SolidBrush brush = new(Color.FromArgb(66, 66, 66)))
        {
            e.Graphics.FillRectangle(brush, this.ClientRectangle);
        }

        int trackHeight = this.Height - 20;
        int thumbHeight = (int)(((double)trackHeight / (maximum - minimum)) * trackHeight);
        thumbHeight = Math.Max(thumbHeight, 10); // Ensure the thumb is at least 10 pixels tall

        int thumbPosition = 10 + (int)(((double)(this.value - minimum) / (maximum - minimum)) * (trackHeight - thumbHeight));
        thumbRect = new Rectangle(0, thumbPosition, this.Width, thumbHeight);

        using (SolidBrush thumbBrush = new(Color.FromArgb(100, 100, 100)))
        {
            e.Graphics.FillRectangle(thumbBrush, thumbRect);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (thumbRect.Contains(e.Location))
        {
            isDragging = true;
            dragStartPoint = e.Location;
        }
        else
        {
            // If the user clicks outside the thumb, adjust the value accordingly
            int clickPosition = e.Y - 10; // Adjust for the top padding
            int newValue = minimum + (int)(((double)clickPosition / (this.Height - 20)) * (maximum - minimum));
            this.Value = newValue;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (isDragging)
        {
            int delta = e.Y - dragStartPoint.Y;
            int newValue = this.value + (int)(((double)delta / (this.Height - 20)) * (maximum - minimum));
            this.Value = newValue;
            dragStartPoint = e.Location; // Update the drag start point to prevent jumping
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        isDragging = false;
    }
}