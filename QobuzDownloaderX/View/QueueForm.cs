using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.AxHost;

namespace QobuzDownloaderX.View
{
    public partial class QueueForm : Form
    {
        public QueueForm()
        {
            InitializeComponent();
            InitializeCustomStyles();

            Task.Run(() =>
            {
                Task.Delay(3000).Wait();
                this.Invoke(() =>
                { // Add a few queue items using a for loop
                    for (int i = 100; i >=0; i--)
                    {
                        this.AddQueueItem(new QueueItem
                        {
                            Image = @"C:\Bibliothek\Download\proxy-image.jpg", // Ensure this file exists in your project
                            Title = $"Sample Title {i}",
                            Album = $"Sample Album {i}",
                            Artist = $"Sample Artists {i}",
                            State = $"{i*3}/30 🠋",
                            IsAlbum = true,
                            Progress = i * 10 // Progress from 10% to 50%
                        });
                    }
                    this.AddQueueItem(new QueueItem
                    {
                        Image = @"C:\Bibliothek\Download\proxy-image.jpg", // Ensure this file exists in your project
                        Title = $"Hello from",
                        Album = $"Foo",
                        Artist = $"Bar",
                        State = $"{0 * 3}/30 🠋",
                        IsAlbum = false,
                        Progress = 0 * 10 // Progress from 10% to 50%
                    });

                });
            });
        }
    }

    public class QueueItem
    {
        public string Image { get; set; }
        public string Title { get; set; }
        public string Album { get; set; }
        public string Artist { get; set; }
        public bool IsAlbum { get; set; }
        public string State { get; set; }
        public int Progress { get; set; }
    }
}
