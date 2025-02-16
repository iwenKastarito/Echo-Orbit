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
        /// The file content encoded as a Base64 string.
        /// </summary>
        public string ContentBase64 { get; set; }
    }

    public class ChatManager
    {
        private StackPanel messagesContainer;
        private MusicController musicController;

        /// <summary>
        /// The current active chat session.
        /// </summary>
        public ChatSession CurrentChatSession { get; set; }

        // We'll use a UDP client on a fixed port for demonstration.
        private UdpClient udpClient;
        private int chatPort = 8890; // arbitrary port for chat messages

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
                    string json = Encoding.UTF8.GetString(result.Buffer);

                    // Try to deserialize the message as JSON.
                    ChatMessage receivedMsg = null;
                    try
                    {
                        receivedMsg = JsonSerializer.Deserialize<ChatMessage>(json);
                    }
                    catch (Exception)
                    {
                        // If JSON deserialization fails, assume it's a plain text message.
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

                        // Process and display each attachment.
                        if (receivedMsg.Attachments != null)
                        {
                            foreach (var att in receivedMsg.Attachments)
                            {
                                messagesContainer.Children.Add(new TextBlock
                                {
                                    Text = $"Attachment: {att.FileName} ({att.FileType})",
                                    Foreground = Brushes.LightGray,
                                    Margin = new Thickness(5)
                                });

                                // If the attachment is an image, display it as a thumbnail.
                                if (att.FileType == "image")
                                {
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
        /// Sends a chat message with attachments (images, audio, zip files) to the currently active chat session.
        /// </summary>
        public void SendMessage(string message, List<string> imageAttachments, List<string> audioAttachments, List<string> zipAttachments)
        {
            if (CurrentChatSession == null)
            {
                MessageBox.Show("No active chat session. Please select a user to chat with.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create the chat message object.
            ChatMessage chatMessage = new ChatMessage
            {
                SenderDisplayName = "Me", // Replace with actual sender's name if available.
                Text = message,
                Attachments = new List<Attachment>()
            };

            // Process image attachments.
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
                            ContentBase64 = Convert.ToBase64String(fileBytes)
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
                        byte[] fileBytes = File.ReadAllBytes(path);
                        chatMessage.Attachments.Add(new Attachment
                        {
                            FileName = Path.GetFileName(path),
                            FileType = "audio",
                            ContentBase64 = Convert.ToBase64String(fileBytes)
                        });
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
                        byte[] fileBytes = File.ReadAllBytes(path);
                        chatMessage.Attachments.Add(new Attachment
                        {
                            FileName = Path.GetFileName(path),
                            FileType = "zip",
                            ContentBase64 = Convert.ToBase64String(fileBytes)
                        });
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

            // Send the JSON data over UDP.
            udpClient.Send(data, data.Length, CurrentChatSession.PeerEndpoint);

            // Also display the sent message and attachment info in the UI.
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
                        messagesContainer.Children.Add(new TextBlock
                        {
                            Text = $"Attachment: {att.FileName} ({att.FileType})",
                            Foreground = Brushes.White,
                            Margin = new Thickness(5)
                        });
                    }
                }
            });
        }
    }
}
