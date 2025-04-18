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

        // TCP listener for control messages (text/chat signaling).
        private TcpListener tcpListener;
        private int chatPort = 8890;

        // Threshold (in bytes) above which a file is sent via TCP file transfer.
        private const long FileSizeThreshold = 100 * 1024; // 100 KB

        public ChatManager(StackPanel container, MusicController musicController)
        {
            this.messagesContainer = container;
            this.musicController = musicController;
            tcpListener = new TcpListener(IPAddress.Any, chatPort);
            tcpListener.Start();
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
                    Console.WriteLine("Error accepting TCP client: " + ex.Message);
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
                    string json = Encoding.UTF8.GetString(ms.ToArray());
                    ChatMessage receivedMsg = null;
                    try { receivedMsg = JsonSerializer.Deserialize<ChatMessage>(json); } catch { }

                    Application.Current.Dispatcher.Invoke(async () =>
                    {
                        if (receivedMsg != null && string.IsNullOrEmpty(receivedMsg.ControlType))
                        {
                            // Display text bubble
                            messagesContainer.Children.Add(CreateTextBubble(
                                $"{receivedMsg.SenderDisplayName}: {receivedMsg.Text}",
                                Brushes.White,
                                Brushes.SeaGreen));

                            // Handle attachments
                            if (receivedMsg.Attachments != null)
                            {
                                foreach (var att in receivedMsg.Attachments)
                                {
                                    if (att.FileType == "image" && !att.IsFileTransfer)
                                    {
                                        // Inline image
                                        try
                                        {
                                            byte[] imageBytes = Convert.FromBase64String(att.ContentBase64);
                                            using (var msImg = new MemoryStream(imageBytes))
                                            {
                                                BitmapImage bmp = new BitmapImage();
                                                bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad;
                                                bmp.StreamSource = msImg; bmp.EndInit();
                                                Image img = new Image
                                                {
                                                    Source = bmp,
                                                    Width = 100,
                                                    Height = 100,
                                                    Margin = new Thickness(5)
                                                };
                                                messagesContainer.Children.Add(CreateImageBubble(new List<Image> { img }, Brushes.SeaGreen));
                                            }
                                        }
                                        catch (Exception) { }
                                    }
                                    else if (att.FileType == "audio")
                                    {
                                        // Audio bubble (inline or transfer)
                                        var audioBubble = CreateAudioBubble(att, senderEndpoint.Address, Brushes.SeaGreen);
                                        messagesContainer.Children.Add(audioBubble);
                                    }
                                    else if (att.IsFileTransfer)
                                    {
                                        // Clickable file transfer bubble
                                        string info = $"Attachment: {att.FileName} ({att.FileType}) - Port {att.TransferPort}";
                                        var bubble = CreateTextBubble(info, Brushes.White, Brushes.SeaGreen);
                                        bubble.Tag = new Tuple<Attachment, IPAddress>(att, senderEndpoint.Address);
                                        bubble.MouseLeftButtonUp += async (s, e) =>
                                        {
                                            var tup = (Tuple<Attachment, IPAddress>)((Border)s).Tag;
                                            var saved = await DownloadFileAsync(tup.Item1, tup.Item2);
                                            if (!string.IsNullOrEmpty(saved))
                                                MessageBox.Show($"File downloaded to: {saved}");
                                        };
                                        messagesContainer.Children.Add(bubble);
                                    }
                                }
                            }
                        }
                        else if (receivedMsg != null)
                        {
                            // Existing control messages (AudioTransferReady/AudioTransferStarted)
                            if (receivedMsg.ControlType == "AudioTransferReady")
                            {
                                // existing logic for initiating audio transfer
                            }
                            else if (receivedMsg.ControlType == "AudioTransferStarted")
                            {
                                // existing logic for updating audio bubble
                            }
                        }
                        else
                        {
                            // Plain text fallback
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

        public void SendMessage(string message, List<string> imageAttachments, List<string> audioAttachments, List<string> zipAttachments)
        {
            if (CurrentChatSession == null)
            {
                MessageBox.Show("No active chat session. Please select a user to chat with.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var chatMessage = new ChatMessage
            {
                SenderDisplayName = "Me",
                Text = message,
                Attachments = new List<Attachment>()
            };

            // Inline images
            foreach (var path in imageAttachments)
            {
                if (!File.Exists(path)) continue;
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
                catch (Exception) { }
            }

            // Audio and ZIP files
            foreach (var fileGroup in new[] { (Paths: audioAttachments, Type: "audio"), (Paths: zipAttachments, Type: "zip") })
            {
                foreach (var path in fileGroup.Paths)
                {
                    if (!File.Exists(path)) continue;
                    try
                    {
                        var fi = new FileInfo(path);
                        if (fi.Length > FileSizeThreshold)
                        {
                            // start transfer listener immediately
                            int port = StartTcpFileTransfer(path);
                            chatMessage.Attachments.Add(new Attachment
                            {
                                FileName = fi.Name,
                                FileType = fileGroup.Type,
                                IsFileTransfer = true,
                                TransferPort = port,
                                LocalFilePath = path
                            });
                        }
                        else
                        {
                            byte[] fileBytes = File.ReadAllBytes(path);
                            chatMessage.Attachments.Add(new Attachment
                            {
                                FileName = fi.Name,
                                FileType = fileGroup.Type,
                                ContentBase64 = Convert.ToBase64String(fileBytes),
                                IsFileTransfer = false
                            });
                        }
                    }
                    catch (Exception) { }
                }
            }

            // Send the chatMessage JSON
            string json = JsonSerializer.Serialize(chatMessage);
            byte[] data = Encoding.UTF8.GetBytes(json);
            try
            {
                using (var client = new TcpClient())
                {
                    client.Connect(CurrentChatSession.PeerEndpoint.Address, chatPort);
                    using (var ns = client.GetStream())
                    {
                        ns.Write(data, 0, data.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error sending message: " + ex.Message);
            }

            // Update UI
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Text bubble
                messagesContainer.Children.Add(CreateTextBubble(
                    $"Me: {message}", Brushes.White, Brushes.DodgerBlue));

                var outgoingImages = new List<Image>();
                // Show attachments
                foreach (var att in chatMessage.Attachments)
                {
                    if (att.FileType == "image" && !att.IsFileTransfer)
                    {
                        try
                        {
                            var imgBytes = Convert.FromBase64String(att.ContentBase64);
                            using var msImg = new MemoryStream(imgBytes);
                            var bmp = new BitmapImage(); bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.StreamSource = msImg; bmp.EndInit();
                            outgoingImages.Add(new Image { Source = bmp, Width = 100, Height = 100, Margin = new Thickness(5) });
                        }
                        catch (Exception) { }
                    }
                    else if (att.IsFileTransfer)
                    {
                        string info = $"Attachment: {att.FileName} ({att.FileType}) - Port {att.TransferPort}";
                        var bubble = CreateTextBubble(info, Brushes.White, Brushes.DodgerBlue);
                        bubble.Tag = new Tuple<Attachment, IPAddress>(att, IPAddress.Loopback);
                        bubble.MouseLeftButtonUp += async (s, e) =>
                        {
                            var tup = (Tuple<Attachment, IPAddress>)((Border)s).Tag;
                            var saved = await DownloadFileAsync(tup.Item1, tup.Item2);
                            if (!string.IsNullOrEmpty(saved))
                                MessageBox.Show($"File downloaded to: {saved}");
                        };
                        messagesContainer.Children.Add(bubble);
                    }
                }
                if (outgoingImages.Count > 0)
                {
                    messagesContainer.Children.Add(CreateImageBubble(outgoingImages, Brushes.DodgerBlue));
                }
            });
        }

        /// <summary>
        /// Starts a TCP listener on a random port and streams the file with an 8-byte length prefix.
        /// Returns the port number.
        /// </summary>
        private int StartTcpFileTransfer(string filePath)
        {
            var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            Task.Run(async () =>
            {
                try
                {
                    using var client = await listener.AcceptTcpClientAsync();
                    using var ns = client.GetStream();
                    using var fs = File.OpenRead(filePath);

                    // 8-byte length header
                    var lengthBytes = BitConverter.GetBytes(fs.Length);
                    await ns.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                    await fs.CopyToAsync(ns);
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
        /// Downloads a file via TCP given its TransferPort and saves locally.
        /// </summary>
        private async Task<string> DownloadFileAsync(Attachment att, IPAddress senderIP)
        {
            string filePath = Path.Combine(Path.GetTempPath(), att.FileName);
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(senderIP, att.TransferPort);
                using var ns = client.GetStream();

                // Read 8-byte length prefix
                byte[] lenBuf = new byte[8];
                int read = 0;
                while (read < 8)
                {
                    int r = await ns.ReadAsync(lenBuf, read, 8 - read);
                    if (r == 0) throw new IOException("Unexpected EOF reading length");
                    read += r;
                }
                long totalLength = BitConverter.ToInt64(lenBuf, 0);

                using var fs = File.Create(filePath);
                byte[] buffer = new byte[81920];
                long remaining = totalLength;
                while (remaining > 0)
                {
                    int chunk = await ns.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                    if (chunk == 0) break;
                    await fs.WriteAsync(buffer, 0, chunk);
                    remaining -= chunk;
                }
                if (remaining != 0)
                    throw new IOException($"Expected {totalLength} bytes but got {totalLength - remaining}");
                return filePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error downloading file: " + ex.Message);
                return string.Empty;
            }
        }

        // ... existing utility methods (GetOutgoingAttachmentByName, DownloadAudioFileAsync, SaveAudioFromBase64, SendControlMessage) remain unchanged or can be commented out if unused ...

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
            // unchanged original implementation...
            // comment out if not needed
            return null; // placeholder
        }

        private Border CreateAudioBubble(Attachment att, IPAddress senderIP, Brush bubbleBackground)
        {
            // unchanged original implementation...
            return null; // placeholder
        }
    }

    // Minimal stub of CircularProgressBar control unchanged
    public class CircularProgressBar : ContentControl
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(CircularProgressBar), new PropertyMetadata(0.0, OnValueChanged));
        public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(double), typeof(CircularProgressBar), new PropertyMetadata(0.0));
        public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(double), typeof(CircularProgressBar), new PropertyMetadata(100.0));
        public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CircularProgressBar cpb && cpb.Content is TextBlock tb)
                tb.Text = $"{cpb.Value:0}%";
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
