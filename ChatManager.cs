using EchoOrbit.Controls;
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
    // Models for the chat message and its attachments.
    public class ChatMessage
    {
        public string SenderDisplayName { get; set; }
        public string Text { get; set; }
        public List<Attachment> Attachments { get; set; }
    }

    public class Attachment
    {
        public string FileName { get; set; }
        /// <summary>
        /// "image", "audio", or "zip"
        /// </summary>
        public string FileType { get; set; }
        /// <summary>
        /// For small files, the file content encoded as a Base64 string.
        /// For large files sent via TCP, this is left empty.
        /// </summary>
        public string ContentBase64 { get; set; }
        /// <summary>
        /// If true, the file was not sent inline and must be downloaded via TCP.
        /// </summary>
        public bool IsFileTransfer { get; set; }
        /// <summary>
        /// The TCP port on the sender’s side where the file can be downloaded.
        /// </summary>
        public int TransferPort { get; set; }
    }

    public class ChatManager
    {
        private StackPanel messagesContainer;
        private MusicController musicController;

        /// <summary>
        /// The current active chat session.
        /// </summary>
        public ChatSession CurrentChatSession { get; set; }

        // TCP server used for incoming chat messages.
        private TcpListener tcpListener;
        private int chatPort = 8890; // fixed port for chat messages

        // Threshold (in bytes) above which a file is sent via TCP file transfer.
        private const long FileSizeThreshold = 100 * 1024; // e.g. 100 KB

        public ChatManager(StackPanel container, MusicController musicController)
        {
            this.messagesContainer = container;
            this.musicController = musicController;
            tcpListener = new TcpListener(IPAddress.Any, chatPort);
            tcpListener.Start();
            // Start listening for incoming TCP messages.
            Task.Run(() => ListenForTcpMessages());
        }

        private async Task ListenForTcpMessages()
        {
            while (true)
            {
                try
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    // Process each connection on its own task.
                    Task.Run(() => ProcessClient(client));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error accepting TCP client: " + ex.Message);
                }
            }
        }

        private void ProcessClient(TcpClient client)
        {
            try
            {
                // Get the sender's endpoint from the client.
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
                    catch (Exception)
                    {
                        // If deserialization fails, treat data as plain text.
                    }
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (receivedMsg != null)
                        {
                            // Create a text bubble for the message text.
                            messagesContainer.Children.Add(CreateTextBubble(
                                $"{receivedMsg.SenderDisplayName}: {receivedMsg.Text}",
                                Brushes.White,
                                Brushes.SeaGreen));

                            // Group inline image attachments.
                            List<Image> inlineImages = new List<Image>();

                            if (receivedMsg.Attachments != null)
                            {
                                foreach (var att in receivedMsg.Attachments)
                                {
                                    if (att.IsFileTransfer && (att.FileType == "audio" || att.FileType == "zip"))
                                    {
                                        // For file-transfer attachments, create clickable text to download.
                                        TextBlock downloadBlock = new TextBlock
                                        {
                                            Text = $"File '{att.FileName}' available. Click here to download.",
                                            Foreground = Brushes.LightBlue,
                                            Margin = new Thickness(5),
                                            Cursor = System.Windows.Input.Cursors.Hand
                                        };
                                        downloadBlock.MouseLeftButtonUp += (s, e) =>
                                        {
                                            DownloadFile(att, senderEndpoint.Address);
                                        };
                                        messagesContainer.Children.Add(downloadBlock);
                                    }
                                    else if (att.FileType == "image" && !att.IsFileTransfer)
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
                                            Console.WriteLine("Error displaying image: " + ex.Message);
                                        }
                                    }
                                    else if (att.FileType == "audio")
                                    {
                                        // Create an audio bubble for the audio attachment.
                                        Border audioBubble = CreateAudioBubble(att, ((IPEndPoint)client.Client.RemoteEndPoint).Address, Brushes.SeaGreen);
                                        messagesContainer.Children.Add(audioBubble);
                                    }
                                    else
                                    {
                                        // For non-image inline attachments.
                                        messagesContainer.Children.Add(CreateTextBubble(
                                            $"Attachment: {att.FileName} ({att.FileType})",
                                            Brushes.White,
                                            Brushes.Gray));
                                    }
                                }
                            }
                            // If we collected any inline images, group them in one image bubble.
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
                Console.WriteLine("Error processing TCP client: " + ex.Message);
            }
        }


        /// <summary>
        /// Sends a chat message with attachments using TCP.
        /// </summary>
        public void SendMessage(string message, List<string> imageAttachments, List<string> audioAttachments, List<string> zipAttachments)
        {
            if (CurrentChatSession == null)
            {
                MessageBox.Show("No active chat session. Please select a user to chat with.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ChatMessage chatMessage = new ChatMessage
            {
                SenderDisplayName = "Me", // Replace with actual sender's display name if available.
                Text = message,
                Attachments = new List<Attachment>()
            };

            // Process image attachments (assumed small enough to send inline).
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
                        Console.WriteLine("Error reading image: " + ex.Message);
                    }
                }
            }

            // Process audio attachments.
            foreach (var path in audioAttachments)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(path);
                        if (fi.Length > FileSizeThreshold)
                        {
                            int port = StartTcpFileTransfer(path);
                            chatMessage.Attachments.Add(new Attachment
                            {
                                FileName = Path.GetFileName(path),
                                FileType = "audio",
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
                                FileType = "audio",
                                ContentBase64 = Convert.ToBase64String(fileBytes),
                                IsFileTransfer = false
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error reading audio: " + ex.Message);
                    }
                }
            }

            // Process zip attachments.
            foreach (var path in zipAttachments)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(path);
                        if (fi.Length > FileSizeThreshold)
                        {
                            int port = StartTcpFileTransfer(path);
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
                        Console.WriteLine("Error reading zip: " + ex.Message);
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
                MessageBox.Show("Error sending message: " + ex.Message);
            }

            // Update UI for outgoing message.
            Application.Current.Dispatcher.Invoke(() =>
            {
                messagesContainer.Children.Add(CreateTextBubble(
                    $"Me: {message}",
                    Brushes.White,
                    Brushes.DodgerBlue));

                // Group outgoing inline image attachments.
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
                                Console.WriteLine("Error displaying outgoing image: " + ex.Message);
                            }
                        }
                        else if (att.FileType == "audio")
                        {
                            // For outgoing messages, use IPAddress.Loopback as the sender is local.
                            Border audioBubble = CreateAudioBubble(att, IPAddress.Loopback, Brushes.DodgerBlue);
                            messagesContainer.Children.Add(audioBubble);
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

        /// <summary>
        /// Creates a text bubble (a Border containing a TextBlock) for displaying a message.
        /// </summary>
        /// <param name="text">The message text.</param>
        /// <param name="foreground">The text color.</param>
        /// <param name="background">The bubble background color.</param>
        /// <returns>A Border element styled as a text bubble.</returns>
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


        /// </summary>
        /// <param name="images">A list of Image controls (their Margin will be set to zero and alignment to Stretch).</param>
        /// <param name="bubbleBackground">The background Brush for the bubble.</param>
        /// <returns>A Border element with rounded corners containing the arranged images.</returns>
        private Border CreateImageBubble(List<Image> images, Brush bubbleBackground)
        {
            int n = images.Count;
            // Create a vertical container
            StackPanel verticalPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            if (n <= 4)
            {
                // Single row: create a Grid with 1 row and n columns.
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

                    // Attach event handler to open focus window.
                    img.MouseLeftButtonUp += (s, e) =>
                    {
                        // Create a list of ImageSource objects from the entire 'images' list (the current bubble)
                        List<ImageSource> bubbleImageSources = images.Select(i => i.Source).ToList();
                        // Get the index of the clicked image.
                        int clickedIndex = bubbleImageSources.IndexOf(img.Source);
                        // Open the focus window passing the list and the clicked index.
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
                // Two rows: split the images into two rows.
                int row1Count = (int)Math.Ceiling(n / 2.0);
                int row2Count = n - row1Count;

                // First row Grid.
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

                    // Attach event handler for focus.
                    img.MouseLeftButtonUp += (s, e) =>
                    {
                        // Here, use the whole images list (from this bubble).
                        List<ImageSource> bubbleImageSources = images.Select(i => i.Source).ToList();
                        int clickedIndex = bubbleImageSources.IndexOf(img.Source);
                        var focusWindow = new ImageFocusWindow(bubbleImageSources, clickedIndex);
                        focusWindow.ShowDialog();
                    };

                    Grid.SetRow(img, 0);
                    Grid.SetColumn(img, i);
                    grid1.Children.Add(img);
                }

                // Second row Grid.
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
                // For the second row:
                for (int i = 0; i < row2Count; i++)
                {
                    Image img = images[row1Count + i];
                    img.Margin = new Thickness(0);
                    img.HorizontalAlignment = HorizontalAlignment.Stretch;
                    img.VerticalAlignment = VerticalAlignment.Stretch;
                    img.Stretch = Stretch.UniformToFill;

                    // Attach the event handler.
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

            // Wrap the vertical panel in a Border.
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

            // Bind the bubble's width to the messagesContainer's ActualWidth so it fills the space.
            // (Assumes messagesContainer is accessible in this scope.)
            bubble.SetBinding(Border.WidthProperty, new System.Windows.Data.Binding("ActualWidth")
            {
                Source = messagesContainer,
                Mode = System.Windows.Data.BindingMode.OneWay
            });

            return bubble;
        }


        // Asynchronous method to download an audio file via TCP.
        private async Task<string> DownloadAudioFileAsync(Attachment att, IPAddress senderIP)
        {
            string filePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), att.FileName);
            try
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    await tcpClient.ConnectAsync(senderIP, att.TransferPort);
                    using (NetworkStream ns = tcpClient.GetStream())
                    using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        await ns.CopyToAsync(fs);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error downloading audio file: " + ex.Message);
                return "";
            }
            return filePath;
        }

        // Synchronously saves an inline (Base64) audio file to a temporary location.
        private string SaveAudioFromBase64(string base64, string fileName)
        {
            string filePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                System.IO.File.WriteAllBytes(filePath, bytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving audio file: " + ex.Message);
                return "";
            }
            return filePath;
        }


        private Border CreateAudioBubble(Attachment att, IPAddress senderIP, Brush bubbleBackground)
        {
            Button playButton = new Button
            {
                Content = "♫",
                FontSize = 48,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            // Store the attachment and senderIP in the Tag for use in the click handler.
            playButton.Tag = new Tuple<Attachment, IPAddress>(att, senderIP);
            playButton.Click += async (s, e) =>
            {
                var tuple = (Tuple<Attachment, IPAddress>)playButton.Tag;
                var attachment = tuple.Item1;
                var ip = tuple.Item2;
                string filePath = "";
                if (attachment.IsFileTransfer)
                {
                    filePath = await DownloadAudioFileAsync(attachment, ip);
                }
                else
                {
                    filePath = SaveAudioFromBase64(attachment.ContentBase64, attachment.FileName);
                }
                if (!string.IsNullOrEmpty(filePath))
                {
                    // Create a Song object from the file.
                    Song song = new Song { FilePath = filePath, Title = attachment.FileName };
                    // If no playlist exists, create one.
                    if (musicController.CurrentPlaylist == null)
                    {
                        musicController.CurrentPlaylist = new ObservableCollection<Song>();
                    }
                    // Add the song to the playlist.
                    musicController.CurrentPlaylist.Add(song);
                    // Set the playlist index to the last song added.
                    musicController.CurrentPlaylistIndex = musicController.CurrentPlaylist.Count - 1;
                    // Play the song.
                    musicController.PlayMusicFromFile(filePath);
                }
            };

            Border bubble = new Border
            {
                Background = bubbleBackground,
                Padding = new Thickness(10),
                Margin = new Thickness(5),
                CornerRadius = new CornerRadius(15),
                Child = playButton,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            return bubble;
        }


        /// <summary>
        /// Starts a TCP file transfer for the given file.
        /// Streams the file so that large files (up to 500 MB or more) can be transferred.
        /// Returns the port number for the transfer.
        /// </summary>
        private int StartTcpFileTransfer(string filePath)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 0); // OS chooses a free port.
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task.Run(() =>
            {
                try
                {
                    using (TcpClient client = listener.AcceptTcpClient())
                    using (NetworkStream ns = client.GetStream())
                    using (FileStream fs = File.OpenRead(filePath))
                    {
                        fs.CopyTo(ns);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error during TCP file transfer: " + ex.Message);
                }
                finally
                {
                    listener.Stop();
                }
            });
            return port;
        }

        /// <summary>
        /// Downloads a file from the sender via TCP using the provided transfer port.
        /// </summary>
        private void DownloadFile(Attachment att, IPAddress senderIP)
        {
            try
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    tcpClient.Connect(senderIP, att.TransferPort);
                    using (NetworkStream ns = tcpClient.GetStream())
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ns.CopyTo(ms);
                        byte[] fileData = ms.ToArray();
                        string filePath = Path.Combine(Path.GetTempPath(), att.FileName);
                        File.WriteAllBytes(filePath, fileData);
                        MessageBox.Show($"File downloaded to: {filePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error downloading file: " + ex.Message);
            }
        }
    }
}
