using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LocalNetworkTest
{
    public class SimplePeerDiscovery
    {
        private const int BroadcastPort = 8888;
        private UdpClient udpClient;
        private string myId;
        private HashSet<string> discoveredPeers = new HashSet<string>();

        // Update event to include display name and profile image data.
        public event Action<IPEndPoint, string, string, string> PeerDiscovered;

        // New properties to hold this user's display name and profile image.
        // (For a real app you might load these from user settings.)
        public string DisplayName { get; set; }
        public string ProfileImageBase64 { get; set; }

        public SimplePeerDiscovery()
        {
            // Create a unique ID for this peer.
            myId = Guid.NewGuid().ToString();

            // Set default display name (you could allow the user to change this).
            DisplayName = "User-" + myId.Substring(0, 4);

            // For demonstration, you can convert a small image to Base64.
            // If you have a file "defaultProfile.png" in your app folder, for example:
            // ProfileImageBase64 = Convert.ToBase64String(File.ReadAllBytes("defaultProfile.png"));
            // For now, we'll leave it empty so that the receiver will use a default image.
            ProfileImageBase64 = "";

            udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, BroadcastPort));
        }

        public void Start()
        {
            Task.Run(() => ListenForBroadcasts());
            BroadcastPresence();
        }

        private void BroadcastPresence()
        {
            // Build the message with id, display name, and profile image.
            string message = "HELLO|" + myId + "|" + DisplayName + "|" + ProfileImageBase64;
            byte[] data = Encoding.UTF8.GetBytes(message);
            IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);
            udpClient.Send(data, data.Length, broadcastEP);
            Console.WriteLine("Broadcasted: " + message);
        }

        private void ListenForBroadcasts()
        {
            while (true)
            {
                try
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, BroadcastPort);
                    byte[] data = udpClient.Receive(ref remoteEP);
                    string message = Encoding.UTF8.GetString(data);
                    Console.WriteLine("Received: " + message + " from " + remoteEP);

                    string[] parts = message.Split('|');
                    if (parts.Length < 4)
                        continue;
                    string type = parts[0];
                    string peerId = parts[1];
                    string displayName = parts[2];
                    string profileImageBase64 = parts[3];

                    // Ignore our own broadcast.
                    if (peerId == myId)
                        continue;

                    if (!discoveredPeers.Contains(peerId))
                    {
                        discoveredPeers.Add(peerId);
                        Console.WriteLine("Discovered new peer: " + peerId);
                        // Fire the event with all needed data.
                        PeerDiscovered?.Invoke(remoteEP, peerId, displayName, profileImageBase64);
                        // Rebroadcast our presence so the new peer is aware.
                        BroadcastPresence();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error in peer discovery: " + ex.Message);
                }
            }
        }
    }
}
