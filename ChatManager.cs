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
        /// For large files sent via TCP, this can be empty.
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

        // We'll use a UDP client on a fixed port for chat messages.
        private UdpClient udpClient;
        private int chatPort = 8890; // arbitrary port for chat messages

        // Threshold in bytes to decide if a file is “large” (e.g. music).
        // For demonstration, assume files larger than 100 KB should use TCP.
        private const long FileSizeThreshold = 100 * 1024;

        public ChatManager(StackPanel container, MusicController musicController)
        {
            this.messagesContainer = container;
            this.musicController = musicController;
            udpClient = new UdpClient(chatPort);
            // Start listening for incoming messages.
            Task.Run(() => ListenForMessages());
        }

        private async Task ListenForMessages()
        {
            while (true)
            {
                try
                {
                    UdpReceiveResult result = await udpClient.ReceiveAsync();
                    // Capture the sender's endpoint (for file transfer later).
                    IPEndPoint senderEndpoint = result.RemoteEndPoint;
                    string json = Encoding.UTF8.GetString(result.Buffer);

                    ChatMessage receivedMsg = null;
                    try
                    {
                        receivedMsg = JsonSerializer.Deserialize<ChatMessage>(json);
                    }
                    catch (Exception)
                    {
                        // If JSON deserialization fails, treat it as plain text.
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            messagesContainer.Children.Add(new TextBlock
                            {
                                Text = $"Peer: {json}",
                                Foreground = Brushes.LightGreen,
                                Margin = new Thickness(5)
                            });
                        });
                        continue;
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Display the text message.
                        messagesContainer.Children.Add(new TextBlock
                        {
                            Text = $"{receivedMsg.SenderDisplayName}: {receivedMsg.Text}",
                            Foreground = Brushes.LightGreen,
                            Margin = new Thickness(5)
                        });

                        // Process attachments.
                        if (receivedMsg.Attachments != null)
                        {
                            foreach (var att in receivedMsg.Attachments)
                            {
                                if (att.IsFileTransfer && att.FileType == "audio")
                                {
                                    // Create a clickable text element to download the file.
                                    TextBlock downloadBlock = new TextBlock
                                    {
                                        Text = $"Audio file '{att.FileName}' available. Click here to download.",
                                        Foreground = Brushes.LightBlue,
                                        Margin = new Thickness(5),
                                        Cursor = System.Windows.Input.Cursors.Hand
                                    };
                                    downloadBlock.MouseLeftButtonUp += (s, e) =>
                                    {
                                        // Initiate file download using the sender's IP and provided transfer port.
                                        DownloadFile(att, senderEndpoint.Address);
                                    };
                                    messagesContainer.Children.Add(downloadBlock);
                                }
                                else if (att.FileType == "image" && !att.IsFileTransfer)
                                {
                                    // For images sent inline.
                                    try
                                    {
                                        byte[] imageBytes = Convert.FromBase64String(att.ContentBase64);
                                        using (var ms = new MemoryStream(imageBytes))
                                        {
                                            BitmapImage bmp = new BitmapImage();
                                            bmp.BeginInit();
                                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                                            bmp.StreamSource = ms;
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
                                        Console.WriteLine("Error displaying image attachment: " + ex.Message);
                                    }
                                }
                                else
                                {
                                    // For other inline attachments (zip, etc.)
                                    messagesContainer.Children.Add(new TextBlock
                                    {
                                        Text = $"Attachment: {att.FileName} ({att.FileType})",
                                        Foreground = Brushes.LightGray,
                                        Margin = new Thickness(5)
                                    });
                                }
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error receiving chat message: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Sends a chat message with attachments.
        /// For large files (exceeding FileSizeThreshold), use TCP file transfer.
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
                SenderDisplayName = "Me", // Replace with actual sender's name if available.
                Text = message,
                Attachments = new List<Attachment>()
            };

            // Process image attachments (assume these are small enough to send inline).
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
                        Console.WriteLine("Error reading image attachment: " + ex.Message);
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
                                ContentBase64 = "" // No inline content.
                            });
                        }
                        else
                        {
                            // For small files, send inline.
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
                        Console.WriteLine("Error reading audio attachment: " + ex.Message);
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
                            // For large zip files, start a TCP file transfer.
                            int port = StartTcpFileTransfer(path);
                            chatMessage.Attachments.Add(new Attachment
                            {
                                FileName = Path.GetFileName(path),
                                FileType = "zip",
                                IsFileTransfer = true,
                                TransferPort = port,
                                ContentBase64 = "" // Not sending inline.
                            });
                        }
                        else
                        {
                            // For small zip files, send inline.
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
                        Console.WriteLine("Error reading zip attachment: " + ex.Message);
                    }
                }
            }


            // Serialize the chat message to JSON.
            string json = JsonSerializer.Serialize(chatMessage);
            byte[] data = Encoding.UTF8.GetBytes(json);
            udpClient.Send(data, data.Length, CurrentChatSession.PeerEndpoint);

            // Also update the UI.
            Application.Current.Dispatcher.Invoke(() =>
            {
                messagesContainer.Children.Add(new TextBlock
                {
                    Text = $"Me: {message}",
                    Foreground = Brushes.White,
                    Margin = new Thickness(5)
                });
                if (chatMessage.Attachments != null)
                {
                    foreach (var att in chatMessage.Attachments)
                    {
                        string attInfo = att.IsFileTransfer
                            ? $"{att.FileName} ({att.FileType}) - File transfer on port {att.TransferPort}"
                            : $"{att.FileName} ({att.FileType})";
                        messagesContainer.Children.Add(new TextBlock
                        {
                            Text = "Attachment: " + attInfo,
                            Foreground = Brushes.White,
                            Margin = new Thickness(5)
                        });
                    }
                }
            });
        }

        /// <summary>
        /// Starts a TCP listener on a free port to send the file.
        /// Returns the port number.
        /// </summary>
        private int StartTcpFileTransfer(string filePath)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 0); // Let OS choose free port.
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            // Launch a task to accept a single connection and send the file.
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
        /// Initiates a TCP connection to download a file from the sender.
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
                        // Save the downloaded file to a temporary path.
                        string filePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), att.FileName);
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
