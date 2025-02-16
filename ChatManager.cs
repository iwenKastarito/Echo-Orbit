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

        // UDP client used for chat messages.
        private UdpClient udpClient;
        private int chatPort = 8890; // arbitrary UDP port for chat messages

        // Threshold in bytes to decide if a file should be sent via TCP.
        // Files larger than this (e.g., 100 KB) will be transferred over TCP.
        private const long FileSizeThreshold = 100 * 1024;

        public ChatManager(StackPanel container, MusicController musicController)
        {
            this.messagesContainer = container;
            this.musicController = musicController;
            udpClient = new UdpClient(chatPort);
            // Start listening for incoming UDP messages.
            Task.Run(() => ListenForMessages());
        }

        private async Task ListenForMessages()
        {
            while (true)
            {
                try
                {
                    UdpReceiveResult result = await udpClient.ReceiveAsync();
                    // Capture the sender's endpoint (used later for TCP file transfer).
                    IPEndPoint senderEndpoint = result.RemoteEndPoint;
                    string json = Encoding.UTF8.GetString(result.Buffer);

                    ChatMessage receivedMsg = null;
                    try
                    {
                        receivedMsg = JsonSerializer.Deserialize<ChatMessage>(json);
                    }
                    catch (Exception)
                    {
                        // If deserialization fails, treat as plain text.
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
                        // Display the text part of the message.
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
                                        DownloadFile(att, senderEndpoint.Address);
                                    };
                                    messagesContainer.Children.Add(downloadBlock);
                                }
                                else if (att.FileType == "image" && !att.IsFileTransfer)
                                {
                                    // Display inline image attachments.
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
                                        Console.WriteLine("Error displaying image: " + ex.Message);
                                    }
                                }
                                else
                                {
                                    // For other inline attachments.
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
        /// For files larger than the threshold, uses TCP file transfer.
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
                SenderDisplayName = "Me", // Replace with your actual display name if available.
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
                            // For large audio files (which can be up to 500 MB), use TCP.
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
                            // Use TCP for large zip files.
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
                            ? $"{att.FileName} ({att.FileType}) - Transfer on port {att.TransferPort}"
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
        /// Starts a TCP listener on a free port to transfer the file.
        /// This method streams the file in chunks so that it can support large files (up to 500 MB or more).
        /// Returns the TCP port number for the file transfer.
        /// </summary>
        private int StartTcpFileTransfer(string filePath)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 0); // OS selects free port.
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            // Start accepting a connection and streaming the file.
            Task.Run(() =>
            {
                try
                {
                    using (TcpClient client = listener.AcceptTcpClient())
                    using (NetworkStream ns = client.GetStream())
                    using (FileStream fs = File.OpenRead(filePath))
                    {
                        // Copy the file stream to the network stream in chunks.
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
                        // Save the file to a temporary location.
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
