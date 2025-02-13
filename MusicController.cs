using System;
using System.Collections.ObjectModel;
using System.IO; // For System.IO.File
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // For drag events
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Windows.Threading;
using TagLib;
using EchoOrbit.Models;

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
        private bool _processingMediaEnded = false;

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
            if (_processingMediaEnded)
                return;
            _processingMediaEnded = true;
            musicTimer.Stop();
            ProgressSlider.Value = 0;

            if (CurrentPlaylist != null && CurrentPlaylist.Count > 0)
            {
                NextSong();
            }
            else
            {
                PlayPauseButton.Content = "▶";
                isPlaying = false;
            }
            MusicPlayer.Dispatcher.BeginInvoke(new Action(() => _processingMediaEnded = false), DispatcherPriority.Background);
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
            // If playback is stopped (track ended), resume playback.
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
                    Thumbnail = GetAlbumArt(file)  // Synchronous load is acceptable here for standalone
                };
                PlaySong(standalone);
            }
        }

        /// <summary>
        /// Offloads heavy operations (such as loading album art) to a background thread.
        /// </summary>
        private async void PlaySong(Song song)
        {
            if (song == null)
                return;

            // Set the media source on the UI thread.
            MusicPlayer.Source = new Uri(song.FilePath, UriKind.Absolute);

            // Offload album art loading to a background thread.
            BitmapImage bmp = await Task.Run(() =>
            {
                try
                {
                    string albumArtPath = System.IO.Path.ChangeExtension(song.FilePath, ".jpg");
                    if (System.IO.File.Exists(albumArtPath))
                    {
                        BitmapImage image = new BitmapImage();
                        image.BeginInit();
                        image.UriSource = new Uri(albumArtPath, UriKind.Absolute);
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.EndInit();
                        image.Freeze();
                        return image;
                    }
                    else
                    {
                        var tagFile = TagLib.File.Create(song.FilePath);
                        if (tagFile.Tag.Pictures.Length > 0)
                        {
                            var picData = tagFile.Tag.Pictures[0].Data.Data;
                            using (var ms = new MemoryStream(picData))
                            {
                                BitmapImage image = new BitmapImage();
                                image.BeginInit();
                                image.CacheOption = BitmapCacheOption.OnLoad;
                                image.StreamSource = ms;
                                image.EndInit();
                                image.Freeze();
                                return image;
                            }
                        }
                    }
                }
                catch { }
                // Fallback image if nothing found.
                BitmapImage fallback = new BitmapImage();
                fallback.BeginInit();
                fallback.UriSource = new Uri("C:/Users/iwen2/source/repos/Echo Orbit/Echo Orbit/defaultAudioImage.jpg", UriKind.Absolute);
                fallback.CacheOption = BitmapCacheOption.OnLoad;
                fallback.EndInit();
                fallback.Freeze();
                return fallback;
            });

            // Update the UI with the loaded album art.
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
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(albumArtPath, UriKind.Absolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                return image;
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
                            BitmapImage image = new BitmapImage();
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.StreamSource = ms;
                            image.EndInit();
                            image.Freeze();
                            return image;
                        }
                    }
                }
                catch { }
            }
            BitmapImage fallback = new BitmapImage();
            fallback.BeginInit();
            fallback.UriSource = new Uri("C:/Users/iwen2/source/repos/Echo Orbit/Echo Orbit/defaultAudioImage.jpg", UriKind.Absolute);
            fallback.CacheOption = BitmapCacheOption.OnLoad;
            fallback.EndInit();
            fallback.Freeze();
            return fallback;
        }
    }


}
