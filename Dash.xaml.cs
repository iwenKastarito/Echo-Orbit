using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Data;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Media.Effects;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.Win32;

namespace EchoOrbit
{
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
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
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
                if (visibleWidth < 0)
                    visibleWidth = 0;
                return new Thickness(150, 0, visibleWidth, 0);
            }
            return new Thickness(150, 0, 0, 0);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public class OneSixthMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length > 0 && values[0] is double totalWidth)
            {
                double leftMargin = totalWidth / 6;
                return new Thickness(leftMargin, 0, 0, 0);
            }
            return new Thickness(0);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public class RelationalAlgebraVisitor : TSqlFragmentVisitor
    {
        // Implementation not provided.
    }

    public partial class Dash : Window
    {
        private bool isDrawerOpen = false;
        private List<object> imageAttachments = new List<object>();
        private List<string> audioAttachments = new List<string>();
        private List<string> zipAttachments = new List<string>();
        private DispatcherTimer musicTimer;
        private bool isPlaying = false;

        // For full-screen image viewer navigation.
        private List<ImageSource> viewerImages = new List<ImageSource>();
        private int currentViewerIndex = 0;

        // For kinetic ticker of MusicTitle.
        private string fullMusicTitle = "";
        private DispatcherTimer musicTitleTimer;
        private int musicTitleOffset = 0;

        public Dash()
        {
            InitializeComponent();
            musicTimer = new DispatcherTimer();
            musicTimer.Interval = TimeSpan.FromSeconds(1);
            musicTimer.Tick += MusicTimer_Tick;

            musicTitleTimer = new DispatcherTimer();
            musicTitleTimer.Interval = TimeSpan.FromSeconds(0.5);
            musicTitleTimer.Tick += MusicTitleTimer_Tick;
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
                    AudioThumbnailImage.Source = new BitmapImage(new Uri("C:/Users/iwen2/source/repos/Echo Orbit/Echo Orbit/defaultAudioImage.png"));
                }
            }
        }

        // Allow dragging from anywhere outside BottomBar.
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

            // Audio attachments in chat drawer (static text)
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
                            AudioThumbnailImage.Source = new BitmapImage(new Uri("C:/Users/iwen2/source/repos/Echo Orbit/Echo Orbit/defaultAudioImage.png"));
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
                            Image clickedImage = s as Image;
                            WrapPanel panel = imagesPanel;
                            List<ImageSource> sources = new List<ImageSource>();
                            int selectedIndex = 0;
                            for (int i = 0; i < panel.Children.Count; i++)
                            {
                                if (panel.Children[i] is Image imgChild)
                                {
                                    sources.Add(imgChild.Source);
                                    if (imgChild == clickedImage)
                                        selectedIndex = i;
                                }
                            }
                            ShowEnlargedImage(sources, selectedIndex);
                        };
                        imagesPanel.Children.Add(imageControl);
                    }
                    innerStack.Children.Add(imagesPanel);
                }
                if (!string.IsNullOrWhiteSpace(messageText))
                {
                    TextBlock textBlock = new TextBlock
                    {
                        Text = messageText,
                        Foreground = Brushes.White,
                        Margin = new Thickness(5),
                        TextWrapping = TextWrapping.Wrap
                    };
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

        // Full-screen image viewer with centered navigation buttons.
        private void ShowEnlargedImage(List<ImageSource> images, int selectedIndex)
        {
            if (images == null || images.Count == 0)
                return;

            viewerImages = images;
            currentViewerIndex = selectedIndex;

            Window fullScreenViewer = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowState = WindowState.Maximized,
                Topmost = true,
                ShowInTaskbar = false
            };

            Grid rootGrid = new Grid();

            Rectangle backgroundRect = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0))
            };
            backgroundRect.Effect = new BlurEffect { Radius = 10 };
            rootGrid.Children.Add(backgroundRect);

            Border imageContainer = new Border
            {
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Image fullScreenImage = new Image
            {
                Source = viewerImages[currentViewerIndex],
                Stretch = Stretch.Uniform,
                MaxWidth = SystemParameters.PrimaryScreenWidth,
                MaxHeight = SystemParameters.PrimaryScreenHeight
            };
            imageContainer.Child = fullScreenImage;
            rootGrid.Children.Add(imageContainer);

            Grid navGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            navGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            navGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button leftButton = new Button
            {
                Content = "❮",
                Width = 50,
                Height = 50,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 30,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 0, 0, 0)
            };
            Grid.SetColumn(leftButton, 0);
            navGrid.Children.Add(leftButton);

            Button rightButton = new Button
            {
                Content = "❯",
                Width = 50,
                Height = 50,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 30,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 20, 0)
            };
            Grid.SetColumn(rightButton, 1);
            navGrid.Children.Add(rightButton);

            rootGrid.Children.Add(navGrid);

            rootGrid.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == backgroundRect)
                {
                    fullScreenViewer.Close();
                }
            };

            leftButton.Click += (s, e) =>
            {
                currentViewerIndex = (currentViewerIndex - 1 + viewerImages.Count) % viewerImages.Count;
                fullScreenImage.Source = viewerImages[currentViewerIndex];
            };
            rightButton.Click += (s, e) =>
            {
                currentViewerIndex = (currentViewerIndex + 1) % viewerImages.Count;
                fullScreenImage.Source = viewerImages[currentViewerIndex];
            };

            fullScreenViewer.Content = rootGrid;
            fullScreenViewer.ShowDialog();
        }

        // Kinetic ticker for MusicTitle: shows a fixed 17-character substring that advances one character at a time.
        private void ApplyKineticAnimationToMusicTitle()
        {
            if (string.IsNullOrEmpty(fullMusicTitle))
            {
                musicTitleTimer.Stop();
                return;
            }
            // If the full title is 17 or fewer characters, show it and stop the ticker.
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
            // If there is enough room, take substring of 17 characters; if not, wrap around.
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
