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
//using EchoOrbit.Models;
using EchoOrbit.Helpers;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using LocalMusicStreamingSecurity;
using EchoOrbit.Controls;
using LocalNetworkTest;

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

        private SecurityManager securityManager;
        private ConnectionsControl connectionsControl;

        private SimplePeerDiscovery peerDiscovery;

        public Dash()
        {
            InitializeComponent();

            // Initialize SecurityManager (starts discovery, key exchange, etc.)
            securityManager = new SecurityManager();
            // Optionally, send discovery request on startup:
            securityManager.SendDiscoveryRequest();
            peerDiscovery = new SimplePeerDiscovery();
            peerDiscovery.Start();
            // Initialize ConnectionsControl and subscribe to its event.
            connectionsControl = new ConnectionsControl();
            connectionsControl.OnlineUserChatRequested += ConnectionsControl_OnlineUserChatRequested;

 


            musicController = new MusicController(MusicPlayer, MusicProgressSlider, ElapsedTimeText, RemainingTimeText, PlayPauseButton, MusicTitle, AudioThumbnailImage);
            attachmentManager = new AttachmentManager(AttachmentsSummaryPanel, ImageAttachmentIndicator, ImageAttachmentCount, AudioAttachmentIndicator, AudioAttachmentCount, ZipAttachmentIndicator, ZipAttachmentCount);
            chatManager = new ChatManager(MessagesContainer, musicController);

            this.Loaded += Dash_Loaded;
            this.Closing += Dash_Closing;
        }

        private void Dash_Loaded(object sender, RoutedEventArgs e)
        {
            userData = UserDataManager.LoadUserData();
        }

        private void Dash_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UserDataManager.SaveUserData(userData);
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
            chatManager.SendMessage(messageText, attachmentManager.ImageAttachments, attachmentManager.AudioAttachments, attachmentManager.ZipAttachments);
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
                    // Load the PlaylistControl (existing code).
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
                    //MainContent.Content = new EchoOrbit.Controls.ConnectionsControl();
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

        private void ConnectionsControl_OnlineUserChatRequested(Controls.OnlineUser user)
        {
            // Here you can perform the necessary secure steps:
            // 1. (Optional) Send a discovery request if not done.
            // 2. The SecurityManager should have already discovered the peer and stored its public key.
            // 3. Use the peer's public key to establish a shared secret.
            // For this example, assume that our SecurityManager has already done key exchange via its ListenForDiscoveryMessages().
            // (You could check if securityManager already has a shared secret with the peer by looking up the peer's endpoint.)
            // 4. Open the chat drawer (if not already open) and load a chat interface targeting that peer.
            // For demonstration, we simply set the chat drawer's visibility and display a welcome message.
            OpenChatWithPeer(user);
        }

        private void OpenChatWithPeer(Controls.OnlineUser user)
        {
            // (You may want to verify that securityManager has a shared secret for this peer.
            //  For example, you could have a dictionary mapping endpoints to shared secrets.)
            // For now, we assume that the SecurityManager has already exchanged keys with this peer.
            // Then, open the chat drawer (if it's hidden) and set up the chat.
            ChatDrawer.Visibility = Visibility.Visible;
            // Optionally, update a label in the chat drawer with the peer's name.
            // And store the peer's information for sending secure messages.
            // For demonstration, we simply write a message into the chat area.
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
