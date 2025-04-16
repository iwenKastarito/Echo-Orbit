using EchoOrbit.Controls;
using EchoOrbit.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        // Control messages use these fields:
        public string ControlType { get; set; }  // e.g.: "AudioTransferReady", "AudioTransferStarted"
        public string AudioFileName { get; set; }
    }

    public class Attachment
    {
        public string FileName { get; set; }
        /// <summary>
        /// "image", "audio", or "zip"
        /// </summary>
        public string FileType { get; set; }
        /// <summary>
        /// For small files, inline: Base64 encoded.
        /// </summary>
        public string ContentBase64 { get; set; }
        /// <summary>
        /// If true, the file must be transferred via TCP.
        /// </summary>
        public bool IsFileTransfer { get; set; }
        /// <summary>
        /// The TCP port on the sender’s side where the file can be downloaded.
        /// </summary>
        public int TransferPort { get; set; }
        /// <summary>
        /// Once downloaded, store the local file path.
        /// </summary>
        public string LocalFilePath { get; set; }
        /// <summary>
        /// (For incoming audio) Reference to the UI container (e.g. a Grid) used in the audio bubble.
        /// </summary>
        public FrameworkElement BubbleElement { get; set; }
    }

    public class ChatManager
    {
        private StackPanel messagesContainer;
        private MusicController musicController;
        private Dictionary<string, Attachment> outgoingAudioAttachments = new Dictionary<string, Attachment>();

        /// <summary>
        /// The current active chat session.
        /// </summary>
        public ChatSession CurrentChatSession { get; set; }

        // TCP server used for incoming chat messages.
        private TcpListener tcpListener;
        private int chatPort = 8890; // fixed port for chat messages

        // Threshold (in bytes) above which a file is sent via TCP file transfer.
        private const long FileSizeThreshold = 100 * 1024; // e.g. 100 KB

        private Attachment GetOutgoingAttachmentByName(string fileName)
        {
            if (outgoingAudioAttachments.TryGetValue(fileName, out Attachment att))
            {
                return att;
            }
            return null;
        }

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

        private void UpdateAudioBubbleButton(string fileName, int transferPort)
        {
            // Iterate through the messagesContainer's children to find a container
            // whose Tag is a Tuple<Attachment, IPAddress> for the given fileName.
            foreach (var child in messagesContainer.Children)
            {
                if (child is Border bubble &&
                    bubble.Tag is Tuple<Attachment, IPAddress> tuple)
                {
                    if (tuple.Item1.FileName == fileName)
                    {
                        // Update the Attachment's transfer port.
                        tuple.Item1.TransferPort = transferPort;
                        // Change the inner icon (if available) from "🢃" to "♫".
                        if (bubble.Child is Grid grid && grid.Children.Count > 0)
                        {
                            if (grid.Children[0] is ContentControl iconControl)
                            {
                                iconControl.Content = "♫";
                            }
                        }
                        break;
                    }
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
                    catch (Exception)
                    {
                        // If deserialization fails, treat data as plain text.
                    }
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (receivedMsg != null)
                        {
                            // Process control messages first.
                            if (!string.IsNullOrEmpty(receivedMsg.ControlType))
                            {
                                if (receivedMsg.ControlType == "AudioTransferReady")
                                {
                                    // We are the sender.
                                    Attachment outgoingAtt = GetOutgoingAttachmentByName(receivedMsg.AudioFileName);
                                    if (outgoingAtt != null && outgoingAtt.TransferPort == 0)
                                    {
                                        int port = StartTcpFileTransfer(outgoingAtt.LocalFilePath);
                                        outgoingAtt.TransferPort = port;
                                        ChatMessage ctrlMsg = new ChatMessage
                                        {
                                            ControlType = "AudioTransferStarted",
                                            AudioFileName = outgoingAtt.FileName,
                                            Text = port.ToString()
                                        };
                                        SendControlMessage(ctrlMsg, ((IPEndPoint)client.Client.RemoteEndPoint).Address);
                                    }
                                    return;  // Don't update UI for control messages.
                                }
                                else if (receivedMsg.ControlType == "AudioTransferStarted")
                                {
                                    int port;
                                    if (int.TryParse(receivedMsg.Text, out port))
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
                                    if (att.FileType == "audio")
                                    {
                                        Border audioBubble = CreateAudioBubble(att, ((IPEndPoint)client.Client.RemoteEndPoint).Address, Brushes.SeaGreen);
                                        messagesContainer.Children.Add(audioBubble);
                                        if (musicController.CurrentPlaylist == null)
                                            musicController.CurrentPlaylist = new ObservableCollection<Song>();
                                        bool alreadyAdded = musicController.CurrentPlaylist.Any(song =>
                                            song.Title.Equals(att.FileName, StringComparison.OrdinalIgnoreCase));
                                        if (!alreadyAdded)
                                        {
                                            Song newSong = new Song { FilePath = "", Title = att.FileName };
                                            musicController.CurrentPlaylist.Add(newSong);
                                        }
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
                Console.WriteLine("Error processing TCP client: " + ex.Message);
            }
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
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
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
                Grid grid1 = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
                grid1.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                for (int i = 0; i < row1Count; i++)
                    grid1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
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
                Grid grid2 = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
                grid2.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                for (int i = 0; i < row2Count; i++)
                    grid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
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

            bubble.SetBinding(Border.WidthProperty, new Binding("ActualWidth")
            {
                Source = messagesContainer,
                Mode = BindingMode.OneWay
            });

            return bubble;
        }

        private async Task<string> DownloadAudioFileAsync(Attachment att, IPAddress senderIP, IProgress<double> progress)
        {
            string filePath = Path.Combine(Path.GetTempPath(), att.FileName);
            try
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    await tcpClient.ConnectAsync(senderIP, att.TransferPort);
                    using (NetworkStream ns = tcpClient.GetStream())
                    using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] lengthBytes = new byte[8];
                        int read = 0;
                        while (read < 8)
                        {
                            int r = await ns.ReadAsync(lengthBytes, read, 8 - read);
                            if (r == 0) break;
                            read += r;
                        }
                        long totalLength = BitConverter.ToInt64(lengthBytes, 0);
                        if (totalLength <= 0)
                        {
                            MessageBox.Show("Received file size is zero.", "Download Error");
                            return "";
                        }
                        long totalRead = 0;
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = await ns.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fs.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;
                            progress.Report((totalRead * 100.0) / totalLength);
                        }
                        await fs.FlushAsync();
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

        private string SaveAudioFromBase64(string base64, string fileName)
        {
            string filePath = Path.Combine(Path.GetTempPath(), fileName);
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                File.WriteAllBytes(filePath, bytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving audio file: " + ex.Message);
                return "";
            }
            return filePath;
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
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending control message: " + ex.Message);
            }
        }

        private Border CreateAudioBubble(Attachment att, IPAddress senderIP, Brush bubbleBackground)
        {
            bool isLocal = senderIP.Equals(IPAddress.Loopback);
            string initialSymbol = isLocal ? "♫" : "🢃";

            ContentControl iconControl = new ContentControl
            {
                Content = initialSymbol,
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White,
                Background = Brushes.Transparent
            };

            Grid container = new Grid();
            container.Children.Add(iconControl);

            Border bubble = new Border
            {
                Background = bubbleBackground,
                Padding = new Thickness(10),
                Margin = new Thickness(5),
                CornerRadius = new CornerRadius(15),
                Child = container,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Tag = new Tuple<Attachment, IPAddress>(att, senderIP)
            };
            att.BubbleElement = container;

            iconControl.MouseLeftButtonUp += async (s, e) =>
            {
                // Prevent concurrent downloads.
                if (container.Tag is bool inProgress && inProgress)
                {
                    MessageBox.Show("File download is in progress. Please wait until it finishes.");
                    return;
                }

                var attachment = att;
                string filePath = attachment.LocalFilePath;

                if (!isLocal)
                {
                    // Check if file exists locally; if not, try to download.
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    {
                        // If TransferPort hasn't been set, request transfer and poll for up to 6 seconds.
                        if (attachment.TransferPort == 0)
                        {
                            ChatMessage ctrlMsg = new ChatMessage
                            {
                                ControlType = "AudioTransferReady",
                                AudioFileName = attachment.FileName
                            };
                            SendControlMessage(ctrlMsg, senderIP);
                            int pollCount = 0;
                            while (attachment.TransferPort == 0 && pollCount < 6)
                            {
                                await Task.Delay(1000);
                                pollCount++;
                            }
                            if (attachment.TransferPort == 0)
                            {
                                MessageBox.Show("File transfer did not start yet. Please try again later.");
                                return;
                            }
                        }
                        container.Tag = true;
                        CircularProgressBar progressBar = new CircularProgressBar
                        {
                            Minimum = 0,
                            Maximum = 100,
                            Value = 0,
                            Width = 60,
                            Height = 60,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = Brushes.LightGreen,
                            Background = Brushes.Transparent
                        };
                        container.Children.Add(progressBar);
                        var progressIndicator = new Progress<double>(percent => progressBar.Value = percent);
                        filePath = await DownloadAudioFileAsync(attachment, senderIP, progressIndicator);
                        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                        {
                            MessageBox.Show("Audio file download failed. Please try again.");
                            container.Children.Remove(progressBar);
                            container.Tag = false;
                            return;
                        }
                        attachment.LocalFilePath = filePath;
                        container.Children.Remove(progressBar);
                        iconControl.Content = "♫";
                        container.Tag = false;
                    }
                }
                // Play the audio if file path is now valid.
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    if (musicController.CurrentPlaylist == null)
                    {
                        musicController.CurrentPlaylist = new ObservableCollection<Song>();
                    }
                    bool alreadyAdded = musicController.CurrentPlaylist.Any(song =>
                        song.Title.Equals(attachment.FileName, StringComparison.OrdinalIgnoreCase));
                    if (!alreadyAdded)
                    {
                        Song newSong = new Song { FilePath = filePath, Title = attachment.FileName };
                        musicController.CurrentPlaylist.Add(newSong);
                        musicController.CurrentPlaylistIndex = musicController.CurrentPlaylist.Count - 1;
                    }
                    musicController.PlayMusicFromFile(filePath);
                }
                else
                {
                    MessageBox.Show("Audio file is not available to play.");
                }
            };

            return bubble;
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
                        Console.WriteLine("Error reading image: " + ex.Message);
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
                        if (fi.Length > FileSizeThreshold)
                        {
                            chatMessage.Attachments.Add(new Attachment
                            {
                                FileName = Path.GetFileName(path),
                                FileType = "audio",
                                IsFileTransfer = true,
                                TransferPort = 0,
                                LocalFilePath = path,
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

            foreach (var path in zipAttachments)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        FileInfo fi = new FileInfo(path);
                        if (fi.Length > FileSizeThreshold)
                        {
                            chatMessage.Attachments.Add(new Attachment
                            {
                                FileName = Path.GetFileName(path),
                                FileType = "zip",
                                IsFileTransfer = true,
                                TransferPort = 0,
                                LocalFilePath = path,
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
                                Console.WriteLine("Error displaying outgoing image: " + ex.Message);
                            }
                        }
                        else if (att.FileType == "audio")
                        {
                            Border audioBubble = CreateAudioBubble(att, System.Net.IPAddress.Loopback, Brushes.DodgerBlue);
                            messagesContainer.Children.Add(audioBubble);
                            if (!outgoingAudioAttachments.ContainsKey(att.FileName))
                            {
                                outgoingAudioAttachments.Add(att.FileName, att);
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

        private int StartTcpFileTransfer(string filePath)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 0); // OS chooses a free port.
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task.Run(async () =>
            {
                try
                {
                    var acceptTask = listener.AcceptTcpClientAsync();
                    if (await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(60))) == acceptTask)
                    {
                        using (TcpClient client = acceptTask.Result)
                        using (NetworkStream ns = client.GetStream())
                        using (FileStream fs = File.OpenRead(filePath))
                        {
                            byte[] lengthBytes = BitConverter.GetBytes(fs.Length);
                            await ns.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                            await fs.CopyToAsync(ns);
                        }
                    }
                    else
                    {
                        Console.WriteLine("File transfer timeout: no connection was made within 60 seconds.");
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

    // Minimal stub of a CircularProgressBar control.
    public class CircularProgressBar : ContentControl
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(CircularProgressBar), new PropertyMetadata(0.0, OnValueChanged));

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(double), typeof(CircularProgressBar), new PropertyMetadata(0.0));

        public double Minimum
        {
            get { return (double)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(double), typeof(CircularProgressBar), new PropertyMetadata(100.0));

        public double Maximum
        {
            get { return (double)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            CircularProgressBar cpb = d as CircularProgressBar;
            if (cpb != null && cpb.Content is TextBlock tb)
            {
                tb.Text = $"{cpb.Value:0}%";
            }
        }

        static CircularProgressBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CircularProgressBar), new FrameworkPropertyMetadata(typeof(CircularProgressBar)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            this.Content = new TextBlock
            {
                Text = $"{Value:0}%",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = this.Foreground,
                FontSize = 14
            };
        }
    }
}
