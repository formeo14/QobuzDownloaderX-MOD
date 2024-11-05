using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QobuzDownloaderX.View
{
    public partial class QueueItemControl : UserControl
    {
        private Timer unloadTimer;
        static Timer scrollEndTimer;
        static Rectangle _loadinGArea = new(-100, -1000, 500, 3000);
        QueueItem _item;
        public QueueItemControl(QueueItem item)
        {
            _item = item;
            // Initialize the timer
            unloadTimer = new Timer
            {
                Interval = 10000 // 20 seconds
            };
            unloadTimer.Tick += UnloadTimer_Tick;

            InitializeComponent();
            InitializeCustomStyles();
            // Set the properties of the controls
            titleLabel.Text = _item.Title;
            stateLabel.Text = _item.State;
            albumLabel.Text = _item.Album;
            artistLabel.Text = _item.Artist;
            progressBar.Value = _item.Progress;
            progressBar.Visible = _item.Progress > 0;

            scrollEndTimer ??= new Timer
            {
                Interval = 500
            };
            scrollEndTimer.Tick += ScrollEndTimer_Tick;

            this.Paint += QueueItemControl_Paint;
        }




        private void QueueItemControl_Paint(object sender, PaintEventArgs e)
        {
            if (!isSet)
            {
                scrollEndTimer.Stop();
                scrollEndTimer.Start();
            }
        }

        private void ScrollEndTimer_Tick(object sender, EventArgs e)
        {
            scrollEndTimer.Stop();
            UpdateImageLoading();
        }

        private void UpdateImageLoading()
        {
                if (_loadinGArea.IntersectsWith(this.Bounds))
                {
                    LoadImage();
                    unloadTimer.Stop(); // Stop the timer if the control is visible
                }
                else
                {
                    unloadTimer.Start(); // Start the timer if the control is not visible
                }
        }

        bool isSet = false;
        private void LoadImage()
        {
            if (imageBox.Image == null && !isSet)
            {
                isSet = true;
                imageBox.Image = Image.FromFile(_item.Image);
            }
        }

        private void UnloadImage()
        {
            if (!_loadinGArea.IntersectsWith(this.Bounds))
            {
                imageBox.Image?.Dispose();
                imageBox.Image = null;
                isSet = false;
            }
        }

        private void UnloadTimer_Tick(object sender, EventArgs e)
        {
            unloadTimer.Stop(); // Stop the timer
            UnloadImage(); // Unload the image
        }
    }
}
