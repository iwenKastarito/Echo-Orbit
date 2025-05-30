﻿using EchoOrbit.Controls;
using EchoOrbit.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EchoOrbit.Helpers
{
    public class ChatMessage
    {
        public string SenderDisplayName { get; set; }
        public string Text { get; set; }
        public List<Attachment> Attachments { get; set; }
        public string ControlType { get; set; }
        public string AudioFileName { get; set; }
    }

    public class Attachment
    {
        public string FileName { get; set; }
        public string FileType { get; set; }
        public string ContentBase64 { get; set; }
        public bool IsFileTransfer { get; set; }
        public int TransferPort { get; set; }
        public string LocalFilePath { get; set; }
        public Button BubbleButton { get; set; }
        public IProgress<float> Progress { get; set; }
    }

    public class ChatManager
    {
        private readonly StackPanel messagesContainer;
        private readonly MusicController musicController;
        private readonly Dictionary<string, Attachment> outgoingAudioAttachments = new Dictionary<string, Attachment>();
        private readonly TcpListener tcpListener;
        private readonly int chatPort = 8890;
        private const long FileSizeThreshold = 100 * 1024; // 100 KB
        private readonly string audioStoragePath; // Path to store received audio files

        public ChatSession CurrentChatSession { get; set; }

        public ChatManager(StackPanel container, MusicController musicController)
        {
            this.messagesContainer = container;
            this.musicController = musicController;
            tcpListener = new TcpListener(IPAddress.Any, chatPort);
            tcpListener.Start();

            // Initialize the audio storage folder for received files
            audioStoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EchoOrbit", "ReceivedAudio");
            try
            {
                Directory.CreateDirectory(audioStoragePath);
                Console.WriteLine($"Audio storage directory created at: {audioStoragePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating audio storage directory: {ex.Message}");
            }

            Task.Run(() => ListenForTcpMessages());
        }

        private async Task ListenForTcpMessages()
        {
            while (true)
            {
                try
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    Task.Run(() => ProcessClient(client));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting TCP client: {ex.Message}");
                }
            }
        }

        private void ProcessClient(TcpClient client)
        {
            try
            {
                IPEndPoint senderEndpoint = (IPEndPoint)client.Client.RemoteEndPoint;
                using (client)
                using (NetworkStream ns = client.GetStream())
                using (MemoryStream ms = new MemoryStream())
                {
                    ns.CopyTo(ms);
                    byte[] data = ms.ToArray();
                    string json = Encoding.UTF8.GetString(data);
                    ChatMessage receivedMsg = null;
                    try
                    {
                        receivedMsg = JsonSerializer.Deserialize<ChatMessage>(json);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deserializing message: {ex.Message}");
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (receivedMsg != null)
                        {
                            if (!string.IsNullOrEmpty(receivedMsg.ControlType))
                            {
                                if (receivedMsg.ControlType == "AudioTransferReady")
                                {
                                    Attachment outgoingAtt = GetOutgoingAttachmentByName(receivedMsg.AudioFileName);
                                    if (outgoingAtt != null && outgoingAtt.TransferPort == 0)
                                    {
                                        int port = StartTcpFileTransfer(outgoingAtt.LocalFilePath, outgoingAtt);
                                        outgoingAtt.TransferPort = port;
                                        ChatMessage ctrlMsg = new ChatMessage
                                        {
                                            ControlType = "AudioTransferStarted",
                                            AudioFileName = outgoingAtt.FileName,
                                            Text = port.ToString()
                                        };
                                        SendControlMessage(ctrlMsg, senderEndpoint.Address);
                                    }
                                    return;
                                }
                                else if (receivedMsg.ControlType == "AudioTransferStarted")
                                {
                                    if (int.TryParse(receivedMsg.Text, out int port))
                                    {
                                        UpdateAudioBubbleButton(receivedMsg.AudioFileName, port);
                                    }
                                    return;
                                }
                            }

                            messagesContainer.Children.Add(CreateTextBubble(
                                $"{receivedMsg.SenderDisplayName}: {receivedMsg.Text}",
                                Brushes.White,
                                Brushes.SeaGreen));

                            List<Image> inlineImages = new List<Image>();
                            if (receivedMsg.Attachments != null)
                            {
                                foreach (var att in receivedMsg.Attachments)
                                {
                                    if (att.FileType == "image" && !att.IsFileTransfer)
                                    {
                                        try
                                        {
                                            byte[] imageBytes = Convert.FromBase64String(att.ContentBase64);
                                            using (var msImg = new MemoryStream(imageBytes))
                                            {
                                                BitmapImage bmp = new BitmapImage();
                                                bmp.BeginInit();
                                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                                bmp.StreamSource = msImg;
                                                bmp.EndInit();
                                                Image img = new Image
                                                {
                                                    Source = bmp,
                                                    Width = 100,
                                                    Height = 100,
                                                    Margin = new Thickness(5)
                                                };
                                                inlineImages.Add(img);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Error displaying image: {ex.Message}");
                                        }
                                    }
                                    else if (att.FileType == "audio")
                                    {
                                        // Handle inline audio (Base64)
                                        if (!att.IsFileTransfer && !string.IsNullOrEmpty(att.ContentBase64))
                                        {
                                            att.LocalFilePath = SaveAudioFromBase64(att.ContentBase64, att.FileName);
                                        }
                                        Border audioBubble = CreateAudioBubble(att, senderEndpoint.Address, Brushes.SeaGreen);
                                        messagesContainer.Children.Add(audioBubble);

                                        if (musicController.CurrentPlaylist == null)
                                            musicController.CurrentPlaylist = new ObservableCollection<Song>();
                                        bool alreadyAdded = false;
                                        foreach (var song in musicController.CurrentPlaylist)
                                        {
                                            if (song.Title.Equals(att.FileName, StringComparison.OrdinalIgnoreCase))
                                            {
                                                alreadyAdded = true;
                                                break;
                                            }
                                        }
                                        if (!alreadyAdded && !string.IsNullOrEmpty(att.LocalFilePath))
                                        {
                                            musicController.CurrentPlaylist.Add(new Song { FilePath = att.LocalFilePath, Title = att.FileName });
                                        }
                                    }
                                    else
                                    {
                                        messagesContainer.Children.Add(CreateTextBubble(
                                            $"Attachment: {att.FileName} ({att.FileType})",
                                            Brushes.White,
                                            Brushes.Gray));
                                    }
                                }
                            }

                            if (inlineImages.Count > 0)
                            {
                                int chunkSize = 8;
                                for (int i = 0; i < inlineImages.Count; i += chunkSize)
                                {
                                    int count = Math.Min(chunkSize, inlineImages.Count - i);
                                    List<Image> chunk = inlineImages.GetRange(i, count);
                                    messagesContainer.Children.Add(CreateImageBubble(chunk, Brushes.SeaGreen));
                                }
                            }
                        }
                        else
                        {
                            messagesContainer.Children.Add(CreateTextBubble(
                                $"Peer: {json}",
                                Brushes.White,
                                Brushes.SeaGreen));
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing TCP client: {ex.Message}");
            }
        }

        private Attachment GetOutgoingAttachmentByName(string fileName)
        {
            return outgoingAudioAttachments.TryGetValue(fileName, out Attachment att) ? att : null;
        }

        private void UpdateAudioBubbleButton(string fileName, int transferPort)
        {
            foreach (var child in messagesContainer.Children)
            {
                if (child is Border bubble &&
                    bubble.Child is Grid container &&
                    container.Children.OfType<Button>().FirstOrDefault() is Button btn &&
                    btn.Tag is Tuple<Attachment, IPAddress> tuple &&
                    tuple.Item1.FileName == fileName)
                {
                    tuple.Item1.TransferPort = transferPort;
                    btn.Content = "🢃"; // Ready to download
                    var progressBar = container.Children.OfType<ProgressBar>().FirstOrDefault();
                    if (progressBar != null)
                    {
                        progressBar.Visibility = Visibility.Visible; // Show progress bar for download
                    }
                    break;
                }
            }
        }

        public void SendMessage(string message, List<string> imageAttachments, List<string> audioAttachments, List<string> zipAttachments)
        {
            if (CurrentChatSession == null)
            {
                MessageBox.Show("No active chat session. Please select a user to chat with.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ChatMessage chatMessage = new ChatMessage
            {
                SenderDisplayName = "Me",
                Text = message,
                Attachments = new List<Attachment>()
            };

            foreach (var path in imageAttachments)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        byte[] fileBytes = File.ReadAllBytes(path);
                        chatMessage.Attachments.Add(new Attachment
                        {
                            FileName = Path.GetFileName(path),
                            FileType = "image",
                            ContentBase64 = Convert.ToBase64String(fileBytes),
                            IsFileTransfer = false
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading image: {ex.Message}");
                    }
                }
            }

            foreach (var path in audioAttachments)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(path);
                        Attachment att = new Attachment
                        {
                            FileName = Path.GetFileName(path),
                            FileType = "audio",
                            LocalFilePath = path // Use original path for sent files
                        };
                        if (fi.Length > FileSizeThreshold)
                        {
                            att.IsFileTransfer = true;
                            att.TransferPort = 0; // Port assigned later
                        }
                        else
                        {
                            byte[] fileBytes = File.ReadAllBytes(path);
                            att.ContentBase64 = Convert.ToBase64String(fileBytes);
                            att.IsFileTransfer = false;
                        }
                        chatMessage.Attachments.Add(att);
                        outgoingAudioAttachments[att.FileName] = att;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading audio: {ex.Message}");
                    }
                }
            }

            foreach (var path in zipAttachments)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(path);
                        if (fi.Length > FileSizeThreshold)
                        {
                            int port = StartTcpFileTransfer(path, null);
                            chatMessage.Attachments.Add(new Attachment
                            {
                                FileName = Path.GetFileName(path),
                                FileType = "zip",
                                IsFileTransfer = true,
                                TransferPort = port,
                                ContentBase64 = ""
                            });
                        }
                        else
                        {
                            byte[] fileBytes = File.ReadAllBytes(path);
                            chatMessage.Attachments.Add(new Attachment
                            {
                                FileName = Path.GetFileName(path),
                                FileType = "zip",
                                ContentBase64 = Convert.ToBase64String(fileBytes),
                                IsFileTransfer = false
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading zip: {ex.Message}");
                    }
                }
            }

            string json = JsonSerializer.Serialize(chatMessage);
            byte[] data = Encoding.UTF8.GetBytes(json);

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(CurrentChatSession.PeerEndpoint.Address, chatPort);
                    using (NetworkStream ns = client.GetStream())
                    {
                        ns.Write(data, 0, data.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending message: {ex.Message}");
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                messagesContainer.Children.Add(CreateTextBubble(
                    $"Me: {message}",
                    Brushes.White,
                    Brushes.DodgerBlue));

                List<Image> outgoingImages = new List<Image>();
                List<string> nonImageAttachments = new List<string>();
                if (chatMessage.Attachments != null)
                {
                    foreach (var att in chatMessage.Attachments)
                    {
                        if (att.FileType == "image" && !att.IsFileTransfer)
                        {
                            try
                            {
                                byte[] imageBytes = Convert.FromBase64String(att.ContentBase64);
                                using (var msImg = new MemoryStream(imageBytes))
                                {
                                    BitmapImage bmp = new BitmapImage();
                                    bmp.BeginInit();
                                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                                    bmp.StreamSource = msImg;
                                    bmp.EndInit();
                                    Image img = new Image
                                    {
                                        Source = bmp,
                                        Width = 100,
                                        Height = 100,
                                        Margin = new Thickness(5)
                                    };
                                    outgoingImages.Add(img);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error displaying outgoing image: {ex.Message}");
                            }
                        }
                        else if (att.FileType == "audio")
                        {
                            Border audioBubble = CreateAudioBubble(att, IPAddress.Loopback, Brushes.DodgerBlue);
                            messagesContainer.Children.Add(audioBubble);
                            if (att.IsFileTransfer && att.TransferPort == 0)
                            {
                                // Start upload immediately
                                int port = StartTcpFileTransfer(att.LocalFilePath, att);
                                att.TransferPort = port;
                            }
                        }
                        else
                        {
                            string info = att.IsFileTransfer
                                ? $"{att.FileName} ({att.FileType}) - Transfer on port {att.TransferPort}"
                                : $"{att.FileName} ({att.FileType})";
                            nonImageAttachments.Add("Attachment: " + info);
                        }
                    }
                }

                if (outgoingImages.Count > 0)
                {
                    int chunkSize = 8;
                    for (int i = 0; i < outgoingImages.Count; i += chunkSize)
                    {
                        int count = Math.Min(chunkSize, outgoingImages.Count - i);
                        List<Image> chunk = outgoingImages.GetRange(i, count);
                        messagesContainer.Children.Add(CreateImageBubble(chunk, Brushes.DodgerBlue));
                    }
                }

                foreach (var info in nonImageAttachments)
                {
                    messagesContainer.Children.Add(CreateTextBubble(info, Brushes.White, Brushes.DodgerBlue));
                }
            });
        }

        private Border CreateTextBubble(string text, Brush foreground, Brush background)
        {
            TextBlock tb = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(5)
            };
            Border bubble = new Border
            {
                Background = background,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Margin = new Thickness(5),
                Child = tb
            };
            return bubble;
        }

        private Border CreateImageBubble(List<Image> images, Brush bubbleBackground)
        {
            int n = images.Count;
            StackPanel verticalPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            if (n <= 4)
            {
                Grid grid = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                for (int i = 0; i < n; i++)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }
                for (int i = 0; i < n; i++)
                {
                    Image img = images[i];
                    img.Margin = new Thickness(0);
                    img.HorizontalAlignment = HorizontalAlignment.Stretch;
                    img.VerticalAlignment = VerticalAlignment.Stretch;
                    img.Stretch = Stretch.UniformToFill;

                    img.MouseLeftButtonUp += (s, e) =>
                    {
                        List<ImageSource> bubbleImageSources = images.Select(i => i.Source).ToList();
                        int clickedIndex = bubbleImageSources.IndexOf(img.Source);
                        var focusWindow = new ImageFocusWindow(bubbleImageSources, clickedIndex);
                        focusWindow.ShowDialog();
                    };

                    Grid.SetRow(img, 0);
                    Grid.SetColumn(img, i);
                    grid.Children.Add(img);
                }
                verticalPanel.Children.Add(grid);
            }
            else if (n <= 8)
            {
                int row1Count = (int)Math.Ceiling(n / 2.0);
                int row2Count = n - row1Count;

                Grid grid1 = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                grid1.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                for (int i = 0; i < row1Count; i++)
                {
                    grid1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }
                for (int i = 0; i < row1Count; i++)
                {
                    Image img = images[i];
                    img.Margin = new Thickness(0);
                    img.HorizontalAlignment = HorizontalAlignment.Stretch;
                    img.VerticalAlignment = VerticalAlignment.Stretch;
                    img.Stretch = Stretch.UniformToFill;

                    img.MouseLeftButtonUp += (s, e) =>
                    {
                        List<ImageSource> bubbleImageSources = images.Select(i => i.Source).ToList();
                        int clickedIndex = bubbleImageSources.IndexOf(img.Source);
                        var focusWindow = new ImageFocusWindow(bubbleImageSources, clickedIndex);
                        focusWindow.ShowDialog();
                    };

                    Grid.SetRow(img, 0);
                    Grid.SetColumn(img, i);
                    grid1.Children.Add(img);
                }

                Grid grid2 = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                grid2.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                for (int i = 0; i < row2Count; i++)
                {
                    grid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }
                for (int i = 0; i < row2Count; i++)
                {
                    Image img = images[row1Count + i];
                    img.Margin = new Thickness(0);
                    img.HorizontalAlignment = HorizontalAlignment.Stretch;
                    img.VerticalAlignment = VerticalAlignment.Stretch;
                    img.Stretch = Stretch.UniformToFill;

                    img.MouseLeftButtonUp += (s, e) =>
                    {
                        List<ImageSource> bubbleImageSources = images.Select(i => i.Source).ToList();
                        int clickedIndex = bubbleImageSources.IndexOf(img.Source);
                        var focusWindow = new ImageFocusWindow(bubbleImageSources, clickedIndex);
                        focusWindow.ShowDialog();
                    };

                    Grid.SetRow(img, 0);
                    Grid.SetColumn(img, i);
                    grid2.Children.Add(img);
                }

                verticalPanel.Children.Add(grid1);
                verticalPanel.Children.Add(grid2);
            }

            Border bubble = new Border
            {
                Background = bubbleBackground,
                Padding = new Thickness(0),
                Margin = new Thickness(5),
                CornerRadius = new CornerRadius(15),
                Child = verticalPanel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                SnapsToDevicePixels = true
            };

            bubble.SetBinding(Border.WidthProperty, new System.Windows.Data.Binding("ActualWidth")
            {
                Source = messagesContainer,
                Mode = System.Windows.Data.BindingMode.OneWay
            });

            return bubble;
        }

        private SolidColorBrush AdjustColor(SolidColorBrush baseBrush, bool lighten)
        {
            Color color = baseBrush.Color;
            double factor = lighten ? 1.2 : 0.8;
            byte r = (byte)Math.Min(255, Math.Max(0, color.R * factor));
            byte g = (byte)Math.Min(255, Math.Max(0, color.G * factor));
            byte b = (byte)Math.Min(255, Math.Max(0, color.B * factor));
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private Border CreateAudioBubble(Attachment att, IPAddress senderIP, Brush bubbleBackground)
        {
            bool isLocal = senderIP.Equals(IPAddress.Loopback);
            string initialSymbol = isLocal ? "♫" : "🢃";

            Button playButton = new Button
            {
                Content = initialSymbol,
                FontSize = 48,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 60,
                Height = 60
            };
            playButton.Tag = new Tuple<Attachment, IPAddress>(att, senderIP);
            att.BubbleButton = playButton;

            ProgressBar progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Width = 70,
                Height = 70,
                Visibility = isLocal && att.IsFileTransfer ? Visibility.Visible : Visibility.Collapsed // Visible for sending
            };

            if (bubbleBackground == Brushes.SeaGreen)
            {
                progressBar.Foreground = AdjustColor((SolidColorBrush)bubbleBackground, true);
            }
            else if (bubbleBackground == Brushes.DodgerBlue)
            {
                progressBar.Foreground = AdjustColor((SolidColorBrush)bubbleBackground, false);
            }
            else
            {
                progressBar.Foreground = Brushes.Blue;
            }

            try
            {
                var template = Application.Current.FindResource("CircularProgressBarTemplate") as ControlTemplate;
                if (template != null)
                {
                    progressBar.Template = template;
                }
                else
                {
                    Console.WriteLine("Warning: CircularProgressBarTemplate not found, using default ProgressBar template.");
                }
            }
            catch (ResourceReferenceKeyNotFoundException ex)
            {
                Console.WriteLine($"Error: CircularProgressBarTemplate resource not found. Using default ProgressBar template. {ex.Message}");
            }

            // Initialize Progress<float> for the attachment
            att.Progress = new Progress<float>(progressValue =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    progressBar.Value = progressValue * 100; // Convert 0-1 to 0-100
                    progressBar.Visibility = Visibility.Visible;
                    if (progressValue >= 1.0f)
                    {
                        progressBar.Visibility = Visibility.Collapsed;
                        if (!isLocal)
                        {
                            playButton.Content = "♫"; // Update to play symbol after download
                        }
                    }
                    Console.WriteLine($"Progress for '{att.FileName}': {progressValue:P0}");
                });
            });

            Grid container = new Grid();
            container.Children.Add(progressBar); // Add progressBar first (background)
            container.Children.Add(playButton);  // Add playButton on top

            playButton.Click += async (s, e) =>
            {
                var tuple = (Tuple<Attachment, IPAddress>)playButton.Tag;
                var attachment = tuple.Item1;
                var ip = tuple.Item2;
                string filePath = attachment.LocalFilePath;

                if (!isLocal)
                {
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    {
                        if (attachment.TransferPort == 0)
                        {
                            ChatMessage ctrlMsg = new ChatMessage
                            {
                                ControlType = "AudioTransferReady",
                                AudioFileName = attachment.FileName
                            };
                            SendControlMessage(ctrlMsg, ip);
                            progressBar.Visibility = Visibility.Visible; // Show progress bar for download
                            return;
                        }
                        else
                        {
                            try
                            {
                                // Save to the ReceivedAudio folder
                                filePath = Path.Combine(audioStoragePath, attachment.FileName);
                                filePath = GetUniqueFilePath(filePath);
                                await TCPFileHandler.DownloadFileAsync(ip, attachment.TransferPort, filePath, attachment.Progress);
                                attachment.LocalFilePath = filePath;
                                Console.WriteLine($"Saved received audio to: {filePath}");
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error downloading audio file: {ex.Message}");
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    progressBar.Visibility = Visibility.Collapsed;
                                });
                                return;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    if (musicController.CurrentPlaylist == null)
                    {
                        musicController.CurrentPlaylist = new ObservableCollection<Song>();
                    }
                    bool alreadyAdded = false;
                    foreach (var song in musicController.CurrentPlaylist)
                    {
                        if (song.Title.Equals(attachment.FileName, StringComparison.OrdinalIgnoreCase))
                        {
                            alreadyAdded = true;
                            break;
                        }
                    }
                    if (!alreadyAdded)
                    {
                        Song newSong = new Song { FilePath = filePath, Title = attachment.FileName };
                        musicController.CurrentPlaylist.Add(newSong);
                        musicController.CurrentPlaylistIndex = musicController.CurrentPlaylist.Count - 1;
                    }
                    Console.WriteLine($"Playing audio file '{filePath}'");
                    musicController.PlayMusicFromFile(filePath);
                }
                else
                {
                    MessageBox.Show($"Audio file not found at: {filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            Border bubble = new Border
            {
                Background = bubbleBackground,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Child = container,
                Margin = new Thickness(5)
            };
            return bubble;
        }

        private string SaveAudioFromBase64(string base64, string fileName)
        {
            string filePath = Path.Combine(audioStoragePath, fileName);
            try
            {
                filePath = GetUniqueFilePath(filePath);
                byte[] bytes = Convert.FromBase64String(base64);
                File.WriteAllBytes(filePath, bytes);
                Console.WriteLine($"Saved inline audio '{fileName}' to '{filePath}'");
                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving audio file '{fileName}': {ex.Message}");
                MessageBox.Show($"Error saving audio file: {ex.Message}");
                return "";
            }
        }

        private string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath))
                return filePath;

            string directory = Path.GetDirectoryName(filePath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);
            int counter = 1;
            string newFilePath;

            do
            {
                newFilePath = Path.Combine(directory, $"{fileNameWithoutExtension} ({counter}){extension}");
                counter++;
            } while (File.Exists(newFilePath));

            return newFilePath;
        }

        private void SendControlMessage(ChatMessage ctrlMsg, IPAddress targetIP)
        {
            try
            {
                string json = JsonSerializer.Serialize(ctrlMsg);
                byte[] data = Encoding.UTF8.GetBytes(json);
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(targetIP, chatPort);
                    using (NetworkStream ns = client.GetStream())
                    {
                        ns.Write(data, 0, data.Length);
                    }
                }
                Console.WriteLine($"Sent control message '{ctrlMsg.ControlType}' to {targetIP}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending control message: {ex.Message}");
            }
        }

        private int StartTcpFileTransfer(string filePath, Attachment att)
        {
            int port = TCPFileHandler.StartFileTransferServer(filePath, att?.Progress);
            Console.WriteLine($"Started TCP file transfer for '{Path.GetFileName(filePath)}' on port {port}");
            return port;
        }
    }
}