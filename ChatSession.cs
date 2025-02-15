// ChatSession.cs
using System.Net;
using EchoOrbit.Controls;  // Import the OnlineUser class

namespace EchoOrbit.Helpers
{
    public class ChatSession
    {
        /// <summary>
        /// The peer we are chatting with.
        /// </summary>
        public OnlineUser Peer { get; set; }

        /// <summary>
        /// The network endpoint of the peer.
        /// In a real app this would be discovered automatically.
        /// </summary>
        public IPEndPoint PeerEndpoint { get; set; }

        /// <summary>
        /// The shared symmetric key established via SecurityManager.
        /// For this demo, it is not used.
        /// </summary>
        public byte[] SharedKey { get; set; }

        public ChatSession(OnlineUser user)
        {
            Peer = user;
            PeerEndpoint = user.PeerEndpoint;
        }
    }
}
