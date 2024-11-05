using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace QobuzDownloaderX.View
{
    public partial class QueueItemControl : UserControl
    {
        private PictureBox imageBox;
        private Label titleLabel;
        private Label albumLabel;
        private Label artistLabel;
        private Label stateLabel;
        private Label waitingLabel;
        private Button startStopButton;
        private Button cancelButton;
        private ModernProgressBar progressBar;


        private void InitializeComponent()
        {
            imageBox = new PictureBox();
            progressBar = new ModernProgressBar();
            titleLabel = new Label();
            albumLabel = new Label();
            artistLabel = new Label();
            stateLabel = new Label();
            waitingLabel = new Label();
            startStopButton = new Button();
            cancelButton = new Button();
            ((System.ComponentModel.ISupportInitialize)imageBox).BeginInit();
            SuspendLayout();
            // 
            // imageBox
            // 
            imageBox.Location = new Point(16, 35);
            imageBox.Margin = new Padding(5);
            imageBox.Name = "imageBox";
            imageBox.Size = new Size(130, 130);
            imageBox.SizeMode = PictureBoxSizeMode.Zoom;
            imageBox.TabIndex = 0;
            imageBox.TabStop = false;
            // 
            // progressBar
            // 
            progressBar.BackColor = Color.Transparent;
            progressBar.Location = new Point(160, 155);
            progressBar.Margin = new Padding(5);
            progressBar.Maximum = 100;
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(350, 20);
            progressBar.TabIndex = 7;
            progressBar.Value = 0;
            progressBar.Visible = false;
            // 
            // titleLabel
            // 
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(154, 16);
            titleLabel.Margin = new Padding(5, 0, 5, 0);
            titleLabel.Name = "titleLabel";
            titleLabel.Size = new Size(60, 32);
            titleLabel.TabIndex = 1;
            titleLabel.Text = "Title";
            // 
            // albumLabel
            // 
            albumLabel.AutoSize = true;
            albumLabel.Location = new Point(154, 56);
            albumLabel.Margin = new Padding(5, 0, 5, 0);
            albumLabel.Name = "albumLabel";
            albumLabel.Size = new Size(84, 32);
            albumLabel.TabIndex = 2;
            albumLabel.Text = "Album";
            // 
            // artistLabel
            // 
            artistLabel.AutoSize = true;
            artistLabel.Location = new Point(154, 96);
            artistLabel.Margin = new Padding(5, 0, 5, 0);
            artistLabel.Name = "artistLabel";
            artistLabel.Size = new Size(69, 32);
            artistLabel.TabIndex = 6;
            artistLabel.Text = "Artist";
            // 
            // stateLabel
            // 
            stateLabel.AutoSize = true;
            stateLabel.BackColor = Color.Transparent;
            stateLabel.Location = new Point(570, 145);
            stateLabel.Margin = new Padding(5, 0, 5, 0);
            stateLabel.Name = "stateLabel";
            stateLabel.Size = new Size(67, 32);
            stateLabel.TabIndex = 5;
            stateLabel.Visible = _item.Progress != 0;
            stateLabel.Text = "State";
            // 
            // waiting
            // 
            waitingLabel.AutoSize = true;
            waitingLabel.BackColor = Color.Transparent;
            waitingLabel.Location = new Point(250, 150);
            waitingLabel.Margin = new Padding(5, 0, 5, 0);
            waitingLabel.Name = "waitingLabel";
            waitingLabel.Size = new Size(67, 32);
            waitingLabel.TabIndex = 5;
            waitingLabel.Visible = _item.Progress == 0;
            waitingLabel.Text = _item.IsAlbum ? "Album is waiting": "Song is waiting";

            // 
            // startStopButton
            // 
            startStopButton.Location = new Point(530, 16);
            startStopButton.Margin = new Padding(5);
            startStopButton.Name = "startStopButton";
            startStopButton.Size = new Size(162, 50);
            startStopButton.TabIndex = 3;
            startStopButton.Text = "Pause";
            startStopButton.UseVisualStyleBackColor = true;
            // 
            // cancelButton
            // 
            cancelButton.Location = new Point(530, 80);
            cancelButton.Margin = new Padding(5);
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new Size(162, 50);
            cancelButton.TabIndex = 4;
            cancelButton.Text = "Remove";
            cancelButton.UseVisualStyleBackColor = true;
            // 
            // QueueItemControl
            // 
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(45, 45, 45);
            Controls.Add(progressBar);
            Controls.Add(imageBox);
            Controls.Add(titleLabel);
            Controls.Add(albumLabel);
            Controls.Add(artistLabel);
            Controls.Add(stateLabel);
            Controls.Add(startStopButton);
            Controls.Add(cancelButton);
            Controls.Add(waitingLabel);
            Margin = new Padding(5);
            Name = "QueueItemControl";
            Padding = new Padding(8);
            Size = new Size(700, 200);
            ((System.ComponentModel.ISupportInitialize)imageBox).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        private void InitializeCustomStyles()
        {
            this.BackColor = Color.FromArgb(45, 45, 45);
            this.ForeColor = Color.White;
            this.Font = new Font("Hanken Grotesk", 10);

            titleLabel.ForeColor = Color.White;
            albumLabel.ForeColor = Color.WhiteSmoke;
            artistLabel.ForeColor = Color.WhiteSmoke;
            stateLabel.ForeColor = Color.WhiteSmoke;
            waitingLabel.ForeColor = Color.SlateGray;
           
            waitingLabel.Font = new Font("Hanken Grotesk", 8);

            startStopButton.BackColor = Color.FromArgb(0, 112, 239);
            startStopButton.ForeColor = Color.White;
            startStopButton.FlatStyle = FlatStyle.Flat;
            startStopButton.FlatAppearance.BorderSize = 0;
            startStopButton.Font = new Font("Hanken Grotesk", 9);
            startStopButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 90, 190);
            startStopButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 70, 150);

            cancelButton.BackColor = Color.FromArgb(220, 53, 69);
            cancelButton.ForeColor = Color.White;
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderSize = 0;
            cancelButton.Font = new Font("Hanken Grotesk", 9);
            cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 33, 49);
            cancelButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 13, 29);
        }

        

        public void SetProgress(int progress)
        {
            if (progress >= 0)
            {
                progressBar.Visible = true;
                progressBar.Value = progress;
            }
            else
            {
                progressBar.Visible = false;
            }
        }

        public void SetState(string state)
        {
            stateLabel.Text = state;
        }

        public void SetArtist(string artist)
        {
            artistLabel.Text = artist;
        }
    }
}