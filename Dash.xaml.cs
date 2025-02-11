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
using EchoOrbit.Models;
using EchoOrbit.Helpers;
using System.Windows.Controls;
using System.Windows.Media.Animation;

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

        public Dash()
        {
            InitializeComponent();

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
                if (item == "Item2")
                {
                    // When "Item2" is clicked, display the PlaylistControl.
                    MainContent.Content = new EchoOrbit.Controls.PlaylistControl();
                }
                else
                {
                    // Default content for other items.
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
            MessageBox.Show("Previous track clicked (not implemented).");
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Next track clicked (not implemented).");
        }

        private void FutureButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Future feature clicked (not implemented).");
        }
    }
}
