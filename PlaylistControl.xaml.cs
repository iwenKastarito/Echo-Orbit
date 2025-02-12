using System;
using System.Collections.ObjectModel;
using System.IO; // Fully qualify System.IO.File
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TagLib;  // Ensure TagLib# is installed via NuGet

namespace EchoOrbit.Controls
{
    public partial class PlaylistControl : UserControl
    {
        public ObservableCollection<Song> CurrentPlaylist { get; set; } = new ObservableCollection<Song>();
        public ObservableCollection<Playlist> ExistingPlaylists { get; set; } = new ObservableCollection<Playlist>();

        // Event raised when a song is selected to play.
        public event Action<string> SongSelected;

        public PlaylistControl()
        {
            InitializeComponent();

            // For demo, add a default playlist.
            var defaultPlaylist = new Playlist
            {
                Name = "Favorites",
                Thumbnail = new BitmapImage(new Uri("C:/Users/iwen2/source/repos/Echo Orbit/Echo Orbit/defaultAudioImage.jpg", UriKind.Absolute))
            };
            defaultPlaylist.Songs.Add(new Song
            {
                FilePath = "dummy.mp3",
                Title = "Sample Song",
                Thumbnail = new BitmapImage(new Uri("C:/Users/iwen2/source/repos/Echo Orbit/Echo Orbit/defaultAudioImage.jpg", UriKind.Absolute))
            });
            ExistingPlaylists.Add(defaultPlaylist);
            CurrentPlaylist = defaultPlaylist.Songs;
            SongsListBox.ItemsSource = CurrentPlaylist;
            DataContext = this;

            // Attach mouse wheel event handlers for horizontal scrolling.
            var playlistsSV = FindVisualChild<ScrollViewer>(PlaylistsItemsControl);
            if (playlistsSV != null)
                playlistsSV.PreviewMouseWheel += (s, e) =>
                {
                    playlistsSV.ScrollToHorizontalOffset(playlistsSV.HorizontalOffset - e.Delta);
                    e.Handled = true;
                };

            var songsSV = FindVisualChild<ScrollViewer>(SongsListBox);
            if (songsSV != null)
                songsSV.PreviewMouseWheel += (s, e) =>
                {
                    songsSV.ScrollToHorizontalOffset(songsSV.HorizontalOffset - e.Delta);
                    e.Handled = true;
                };
        }

        // Generic helper to find a visual child.
        private static T FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                    return t;
                T childItem = FindVisualChild<T>(child);
                if (childItem != null)
                    return childItem;
            }
            return null;
        }

        private void NewPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            var newPlaylist = new Playlist
            {
                Name = "New Playlist",
                Thumbnail = new BitmapImage(new Uri("C:/Users/iwen2/source/repos/Echo Orbit/Echo Orbit/defaultAudioImage.jpg", UriKind.Absolute))
            };
            ExistingPlaylists.Add(newPlaylist);
            CurrentPlaylist = newPlaylist.Songs;
            SongsListBox.ItemsSource = CurrentPlaylist;
            MessageBox.Show("New playlist created.");
        }

        private void AddSongButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Audio Files|*.mp3;*.wav;*.wma",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                foreach (string file in dlg.FileNames)
                {
                    Song song = new Song
                    {
                        FilePath = file,
                        Title = System.IO.Path.GetFileName(file),
                        Thumbnail = GetAlbumArt(file)
                    };
                    CurrentPlaylist.Add(song);
                }
            }
        }

        private void RemoveSongButton_Click(object sender, RoutedEventArgs e)
        {
            if (SongsListBox.SelectedItem is Song song)
            {
                CurrentPlaylist.Remove(song);
            }
        }

        private void ShareSongButton_Click(object sender, RoutedEventArgs e)
        {
            if (SongsListBox.SelectedItem is Song song)
            {
                Clipboard.SetText(song.FilePath);
                MessageBox.Show("Song path copied to clipboard.");
            }
        }

        private void SongPlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filePath)
            {
                SongSelected?.Invoke(filePath);
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

        private void PlaylistItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Playlist selectedPlaylist)
            {
                CurrentPlaylist = selectedPlaylist.Songs;
                SongsListBox.ItemsSource = CurrentPlaylist;
            }
        }

        // Prevent auto-scrolling when an item is selected.
        private void SongsListBox_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }
    }

    public class Song
    {
        public string FilePath { get; set; }
        public string Title { get; set; }
        public ImageSource Thumbnail { get; set; }
    }

    public class Playlist
    {
        public string Name { get; set; }
        public ImageSource Thumbnail { get; set; }
        public ObservableCollection<Song> Songs { get; set; } = new ObservableCollection<Song>();
    }
}
