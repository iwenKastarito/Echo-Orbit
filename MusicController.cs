using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Windows.Threading;

namespace EchoOrbit
{
    public class MusicController
    {
        private DispatcherTimer musicTimer;
        private DispatcherTimer musicTitleTimer;
        private int musicTitleOffset = 0;
        private string fullMusicTitle = "";
        private bool isPlaying = false;

        public MediaElement MusicPlayer { get; private set; }
        public Slider ProgressSlider { get; private set; }
        public TextBlock ElapsedTimeText { get; private set; }
        public TextBlock RemainingTimeText { get; private set; }
        public Button PlayPauseButton { get; private set; }
        public TextBlock MusicTitle { get; private set; }
        public Image AudioThumbnailImage { get; private set; }

        public MusicController(MediaElement player, Slider slider, TextBlock elapsed, TextBlock remaining, Button playPause, TextBlock title, Image thumbnail)
        {
            MusicPlayer = player;
            ProgressSlider = slider;
            ElapsedTimeText = elapsed;
            RemainingTimeText = remaining;
            PlayPauseButton = playPause;
            MusicTitle = title;
            AudioThumbnailImage = thumbnail;

            musicTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            musicTimer.Tick += MusicTimer_Tick;

            musicTitleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
            musicTitleTimer.Tick += MusicTitleTimer_Tick;
        }

        private void MusicTimer_Tick(object sender, EventArgs e)
        {
            if (MusicPlayer.NaturalDuration.HasTimeSpan)
            {
                ProgressSlider.Value = MusicPlayer.Position.TotalSeconds;
                UpdateTimeLabels();
            }
        }

        private void UpdateTimeLabels()
        {
            if (MusicPlayer.NaturalDuration.HasTimeSpan)
            {
                TimeSpan elapsed = MusicPlayer.Position;
                TimeSpan total = MusicPlayer.NaturalDuration.TimeSpan;
                TimeSpan remaining = total - elapsed;
                ElapsedTimeText.Text = elapsed.ToString(@"m\:ss");
                RemainingTimeText.Text = remaining.ToString(@"m\:ss");
            }
        }

        private void MusicTitleTimer_Tick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(fullMusicTitle) || fullMusicTitle.Length <= 17)
            {
                musicTitleTimer.Stop();
                return;
            }
            int len = fullMusicTitle.Length;
            if (musicTitleOffset + 17 <= len)
            {
                MusicTitle.Text = fullMusicTitle.Substring(musicTitleOffset, 17);
            }
            else
            {
                int rem = len - musicTitleOffset;
                MusicTitle.Text = fullMusicTitle.Substring(musicTitleOffset, rem) + fullMusicTitle.Substring(0, 17 - rem);
            }
            musicTitleOffset = (musicTitleOffset + 1) % len;
        }

        public void OpenFileAndPlay()
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Audio Files|*.mp3;*.wav;*.wma" };
            if (ofd.ShowDialog() == true)
            {
                PlayMusicFromFile(ofd.FileName);
            }
        }

        public void PlayMusicFromFile(string file)
        {
            if (string.IsNullOrEmpty(file))
                return;

            // Set the source for playback.
            MusicPlayer.Source = new Uri(file, UriKind.Absolute);

            // Update album art.
            BitmapImage bmp = new BitmapImage();
            // First, try to find an external JPEG file.
            string albumArtPath = System.IO.Path.ChangeExtension(file, ".jpg");
            if (File.Exists(albumArtPath))
            {
                bmp.BeginInit();
                bmp.UriSource = new Uri(albumArtPath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
            }
            else
            {
                // Attempt to extract embedded artwork using TagLib#
                try
                {
                    var tagFile = TagLib.File.Create(file);
                    if (tagFile.Tag.Pictures.Length > 0)
                    {
                        var picData = tagFile.Tag.Pictures[0].Data.Data;
                        using (var ms = new MemoryStream(picData))
                        {
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.StreamSource = ms;
                            bmp.EndInit();
                        }
                    }
                    else
                    {
                        // No embedded image: load default image.
                        bmp.BeginInit();
                        bmp.UriSource = new Uri("C:/Users/iwen2/source/repos/Echo Orbit/Echo Orbit/defaultAudioImage.jpg", UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                    }
                }
                catch (Exception)
                {
                    // In case of error, load the default image.
                    bmp.BeginInit();
                    bmp.UriSource = new Uri("C:/Users/iwen2/source/repos/Echo Orbit/Echo Orbit/defaultAudioImage.jpg", UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                }
            }
            AudioThumbnailImage.Source = bmp;

            // Update music title.
            string fileName = System.IO.Path.GetFileName(file);
            fullMusicTitle = fileName;
            MusicTitle.Text = fileName;
            ApplyKineticAnimationToMusicTitle();

            // Start playback.
            MusicPlayer.Play();
            PlayPauseButton.Content = "⏸";
            isPlaying = true;
        }

        public void ApplyKineticAnimationToMusicTitle()
        {
            if (string.IsNullOrEmpty(fullMusicTitle))
            {
                musicTitleTimer.Stop();
                return;
            }
            if (fullMusicTitle.Length <= 17)
            {
                musicTitleTimer.Stop();
                MusicTitle.Text = fullMusicTitle;
                return;
            }
            musicTitleOffset = 0;
            MusicTitle.Text = fullMusicTitle.Substring(0, 17);
            musicTitleTimer.Start();
        }

        public void PlayPause()
        {
            if (isPlaying)
            {
                MusicPlayer.Pause();
                PlayPauseButton.Content = "▶";
                isPlaying = false;
            }
            else
            {
                MusicPlayer.Play();
                PlayPauseButton.Content = "⏸";
                isPlaying = true;
            }
        }

        public void MediaOpened()
        {
            if (MusicPlayer.NaturalDuration.HasTimeSpan)
            {
                ProgressSlider.Maximum = MusicPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                musicTimer.Start();
                UpdateTimeLabels();
            }
        }

        public void MediaEnded()
        {
            musicTimer.Stop();
            PlayPauseButton.Content = "▶";
            isPlaying = false;
        }

        public void UpdatePosition(double value)
        {
            if (MusicPlayer.NaturalDuration.HasTimeSpan)
            {
                MusicPlayer.Position = TimeSpan.FromSeconds(value);
                UpdateTimeLabels();
            }
        }
    }
}
