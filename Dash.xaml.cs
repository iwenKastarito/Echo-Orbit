using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Data;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.Win32;

namespace AlgebraSQLizer
{
    // Converter to compute the width of the progress fill: (Value / Maximum) * ActualWidth
    public class SliderProgressWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 3 &&
                values[0] is double value &&
                values[1] is double maximum &&
                values[2] is double actualWidth)
            {
                if (maximum <= 0)
                    return 0.0;
                return (value / maximum) * actualWidth;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BottomBarMarginConverter : IMultiValueConverter
    {
        private const double SlideButtonWidth = 30;
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 &&
                values[0] is double drawerWidth &&
                values[1] is double transformX)
            {
                double visibleWidth = drawerWidth - transformX - SlideButtonWidth;
                if (visibleWidth < 0) visibleWidth = 0;
                return new Thickness(150, 0, visibleWidth, 0);
            }
            return new Thickness(150, 0, 0, 0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RelationalAlgebraVisitor : TSqlFragmentVisitor
    {
        // (Your existing visitor implementation remains unchanged.)
    }

    public partial class Dash : Window
    {
        private bool isDrawerOpen = false;
        private List<object> imageAttachments = new List<object>();
        private List<string> audioAttachments = new List<string>();
        private List<string> zipAttachments = new List<string>();
        private DispatcherTimer musicTimer;
        private bool isPlaying = false;

        public Dash()
        {
            InitializeComponent();
            musicTimer = new DispatcherTimer();
            musicTimer.Interval = TimeSpan.FromSeconds(1);
            musicTimer.Tick += MusicTimer_Tick;
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
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Audio Files|*.mp3;*.wav;*.wma";
            if (ofd.ShowDialog() == true)
            {
                MusicPlayer.Source = new Uri(ofd.FileName);
                MusicTitle.Text = System.IO.Path.GetFileName(ofd.FileName);
                MusicPlayer.Play();
                PlayPauseButton.Content = "⏸";
                isPlaying = true;
            }
        }

        private void SlideButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isDrawerOpen)
            {
                Storyboard slideIn = (Storyboard)FindResource("SlideIn");
                slideIn.Begin();
            }
            else
            {
                Storyboard slideOut = (Storyboard)FindResource("SlideOut");
                slideOut.Begin();
            }
            isDrawerOpen = !isDrawerOpen;
        }

        private void AttachFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp|Audio Files|*.mp3;*.wav;*.wma|Zip Files|*.zip";
            if (ofd.ShowDialog() == true)
            {
                string selectedFile = ofd.FileName;
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
                UpdateAttachmentsUI();
            }
        }

        private void UpdateAttachmentsUI()
        {
            if (imageAttachments.Count > 0)
            {
                ImageAttachmentIndicator.Visibility = Visibility.Visible;
                ImageAttachmentCount.Text = imageAttachments.Count.ToString();
            }
            else
            {
                ImageAttachmentIndicator.Visibility = Visibility.Collapsed;
            }
            if (audioAttachments.Count > 0)
            {
                AudioAttachmentIndicator.Visibility = Visibility.Visible;
                AudioAttachmentCount.Text = audioAttachments.Count.ToString();
            }
            else
            {
                AudioAttachmentIndicator.Visibility = Visibility.Collapsed;
            }
            if (zipAttachments.Count > 0)
            {
                ZipAttachmentIndicator.Visibility = Visibility.Visible;
                ZipAttachmentCount.Text = zipAttachments.Count.ToString();
            }
            else
            {
                ZipAttachmentIndicator.Visibility = Visibility.Collapsed;
            }
            AttachmentsSummaryPanel.Visibility = (imageAttachments.Count > 0 || audioAttachments.Count > 0 || zipAttachments.Count > 0)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string messageText = MessageTextBox.Text;
            StackPanel messageOuterPanel = new StackPanel { Margin = new Thickness(5) };

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
                        MusicTitle.Text = System.IO.Path.GetFileName(file);
                        MusicPlayer.Play();
                        PlayPauseButton.Content = "⏸";
                        isPlaying = true;
                    };
                    TextBlock audioName = new TextBlock { Text = System.IO.Path.GetFileName(audio), Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
                    sp.Children.Add(audioButton);
                    sp.Children.Add(audioName);
                    audioOuterPanel.Children.Add(sp);
                }
                messageOuterPanel.Children.Add(audioOuterPanel);
            }
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
                    TextBlock zipName = new TextBlock { Text = System.IO.Path.GetFileName(zip), Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
                    sp.Children.Add(zipButton);
                    sp.Children.Add(zipName);
                    zipOuterPanel.Children.Add(sp);
                }
                messageOuterPanel.Children.Add(zipOuterPanel);
            }
            if (!string.IsNullOrWhiteSpace(messageText) || imageAttachments.Count > 0)
            {
                Border messageBubble = new Border { Background = Brushes.DarkGray, Margin = new Thickness(5), Padding = new Thickness(5) };
                StackPanel innerStack = new StackPanel();
                if (imageAttachments.Count > 0)
                {
                    WrapPanel imagesPanel = new WrapPanel { Margin = new Thickness(5) };
                    foreach (var img in imageAttachments)
                    {
                        Image imageControl = new Image { Width = 100, Height = 100, Margin = new Thickness(5) };
                        if (img is string filePath)
                        {
                            try { imageControl.Source = new BitmapImage(new Uri(filePath)); } catch { }
                        }
                        else if (img is BitmapSource bmp)
                        {
                            imageControl.Source = bmp;
                        }
                        imageControl.Cursor = Cursors.Hand;
                        imageControl.MouseLeftButtonUp += (s, args) =>
                        {
                            ShowEnlargedImage(imageControl.Source);
                        };
                        imagesPanel.Children.Add(imageControl);
                    }
                    innerStack.Children.Add(imagesPanel);
                }
                if (!string.IsNullOrWhiteSpace(messageText))
                {
                    TextBlock textBlock = new TextBlock { Text = messageText, Foreground = Brushes.White, Margin = new Thickness(5), TextWrapping = TextWrapping.Wrap };
                    innerStack.Children.Add(textBlock);
                }
                messageBubble.Child = innerStack;
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

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch (InvalidOperationException) { }
            }
        }

        // When the mouse enters the bottom bar container, show the controls panel.
        private void BottomBar_MouseEnter(object sender, MouseEventArgs e)
        {
            Storyboard sb = (Storyboard)FindResource("BottomBarShow");
            sb.Begin();
        }

        // When the mouse leaves the bottom bar container, hide the controls panel.
        private void BottomBar_MouseLeave(object sender, MouseEventArgs e)
        {
            Storyboard sb = (Storyboard)FindResource("BottomBarHide");
            sb.Begin();
        }

        // Opens a full-screen overlay (covering the entire screen) to display an enlarged image.
        private void ShowEnlargedImage(ImageSource source)
        {
            Window overlay = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                WindowState = WindowState.Maximized,
                Topmost = true,
                ShowInTaskbar = false
            };
            Grid grid = new Grid();
            Image enlargedImage = new Image
            {
                Source = source,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = SystemParameters.PrimaryScreenWidth / 2,
                MaxHeight = SystemParameters.PrimaryScreenHeight / 2
            };
            grid.Children.Add(enlargedImage);
            overlay.Content = grid;
            overlay.MouseLeftButtonDown += (s, e) =>
            {
                overlay.Close();
            };
            overlay.ShowDialog();
        }
    }
}
