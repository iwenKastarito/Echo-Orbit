// ChatManager.cs
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EchoOrbit.Helpers
{
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
                    string message = Encoding.UTF8.GetString(result.Buffer);

                    // In a real app, you’d determine which chat session this belongs to,
                    // and decrypt using the shared key. Here we simply display the plain text.
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        messagesContainer.Children.Add(new TextBlock
                        {
                            Text = $"Peer: {message}",
                            Foreground = Brushes.LightGreen,
                            Margin = new Thickness(5)
                        });
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error receiving chat message: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Sends the given message (and attachments) to the currently active chat session.
        /// </summary>
        public void SendMessage(string message, List<string> imageAttachments, List<string> audioAttachments, List<string> zipAttachments)
        {
            if (CurrentChatSession == null)
            {
                MessageBox.Show("No active chat session. Please select a user to chat with.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // In a real application, encrypt the message with CurrentChatSession.SharedKey.
            byte[] plainBytes = Encoding.UTF8.GetBytes(message);

            // Send the message to the peer's endpoint.
            udpClient.Send(plainBytes, plainBytes.Length, CurrentChatSession.PeerEndpoint);

            // Also display the message in the UI.
            messagesContainer.Children.Add(new TextBlock
            {
                Text = $"Me: {message}",
                Foreground = Brushes.White,
                Margin = new Thickness(5)
            });
        }
    }
}
