using System;
using System.Collections.ObjectModel;
using System.IO; // Fully qualify System.IO.File
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Windows.Threading;
using TagLib;
using Microsoft.VisualBasic; // For InputBox

using EchoOrbit.Models;


namespace EchoOrbit.Controls
{
    public partial class PlaylistControl : UserControl
    {
        public ObservableCollection<Song> CurrentPlaylist { get; set; } = new ObservableCollection<Song>();
        public ObservableCollection<Playlist> ExistingPlaylists { get; set; } = new ObservableCollection<Playlist>();

        // Event raised when a song is selected to play.
        public event Action<string> SongSelected;

        private UserData userData;
        private string userMusicFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserMusic");

        public PlaylistControl()
        {
            InitializeComponent();
            if (!Directory.Exists(userMusicFolder))
                Directory.CreateDirectory(userMusicFolder);

            userData = UserDataManager.LoadUserData();
            if (userData.Playlists.Count == 0)
            {
                var defaultStored = new StoredPlaylist { Name = "Favorites" };
                userData.Playlists.Add(defaultStored);
                UserDataManager.SaveUserData(userData);
            }
            foreach (var sp in userData.Playlists)
            {
                var pl = new Playlist
                {
                    Name = sp.Name,
                    // Make sure the resource name and extension match exactly.
                    Thumbnail = new BitmapImage(new Uri("pack://application:,,,/defaultAudioImage.jpg", UriKind.Absolute))
                };
                foreach (var ss in sp.Songs)
                {
                    pl.Songs.Add(new Song { FilePath = ss.FilePath, Title = ss.Title, Thumbnail = GetAlbumArt(ss.FilePath) });
                }
                ExistingPlaylists.Add(pl);
            }
            if (ExistingPlaylists.Count > 0)
                CurrentPlaylist = ExistingPlaylists[0].Songs;
            SongsListBox.ItemsSource = CurrentPlaylist;
            DataContext = this;

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
            string name = Interaction.InputBox("Enter playlist name:", "New Playlist", "New Playlist");
            if (string.IsNullOrWhiteSpace(name))
                name = "New Playlist";

            var newStored = new StoredPlaylist { Name = name };
            userData.Playlists.Add(newStored);
            UserDataManager.SaveUserData(userData);

            var newPlaylist = new Playlist
            {
                Name = name,
                Thumbnail = new BitmapImage(new Uri("pack://application:,,,/defaultAudioImage.jpg", UriKind.Absolute))
            };
            ExistingPlaylists.Add(newPlaylist);
            CurrentPlaylist = newPlaylist.Songs;
            SongsListBox.ItemsSource = CurrentPlaylist;
        }

        private void AddSongButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "Audio Files|*.mp3;*.wav;*.wma", Multiselect = true };
            if (dlg.ShowDialog() == true)
            {
                var storedPlaylist = GetStoredPlaylist(CurrentPlaylist);
                foreach (string file in dlg.FileNames)
                {
                    string destFile = Path.Combine(userMusicFolder, Path.GetFileName(file));
                    try { System.IO.File.Copy(file, destFile, true); } catch { }
                    Song song = new Song
                    {
                        FilePath = destFile,
                        Title = Path.GetFileName(destFile),
                        Thumbnail = GetAlbumArt(destFile)
                    };
                    CurrentPlaylist.Add(song);
                    storedPlaylist.Songs.Add(new StoredSong { FilePath = destFile, Title = song.Title });
                }
                UserDataManager.SaveUserData(userData);
            }
        }

        private void RemoveSongButton_Click(object sender, RoutedEventArgs e)
        {
            if (SongsListBox.SelectedItem is Song song)
            {
                CurrentPlaylist.Remove(song);
                var storedPlaylist = GetStoredPlaylist(CurrentPlaylist);
                var storedSong = storedPlaylist.Songs.Find(s => s.FilePath == song.FilePath);
                if (storedSong != null)
                    storedPlaylist.Songs.Remove(storedSong);
                UserDataManager.SaveUserData(userData);
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
                            bmp.DecodePixelWidth = 120; // Load a scaled version.
                            bmp.StreamSource = ms;
                            bmp.EndInit();
                            bmp.Freeze();
                            return bmp;
                        }
                    }
                }
                catch { }
            }
            return new BitmapImage(new Uri("pack://application:,,,/defaultAudioImage.jpg", UriKind.Absolute));
        }

        private void PlaylistItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Playlist selectedPlaylist)
            {
                CurrentPlaylist = selectedPlaylist.Songs;
                SongsListBox.ItemsSource = CurrentPlaylist;
            }
        }

        private void SongsListBox_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }

        private StoredPlaylist GetStoredPlaylist(ObservableCollection<Song> runtimeSongs)
        {
            foreach (var sp in userData.Playlists)
            {
                if (sp.Songs.Count == runtimeSongs.Count)
                    return sp;
            }
            return userData.Playlists[0];
        }
    }


}
