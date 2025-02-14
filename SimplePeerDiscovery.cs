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

        public SimplePeerDiscovery()
        {
            // Create a unique ID for this peer (you could also use the local IP, etc.)
            myId = Guid.NewGuid().ToString();
            // Initialize the UDP client.
            udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;
            // Bind to any available interface on the broadcast port.
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, BroadcastPort));
        }

        /// <summary>
        /// Starts listening for broadcast messages and immediately broadcasts our presence.
        /// </summary>
        public void Start()
        {
            // Start the listening loop in a background task.
            Task.Run(() => ListenForBroadcasts());
            // Immediately broadcast our presence.
            BroadcastPresence();
        }

        /// <summary>
        /// Sends a broadcast message with our unique ID.
        /// </summary>
        private void BroadcastPresence()
        {
            string message = "HELLO|" + myId;
            byte[] data = Encoding.UTF8.GetBytes(message);
            IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);
            udpClient.Send(data, data.Length, broadcastEP);
            Console.WriteLine("Broadcasted: " + message);
        }

        /// <summary>
        /// Listens continuously for UDP broadcast messages.
        /// </summary>
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
                    if (parts.Length < 2)
                        continue;
                    string type = parts[0];
                    string peerId = parts[1];

                    // Ignore our own broadcast.
                    if (peerId == myId)
                        continue;

                    if (!discoveredPeers.Contains(peerId))
                    {
                        discoveredPeers.Add(peerId);
                        Console.WriteLine("Discovered new peer: " + peerId);
                        // Rebroadcast our presence so the new peer (and others) are aware.
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
