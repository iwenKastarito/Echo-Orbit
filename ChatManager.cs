using System;
using System.Collections.Generic;
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

        // Threshold (in bytes) above which a file is sent via a separate TCP file transfer.
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
                        // If deserialization fails, treat the data as plain text.
                    }
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (receivedMsg != null)
                        {
                            // Display the text message in a bubble.
                            messagesContainer.Children.Add(CreateTextBubble(
                                $"{receivedMsg.SenderDisplayName}: {receivedMsg.Text}",
                                Brushes.White,
                                Brushes.SeaGreen));

                            if (receivedMsg.Attachments != null)
                            {
                                foreach (var att in receivedMsg.Attachments)
                                {
                                    if (att.IsFileTransfer && (att.FileType == "audio" || att.FileType == "zip"))
                                    {
                                        // Create clickable text to download the file.
                                        TextBlock downloadBlock = new TextBlock
                                        {
                                            Text = $"File '{att.FileName}' available. Click here to download.",
                                            Foreground = Brushes.LightBlue,
                                            Margin = new Thickness(5),
                                            Cursor = System.Windows.Input.Cursors.Hand
                                        };
                                        downloadBlock.MouseLeftButtonUp += (s, e) =>
                                        {
                                            // Use the sender's IP from the client socket.
                                            DownloadFile(att, ((IPEndPoint)client.Client.RemoteEndPoint).Address);
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
                                                messagesContainer.Children.Add(img);
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
                            // For large audio files, use TCP file transfer.
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

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Display outgoing message in a bubble.
                messagesContainer.Children.Add(CreateTextBubble(
                    $"Me: {message}",
                    Brushes.White,
                    Brushes.DodgerBlue));

                if (chatMessage.Attachments != null)
                {
                    foreach (var att in chatMessage.Attachments)
                    {
                        string attInfo = att.IsFileTransfer
                            ? $"{att.FileName} ({att.FileType}) - Transfer on port {att.TransferPort}"
                            : $"{att.FileName} ({att.FileType})";
                        messagesContainer.Children.Add(CreateTextBubble(
                            "Attachment: " + attInfo,
                            Brushes.White,
                            Brushes.DodgerBlue));
                    }
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
                TextWrapping = TextWrapping.Wrap
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
