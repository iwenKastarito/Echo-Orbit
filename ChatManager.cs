using EchoOrbit.Helpers;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace EchoOrbit
{
    public class ChatManager
    {
        private StackPanel messagesContainer;
        private MusicController musicController; // Reference to MusicController

        public ChatManager(StackPanel container, MusicController musicCtrl)
        {
            messagesContainer = container;
            musicController = musicCtrl;
        }

        public void SendMessage(string messageText, List<object> imageAttachments, List<string> audioAttachments, List<string> zipAttachments)
        {
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
                        PlayAudio(file);
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
                int groupCount = (int)System.Math.Ceiling((double)imageAttachments.Count / picturesPerGroup);
                for (int group = 0; group < groupCount; group++)
                {
                    int startIndex = group * picturesPerGroup;
                    int count = System.Math.Min(picturesPerGroup, imageAttachments.Count - startIndex);

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
                        Rows = (int)System.Math.Ceiling((double)count / ((count <= 4) ? count : 4)),
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
                            try { imageControl.Source = new System.Windows.Media.Imaging.BitmapImage(new System.Uri(filePath)); } catch { }
                        }
                        else if (imgObj is ImageSource bmp)
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
                messagesContainer.Children.Add(messageOuterPanel);
            }
        }

        private void PlayAudio(string file)
        {
            musicController.PlayMusicFromFile(file);
        }
    }
}
