using System;
using System.Collections.ObjectModel;
using System.IO; // For System.IO.File
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // For drag events
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Windows.Threading;
using TagLib;  // Requires TagLib# NuGet package

namespace EchoOrbit
{
    public class MusicController
    {
        private DispatcherTimer musicTimer;
        private DispatcherTimer musicTitleTimer;
        private int musicTitleOffset = 0;
        private string fullMusicTitle = "";
        private bool isPlaying = false;
        private bool isSliderDragging = false;

        public MediaElement MusicPlayer { get; private set; }
        public Slider ProgressSlider { get; private set; }
        public TextBlock ElapsedTimeText { get; private set; }
        public TextBlock RemainingTimeText { get; private set; }
        public Button PlayPauseButton { get; private set; }
        public TextBlock MusicTitle { get; private set; }
        public Image AudioThumbnailImage { get; private set; }

        // If playing from a playlist:
        public ObservableCollection<Song> CurrentPlaylist { get; set; }
        public int CurrentPlaylistIndex { get; set; } = -1;

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

            // Subscribe to MediaEnded event.
            MusicPlayer.MediaEnded += MusicPlayer_MediaEnded;

            // Attach slider drag events.
            ProgressSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(ProgressSlider_DragStarted));
            ProgressSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(ProgressSlider_DragCompleted));
        }

        private void MusicTimer_Tick(object sender, EventArgs e)
        {
            if (!isSliderDragging && MusicPlayer.NaturalDuration.HasTimeSpan)
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
                MusicTitle.Text = fullMusicTitle.Substring(musicTitleOffset, 17);
            else
            {
                int rem = len - musicTitleOffset;
                MusicTitle.Text = fullMusicTitle.Substring(musicTitleOffset, rem) + fullMusicTitle.Substring(0, 17 - rem);
            }
            musicTitleOffset = (musicTitleOffset + 1) % len;
        }

        private void MusicPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            MediaEnded();
        }

        public void MediaEnded()
        {
            musicTimer.Stop();
            if (CurrentPlaylist != null && CurrentPlaylist.Count > 0)
            {
                // For playlists, automatically play next song.
                NextSong();
            }
            else
            {
                // Standalone playback: reset the media so that slider changes work.
                var currentSource = MusicPlayer.Source;
                MusicPlayer.Stop();
                // Reset the Source to force reinitialization.
                MusicPlayer.Source = null;
                MusicPlayer.Source = currentSource;
                MusicPlayer.Position = TimeSpan.Zero;
                ProgressSlider.Value = 0;
                PlayPauseButton.Content = "▶";
                isPlaying = false;
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

        public void UpdatePosition(double value)
        {
            if (MusicPlayer.NaturalDuration.HasTimeSpan)
            {
                MusicPlayer.Position = TimeSpan.FromSeconds(value);
                UpdateTimeLabels();
            }
        }

        private void ProgressSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            isSliderDragging = true;
        }

        private void ProgressSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            isSliderDragging = false;
            UpdatePosition(ProgressSlider.Value);
            // If playback is stopped (because the track ended), resume playback.
            if (!isPlaying)
            {
                MusicPlayer.Play();
                PlayPauseButton.Content = "⏸";
                isPlaying = true;
            }
        }






        public void OpenFileAndPlay()
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Audio Files|*.mp3;*.wav;*.wma" };
            if (ofd.ShowDialog() == true)
                PlayMusicFromFile(ofd.FileName);
        }

        /// <summary>
        /// Plays a song given its file path.
        /// If a playlist is active, it searches for the song and updates the index.
        /// If not found, it defaults to the first song.
        /// </summary>
        public void PlayMusicFromFile(string file)
        {
            if (string.IsNullOrEmpty(file))
                return;

            if (CurrentPlaylist != null && CurrentPlaylist.Count > 0)
            {
                bool found = false;
                for (int i = 0; i < CurrentPlaylist.Count; i++)
                {
                    if (string.Equals(CurrentPlaylist[i].FilePath, file, StringComparison.OrdinalIgnoreCase))
                    {
                        CurrentPlaylistIndex = i;
                        PlaySong(CurrentPlaylist[i]);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    CurrentPlaylistIndex = 0;
                    PlaySong(CurrentPlaylist[0]);
                }
            }
            else
            {
                Song standalone = new Song
                {
                    FilePath = file,
                    Title = System.IO.Path.GetFileName(file),
                    Thumbnail = GetAlbumArt(file)
                };
                PlaySong(standalone);
            }
        }

        private void PlaySong(Song song)
        {
            if (song == null) return;

            MusicPlayer.Source = new Uri(song.FilePath, UriKind.Absolute);

            BitmapImage bmp = new BitmapImage();
            string albumArtPath = System.IO.Path.ChangeExtension(song.FilePath, ".jpg");
            if (System.IO.File.Exists(albumArtPath))
            {
                bmp.BeginInit();
                bmp.UriSource = new Uri(albumArtPath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
            }
            else
            {
                try
                {
                    var tagFile = TagLib.File.Create(song.FilePath);
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
                        bmp.BeginInit();
                        bmp.UriSource = new Uri("C:/Users/iwen2/source/repos/Echo Orbit/Echo Orbit/defaultAudioImage.jpg", UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                    }
                }
                catch
                {
                    bmp.BeginInit();
                    bmp.UriSource = new Uri("C:/Users/iwen2/source/repos/Echo Orbit/Echo Orbit/defaultAudioImage.jpg", UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                }
            }
            AudioThumbnailImage.Source = bmp;

            fullMusicTitle = song.Title;
            MusicTitle.Text = song.Title;
            ApplyKineticAnimationToMusicTitle();

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

        public void NextSong()
        {
            if (CurrentPlaylist != null && CurrentPlaylist.Count > 0)
            {
                if (CurrentPlaylistIndex + 1 < CurrentPlaylist.Count)
                    CurrentPlaylistIndex++;
                else
                    CurrentPlaylistIndex = 0; // Wrap-around to first song.
                PlaySong(CurrentPlaylist[CurrentPlaylistIndex]);
            }
        }

        public void PrevSong()
        {
            if (CurrentPlaylist != null && CurrentPlaylist.Count > 0)
            {
                if (CurrentPlaylistIndex > 0)
                    CurrentPlaylistIndex--;
                else
                    CurrentPlaylistIndex = CurrentPlaylist.Count - 1; // Wrap-around to last song.
                PlaySong(CurrentPlaylist[CurrentPlaylistIndex]);
            }
        }

        private ImageSource GetAlbumArt(string file)
        {
            string albumArtPath = System.IO.Path.ChangeExtension(file, ".jpg");
            if (System.IO.File.Exists(albumArtPath))
            {
                return new BitmapImage(new Uri(albumArtPath, UriKind.Absolute));
            }
            else
            {
                try
                {
                    var tagFile = TagLib.File.Create(file);
                    if (tagFile.Tag.Pictures.Length > 0)
                    {
                        var picData = tagFile.Tag.Pictures[0].Data.Data;
                        using (var ms = new MemoryStream(picData))
                        {
                            BitmapImage bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.StreamSource = ms;
                            bmp.EndInit();
                            return bmp;
                        }
                    }
                }
                catch { }
            }
            return new BitmapImage(new Uri("C:/Users/iwen2/source/repos/Echo Orbit/Echo Orbit/defaultAudioImage.jpg", UriKind.Absolute));
        }
    }

    public class Song
    {
        public string FilePath { get; set; }
        public string Title { get; set; }
        public ImageSource Thumbnail { get; set; }
    }
}
