using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using EchoOrbit.Helpers;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using EchoOrbit.Controls;
using LocalNetworkTest;
using LocalChatSecurity; // Contains SecurityManager
using System.Net;



namespace EchoOrbit
{
    public partial class Dash : Window
    {
        private bool isDrawerOpen = false;
        private UserData userData;

        // Controllers / Managers.
        private MusicController musicController;
        private AttachmentManager attachmentManager;
        private ChatManager chatManager;

        private ConnectionsControl connectionsControl;

        // Security and Discovery Managers.
        private SecurityManager securityManager;
        private SimplePeerDiscovery peerDiscovery;

        public Dash()
        {
            InitializeComponent();

            // Initialize the SecurityManager.
            securityManager = new SecurityManager();

            // Initialize Peer Discovery.
            peerDiscovery = new SimplePeerDiscovery();
            var profile = ProfileDataManager.LoadProfileData();
            if (profile != null)
            {
                // Use the profile display name.
                peerDiscovery.DisplayName = profile.DisplayName;

                // Generate a thumbnail Base64 string for the profile image.
                if (!string.IsNullOrEmpty(profile.ProfilePicturePath) && File.Exists(profile.ProfilePicturePath))
                {
                    // For example, generate a 64x64 thumbnail.
                    peerDiscovery.ProfileImageBase64 = ImageHelper.GetThumbnailBase64(profile.ProfilePicturePath, 64, 64);
                }
                else
                {
                    // If no profile picture, leave it empty (so the receiver will use its default image).
                    peerDiscovery.ProfileImageBase64 = "";
                }
            }
            else
            {
                // Fallback values.
                peerDiscovery.DisplayName = "Echo";
                peerDiscovery.ProfileImageBase64 = "";
            }

            // Start peer discovery.
            peerDiscovery.Start();


            // In Dash.xaml.cs (inside the constructor, after initializing peerDiscovery and connectionsControl)
            peerDiscovery.PeerDiscovered += (endpoint, peerId, displayName, profileImageBase64) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Check if this peer is already in the list.
                    bool exists = false;
                    foreach (var user in connectionsControl.OnlineUsers)
                    {
                        if (user.PeerEndpoint.Address.Equals(endpoint.Address))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                    {
                        System.Windows.Media.Imaging.BitmapImage profileImage;
                        if (!string.IsNullOrEmpty(profileImageBase64))
                        {
                            try
                            {
                                byte[] imageBytes = Convert.FromBase64String(profileImageBase64);
                                using (var ms = new MemoryStream(imageBytes))
                                {
                                    profileImage = new System.Windows.Media.Imaging.BitmapImage();
                                    profileImage.BeginInit();
                                    profileImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                                    profileImage.StreamSource = ms;
                                    profileImage.EndInit();
                                }
                            }
                            catch (Exception ex)
                            {
                                // If conversion fails, fall back to a default image.
                                profileImage = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/defaultProfile.png", UriKind.Absolute));
                            }
                        }
                        else
                        {
                            profileImage = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/defaultProfile.png", UriKind.Absolute));
                        }

                        connectionsControl.OnlineUsers.Add(new EchoOrbit.Controls.OnlineUser
                        {
                            DisplayName = displayName,
                            // Use port 8890 for chat messages.
                            PeerEndpoint = new IPEndPoint(endpoint.Address, 8890),
                            ProfileImage = profileImage
                        });
                    }
                });
            };




            // Initialize ConnectionsControl.
            connectionsControl = new ConnectionsControl();
            // Subscribe to the chat-request event.
            connectionsControl.OnlineUserChatRequested += ConnectionsControl_OnlineUserChatRequested;

            musicController = new MusicController(MusicPlayer, MusicProgressSlider, ElapsedTimeText, RemainingTimeText, PlayPauseButton, MusicTitle, AudioThumbnailImage);
            attachmentManager = new AttachmentManager(AttachmentsSummaryPanel, ImageAttachmentIndicator, ImageAttachmentCount, AudioAttachmentIndicator, AudioAttachmentCount, ZipAttachmentIndicator, ZipAttachmentCount);

            // Pass the main chat container to ChatManager.
            chatManager = new ChatManager(MessagesContainer, musicController);

            this.Loaded += Dash_Loaded;
            this.Closing += Dash_Closing;
        }

        private void Dash_Loaded(object sender, RoutedEventArgs e)
        {
            userData = UserDataManager.LoadUserData();

            // Optionally, trigger additional secure steps here.
            // For example, if you want to perform an immediate key exchange:
            // var (myEphemeralKey, mySignature) = securityManager.StartKeyExchange();
            // Then send these (along with your long-term public key) to discovered peers.
        }

        private void Dash_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UserDataManager.SaveUserData(userData);            // If in the future you add a Stop() method to SimplePeerDiscovery, you can call it here.
            // For now, no such method exists.
        }

        private void ChatArea_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                DropOverlay.Visibility = Visibility.Visible;
            }
            e.Handled = true;
        }

        private void ChatArea_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void ChatArea_DragLeave(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        private void ChatArea_Drop(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in droppedFiles)
                {
                    attachmentManager.AddAttachment(file);
                }
            }
            e.Handled = true;
        }

        private void SlideButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isDrawerOpen)
            {
                Storyboard slideIn = (Storyboard)FindResource("SlideIn");
                slideIn.Begin();
                BeeHiveBackground.IsBeehiveActive = true;
            }
            else
            {
                Storyboard slideOut = (Storyboard)FindResource("SlideOut");
                slideOut.Begin();
                BeeHiveBackground.IsBeehiveActive = false;
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
                foreach (string file in ofd.FileNames)
                {
                    attachmentManager.AddAttachment(file);
                }
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string messageText = MessageTextBox.Text;
            chatManager.SendMessage(
                messageText,
                attachmentManager.ImageAttachments.ConvertAll(item => item.ToString()),
                attachmentManager.AudioAttachments.ConvertAll(item => item.ToString()),
                attachmentManager.ZipAttachments.ConvertAll(item => item.ToString())
            );
            MessageTextBox.Clear();
            attachmentManager.ClearAttachments();
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
                        attachmentManager.ImageAttachments.Add(img);
                        attachmentManager.UpdateAttachmentsUI();
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

        private void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            musicController.OpenFileAndPlay();
        }

        private void MusicProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            musicController.UpdatePosition(MusicProgressSlider.Value);
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            musicController.PlayPause();
        }

        private void MusicPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            musicController.MediaOpened();
        }

        private void MusicPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            musicController.MediaEnded();
        }

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

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string item = btn.Content.ToString();
                if (item == "Item1")
                {
                    // Load the ProfileControl.
                    MainContent.Content = new EchoOrbit.Controls.ProfileControl();
                }
                else if (item == "Item2")
                {
                    // Load the PlaylistControl.
                    var playlistControl = new EchoOrbit.Controls.PlaylistControl();
                    playlistControl.SongSelected += (filePath) =>
                    {
                        musicController.CurrentPlaylist = playlistControl.CurrentPlaylist;
                        for (int i = 0; i < musicController.CurrentPlaylist.Count; i++)
                        {
                            if (musicController.CurrentPlaylist[i].FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                            {
                                musicController.CurrentPlaylistIndex = i;
                                break;
                            }
                        }
                        musicController.PlayMusicFromFile(filePath);
                    };
                    MainContent.Content = playlistControl;
                }
                else if (item == "Item3")
                {
                    MainContent.Content = connectionsControl;
                }
                else
                {
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
        }

        private void ConnectionsControl_OnlineUserChatRequested(OnlineUser user)
        {
            // Create a new ChatSession for the selected peer.
            chatManager.CurrentChatSession = new ChatSession(user);

            // Open the chat drawer and clear previous messages.
            ChatDrawer.Visibility = Visibility.Visible;
            MessagesContainer.Children.Clear();
            MessagesContainer.Children.Add(new TextBlock
            {
                Text = $"Chat started with {user.DisplayName}.",
                Foreground = Brushes.White,
                Margin = new Thickness(5)
            });
        }

        private void OpenChatWithPeer(Controls.OnlineUser user)
        {
            // Assuming the SecurityManager has already established a shared secret with the peer,
            // open the chat drawer and load the secure chat interface.
            ChatDrawer.Visibility = Visibility.Visible;
            MessagesContainer.Children.Clear();
            MessagesContainer.Children.Add(new TextBlock
            {
                Text = $"Chat started with {user.DisplayName}.",
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(5)
            });
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

        // Stub event handlers for audio control buttons.
        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (musicController.CurrentPlaylist != null && musicController.CurrentPlaylist.Count > 0)
            {
                musicController.PrevSong();
            }
            else
            {
                MessageBox.Show("No playlist available for previous track.");
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (musicController.CurrentPlaylist != null && musicController.CurrentPlaylist.Count > 0)
            {
                musicController.NextSong();
            }
            else
            {
                MessageBox.Show("No playlist available for next track.");
            }
        }

        private void FutureButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Future feature clicked (not implemented).");
        }
    }
}
