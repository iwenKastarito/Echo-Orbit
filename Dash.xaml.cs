using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Windows.Media.Imaging;
using EchoOrbit.Models;
using EchoOrbit.Helpers;
using System.Windows.Controls.Primitives;

namespace EchoOrbit
{
    public partial class Dash : Window
    {
        private bool isDrawerOpen = false;
        private List<object> imageAttachments = new List<object>();
        private List<string> audioAttachments = new List<string>();
        private List<string> zipAttachments = new List<string>();
        private DispatcherTimer musicTimer;
        private bool isPlaying = false;

        // For full‑screen image viewer navigation.
        private List<ImageSource> viewerImages = new List<ImageSource>();
        private int currentViewerIndex = 0;

        // For kinetic ticker of MusicTitle.
        private string fullMusicTitle = "";
        private DispatcherTimer musicTitleTimer;
        private int musicTitleOffset = 0;

        // User data for JSON persistence.
        private UserData userData;
        private const string UserDataFileName = "userdata.json";

        public Dash()
        {
            InitializeComponent();

            musicTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            musicTimer.Tick += MusicTimer_Tick;

            musicTitleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.5)
            };
            musicTitleTimer.Tick += MusicTitleTimer_Tick;

            this.Loaded += Dash_Loaded;
            this.Closing += Dash_Closing;
        }

        private void Dash_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUserData();
            // Optionally update UI with userData.
        }

        private void Dash_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveUserData();
        }

        private void LoadUserData()
        {
            if (File.Exists(UserDataFileName))
            {
                try
                {
                    string json = File.ReadAllText(UserDataFileName);
                    userData = JsonSerializer.Deserialize<UserData>(json);
                }
                catch
                {
                    userData = new UserData { UserName = "Default", SomeValue = 0 };
                }
            }
            else
            {
                userData = new UserData { UserName = "Default", SomeValue = 0 };
            }
        }

        private void SaveUserData()
        {
            try
            {
                string json = JsonSerializer.Serialize(userData);
                File.WriteAllText(UserDataFileName, json);
            }
            catch
            {
                // Handle errors if needed.
            }
        }

        private void MusicTimer_Tick(object sender, EventArgs e)
        {
            if (MusicPlayer.NaturalDuration.HasTimeSpan)
            {
                MusicProgressSlider.Value = MusicPlayer.Position.TotalSeconds;
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

        private void MusicPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (MusicPlayer.NaturalDuration.HasTimeSpan)
            {
                MusicProgressSlider.Maximum = MusicPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                musicTimer.Start();
                UpdateTimeLabels();
            }
        }

        private void MusicPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            musicTimer.Stop();
            PlayPauseButton.Content = "▶";
            isPlaying = false;
        }

        private void MusicProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MusicPlayer.NaturalDuration.HasTimeSpan)
            {
                MusicPlayer.Position = TimeSpan.FromSeconds(MusicProgressSlider.Value);
                UpdateTimeLabels();
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
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

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Previous track clicked (not implemented).");
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Next track clicked (not implemented).");
        }

        private void FutureButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Future feature (not implemented).");
        }

        private void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Audio Files|*.mp3;*.wav;*.wma"
            };
            if (ofd.ShowDialog() == true)
            {
                string fileName = System.IO.Path.GetFileName(ofd.FileName);
                MusicPlayer.Source = new Uri(ofd.FileName);
                fullMusicTitle = fileName;
                MusicTitle.Text = fileName;
                ApplyKineticAnimationToMusicTitle();
                MusicPlayer.Play();
                PlayPauseButton.Content = "⏸";
                isPlaying = true;
                string albumArtPath = System.IO.Path.ChangeExtension(ofd.FileName, ".jpg");
                if (System.IO.File.Exists(albumArtPath))
                {
                    AudioThumbnailImage.Source = new BitmapImage(new Uri(albumArtPath));
                }
                else
                {
                    AudioThumbnailImage.Source = new BitmapImage(new Uri("defaultAudioImage.png", UriKind.Relative));
                }
            }
        }

        // Allow dragging from anywhere outside the BottomBar.
        private void OuterBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsDescendant(BottomBar, e.OriginalSource as DependencyObject))
            {
                try { DragMove(); } catch { }
            }
        }

        private bool IsDescendant(DependencyObject parent, DependencyObject child)
        {
            if (parent == null || child == null)
                return false;
            DependencyObject current = child;
            while (current != null)
            {
                if (current == parent)
                    return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }




        // When files enter the chat area, show the overlay.
        // Called when dragged files enter the chat area.
        // Called when dragged files enter the chat area.
        private void ChatArea_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                DropOverlay.Visibility = Visibility.Visible;
            }
            e.Handled = true;
        }

        // Called when files are dragged over the chat area.
        private void ChatArea_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        // Called when the dragged files leave the chat area.
        private void ChatArea_DragLeave(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        // Called when files are dropped onto the chat area.
        private void ChatArea_Drop(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
                ProcessAttachedFiles(droppedFiles);
            }
            e.Handled = true;
        }

        // Example helper method to process attached files.
        private void ProcessAttachedFiles(string[] fileNames)
        {
            foreach (string selectedFile in fileNames)
            {
                string ext = System.IO.Path.GetExtension(selectedFile).ToLower();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                {
                    imageAttachments.Add(selectedFile);
                }
                else if (ext == ".mp3" || ext == ".wav" || ext == ".wma")
                {
                    audioAttachments.Add(selectedFile);
                }
                else if (ext == ".zip")
                {
                    zipAttachments.Add(selectedFile);
                }
            }
            UpdateAttachmentsUI();
        }






        private void SlideButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isDrawerOpen)
            {
                Storyboard slideIn = (Storyboard)FindResource("SlideIn");
                slideIn.Begin();
                BeeHiveBackground.IsBeehiveActive = true; // Reactivate when open.
            }
            else
            {
                Storyboard slideOut = (Storyboard)FindResource("SlideOut");
                slideOut.Begin();
                BeeHiveBackground.IsBeehiveActive = false; // Pause updates when closed.
            }
            isDrawerOpen = !isDrawerOpen;
        }


        private void AttachFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp|Audio Files|*.mp3;*.wav;*.wma|Zip Files|*.zip"
            };
            if (ofd.ShowDialog() == true)
            {
                foreach (string selectedFile in ofd.FileNames)
                {
                    string ext = System.IO.Path.GetExtension(selectedFile).ToLower();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                    {
                        imageAttachments.Add(selectedFile);
                    }
                    else if (ext == ".mp3" || ext == ".wav" || ext == ".wma")
                    {
                        audioAttachments.Add(selectedFile);
                    }
                    else if (ext == ".zip")
                    {
                        zipAttachments.Add(selectedFile);
                    }
                }
                UpdateAttachmentsUI();
            }
        }

        private void UpdateAttachmentsUI()
        {
            ImageAttachmentIndicator.Visibility = (imageAttachments.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
            ImageAttachmentCount.Text = imageAttachments.Count.ToString();

            AudioAttachmentIndicator.Visibility = (audioAttachments.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
            AudioAttachmentCount.Text = audioAttachments.Count.ToString();

            ZipAttachmentIndicator.Visibility = (zipAttachments.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
            ZipAttachmentCount.Text = zipAttachments.Count.ToString();

            AttachmentsSummaryPanel.Visibility = (imageAttachments.Count > 0 || audioAttachments.Count > 0 || zipAttachments.Count > 0)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string messageText = MessageTextBox.Text;
            StackPanel messageOuterPanel = new StackPanel { Margin = new Thickness(5) };

            // Audio attachments.
            if (audioAttachments.Count > 0)
            {
                StackPanel audioOuterPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(5) };
                foreach (var audio in audioAttachments)
                {
                    StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
                    Button audioButton = new Button { Content = "♫", Margin = new Thickness(2), Tag = audio };
                    audioButton.Click += (s, args) =>
                    {
                        string file = (s as Button).Tag as string;
                        MusicPlayer.Source = new Uri(file);
                        string fileName = System.IO.Path.GetFileName(file);
                        fullMusicTitle = fileName;
                        MusicTitle.Text = fileName;
                        ApplyKineticAnimationToMusicTitle();
                        MusicPlayer.Play();
                        PlayPauseButton.Content = "⏸";
                        isPlaying = true;
                        string albumArtPath = System.IO.Path.ChangeExtension(file, ".jpg");
                        if (System.IO.File.Exists(albumArtPath))
                        {
                            AudioThumbnailImage.Source = new BitmapImage(new Uri(albumArtPath));
                        }
                        else
                        {
                            AudioThumbnailImage.Source = new BitmapImage(new Uri("defaultAudioImage.png", UriKind.Relative));
                        }
                    };
                    TextBlock audioName = new TextBlock
                    {
                        Text = System.IO.Path.GetFileName(audio),
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(5, 0, 0, 0)
                    };
                    sp.Children.Add(audioButton);
                    sp.Children.Add(audioName);
                    audioOuterPanel.Children.Add(sp);
                }
                messageOuterPanel.Children.Add(audioOuterPanel);
            }

            // Zip attachments.
            if (zipAttachments.Count > 0)
            {
                StackPanel zipOuterPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(5) };
                foreach (var zip in zipAttachments)
                {
                    StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
                    Button zipButton = new Button { Content = "◘", Margin = new Thickness(2), Tag = zip };
                    zipButton.Click += (s, args) =>
                    {
                        MessageBox.Show("Zip file: " + (s as Button).Tag.ToString());
                    };
                    TextBlock zipName = new TextBlock
                    {
                        Text = System.IO.Path.GetFileName(zip),
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(5, 0, 0, 0)
                    };
                    sp.Children.Add(zipButton);
                    sp.Children.Add(zipName);
                    zipOuterPanel.Children.Add(sp);
                }
                messageOuterPanel.Children.Add(zipOuterPanel);
            }

            // Image attachments.
            if (imageAttachments.Count > 0)
            {
                int picturesPerGroup = 8;
                int groupCount = (int)Math.Ceiling((double)imageAttachments.Count / picturesPerGroup);
                for (int group = 0; group < groupCount; group++)
                {
                    int startIndex = group * picturesPerGroup;
                    int count = Math.Min(picturesPerGroup, imageAttachments.Count - startIndex);

                    Border imageBubble = new Border
                    {
                        Background = Brushes.DarkGray,
                        Margin = new Thickness(5),
                        Padding = new Thickness(0),
                        CornerRadius = new CornerRadius(10)
                    };

                    UniformGrid grid = new UniformGrid
                    {
                        Columns = (count <= 4) ? count : 4,
                        Rows = (int)Math.Ceiling((double)count / ((count <= 4) ? count : 4)),
                        Margin = new Thickness(0)
                    };

                    for (int i = 0; i < count; i++)
                    {
                        var imgObj = imageAttachments[startIndex + i];
                        Image imageControl = new Image
                        {
                            Stretch = Stretch.UniformToFill,
                            Cursor = Cursors.Hand,
                            Margin = new Thickness(0)
                        };

                        if (imgObj is string filePath)
                        {
                            try { imageControl.Source = new BitmapImage(new Uri(filePath)); } catch { }
                        }
                        else if (imgObj is BitmapSource bmp)
                        {
                            imageControl.Source = bmp;
                        }

                        imageControl.MouseLeftButtonUp += (s, args) =>
                        {
                            List<ImageSource> sources = new List<ImageSource>();
                            foreach (var child in grid.Children)
                            {
                                if (child is Image img && img.Source != null)
                                    sources.Add(img.Source);
                            }
                            int selectedIndex = 0;
                            for (int j = 0; j < grid.Children.Count; j++)
                            {
                                if (grid.Children[j] is Image img && img == s as Image)
                                {
                                    selectedIndex = j;
                                    break;
                                }
                            }
                            // Use the helper to show full‑screen image viewer.
                            FullScreenImageViewer.Show(sources, selectedIndex);
                        };

                        grid.Children.Add(imageControl);
                    }

                    imageBubble.Child = grid;
                    messageOuterPanel.Children.Add(imageBubble);
                }
            }

            // Text message.
            if (!string.IsNullOrWhiteSpace(messageText))
            {
                Border messageBubble = new Border
                {
                    Background = Brushes.White,
                    Margin = new Thickness(5),
                    Padding = new Thickness(5),
                    CornerRadius = new CornerRadius(5)
                };
                TextBlock textBlock = new TextBlock
                {
                    Text = messageText,
                    Foreground = Brushes.Black,
                    Margin = new Thickness(5),
                    TextWrapping = TextWrapping.Wrap
                };
                messageBubble.Child = textBlock;
                messageOuterPanel.Children.Add(messageBubble);
            }

            if (messageOuterPanel.Children.Count > 0)
            {
                MessagesContainer.Children.Add(messageOuterPanel);
            }
            MessageTextBox.Clear();
            imageAttachments.Clear();
            audioAttachments.Clear();
            zipAttachments.Clear();
            UpdateAttachmentsUI();
        }

        private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (Clipboard.ContainsImage())
                {
                    var img = Clipboard.GetImage();
                    if (img != null)
                    {
                        imageAttachments.Add(img);
                        UpdateAttachmentsUI();
                        e.Handled = true;
                        return;
                    }
                }
            }
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
            {
                int caretIndex = MessageTextBox.CaretIndex;
                MessageTextBox.Text = MessageTextBox.Text.Insert(caretIndex, Environment.NewLine);
                MessageTextBox.CaretIndex = caretIndex + Environment.NewLine.Length;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                SendButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string item = btn.Content.ToString();
                MainContent.Content = new TextBlock
                {
                    Text = "You selected " + item,
                    Foreground = Brushes.White,
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
        }

        // Kinetic ticker for MusicTitle.
        private void ApplyKineticAnimationToMusicTitle()
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

        private void BottomBar_MouseEnter(object sender, MouseEventArgs e)
        {
            Storyboard sb = (Storyboard)FindResource("BottomBarShow");
            sb.Begin();
        }

        private void BottomBar_MouseLeave(object sender, MouseEventArgs e)
        {
            Storyboard sb = (Storyboard)FindResource("BottomBarHide");
            sb.Begin();
        }
    }
}
