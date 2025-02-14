using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Runtime.InteropServices;

namespace LocalMusicStreamingSecurity
{
    public class SecurityManager
    {
        #region Constants and Fields

        private const int BroadcastPort = 8888;
        private const string DISCOVER_REQUEST = "DISCOVER_REQUEST";
        private const string DISCOVER_RESPONSE = "DISCOVER_RESPONSE";

        // Store known peers’ public keys for key pinning.
        private Dictionary<string, byte[]> knownPublicKeys = new Dictionary<string, byte[]>();

        // ECDH for secure key exchange.
        private ECDiffieHellmanCng ecdh;
        public byte[] PublicKey { get; private set; }
        private byte[] sharedSecret;
        private byte[] aesKey;

        // AES for message encryption/decryption.
        private Aes aes;

        // Replay protection: store used nonces.
        private HashSet<string> usedNonces = new HashSet<string>();

        // Session key rotation timer (rotate every 5 minutes).
        private System.Timers.Timer keyRotationTimer;

        // Simulated WiFi fingerprint hash.
        private string wifiFingerprintHash;

        // UDP client for peer discovery.
        private UdpClient udpClient;

        #endregion

        #region Events

        /// <summary>
        /// Raised when a peer is discovered (and key exchange completed).
        /// Provides the remote endpoint and the peer's public key.
        /// </summary>
        public event Action<IPEndPoint, byte[]> PeerDiscovered;

        #endregion

        #region Constructor

        public SecurityManager()
        {
            // Initialize ECDH key exchange.
            ecdh = new ECDiffieHellmanCng
            {
                KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash,
                HashAlgorithm = CngAlgorithm.Sha256
            };
            PublicKey = ecdh.PublicKey.ToByteArray();

            // Initialize AES encryption (AES-256).
            aes = Aes.Create();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Simulate WiFi details and compute fingerprint hash.
            wifiFingerprintHash = ComputeSHA256(GetWiFiDetails());

            // Start key rotation timer (rotate every 5 minutes).
            keyRotationTimer = new System.Timers.Timer(5 * 60 * 1000);
            keyRotationTimer.Elapsed += (sender, e) => RotateSessionKey();
            keyRotationTimer.Start();

            // Initialize UDP client for peer discovery.
            udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, BroadcastPort));

            // Start listening for discovery messages on a background thread.
            Task.Run(() => ListenForDiscoveryMessages());

            try
            {
                // Get the local IPv4 address from the host name.
                var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress localIP = null;
                foreach (var ip in hostEntry.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        localIP = ip;
                        break;
                    }
                }
                if (localIP == null)
                {
                    localIP = IPAddress.Any;
                }
                udpClient.Client.Bind(new IPEndPoint(localIP, BroadcastPort));
                Console.WriteLine("UDP client bound to " + localIP + ":" + BroadcastPort);
            }
            catch (Exception ex)
            {
                Console.WriteLine("UDP Bind failed: " + ex.Message);
            }
        }

        #endregion

        #region WiFi Fingerprint Methods

        // Simulated method to obtain WiFi details.
        private string GetWiFiDetails()
        {
            // In a real application, retrieve actual WiFi details.
            return "SSID:MyWiFi;BSSID:00:11:22:33:44:55;Signal:-40;";
        }

        // Compute a SHA-256 hash of the input string.
        private string ComputeSHA256(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        #endregion

        #region UDP Peer Discovery

        /// <summary>
        /// Sends a discovery request via UDP broadcast.
        /// </summary>
        public void SendDiscoveryRequest()
        {
            string nonce = GenerateNonce();
            string message = string.Format("{0}|{1}|{2}|{3}|{4}",
                DISCOVER_REQUEST,
                Convert.ToBase64String(PublicKey),
                wifiFingerprintHash,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                nonce);
            byte[] data = Encoding.UTF8.GetBytes(message);
            IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);
            udpClient.Send(data, data.Length, broadcastEP);
            Console.WriteLine("Sent discovery request: " + message);
        }


        /// <summary>
        /// Continuously listens for discovery messages.
        /// </summary>
        public void ListenForDiscoveryMessages()
        {
            while (true)
            {
                try
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, BroadcastPort);
                    byte[] data = udpClient.Receive(ref remoteEP);
                    string message = Encoding.UTF8.GetString(data);
                    HandleDiscoveryMessage(message, remoteEP);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error in discovery listener: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Processes incoming discovery messages.
        /// </summary>
        private void HandleDiscoveryMessage(string message, IPEndPoint remoteEP)
        {
            Console.WriteLine($"Received discovery message from {remoteEP}: {message}");
            // Expected format: Type|PublicKey|WiFiHash|Timestamp|Nonce
            string[] parts = message.Split('|');
            if (parts.Length < 5)
                return; // Invalid message.

            string type = parts[0];
            string receivedPublicKeyBase64 = parts[1];
            string receivedWifiHash = parts[2];
            long timestamp = long.Parse(parts[3]);
            string nonce = parts[4];

            if (!IsValidNonce(nonce) || IsTimestampStale(timestamp))
            {
                Console.WriteLine("Replay attack detected or timestamp is stale.");
                return;
            }
            MarkNonceUsed(nonce);

            byte[] receivedPublicKey = Convert.FromBase64String(receivedPublicKeyBase64);

            if (type == DISCOVER_REQUEST)
            {
                // Respond with our public key and WiFi fingerprint hash.
                string response = string.Format("{0}|{1}|{2}|{3}|{4}",
                    DISCOVER_RESPONSE,
                    Convert.ToBase64String(PublicKey),
                    wifiFingerprintHash,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    GenerateNonce());
                byte[] responseData = Encoding.UTF8.GetBytes(response);
                udpClient.Send(responseData, responseData.Length, remoteEP);
            }
            else if (type == DISCOVER_RESPONSE)
            {
                // Verify WiFi fingerprint.
                if (receivedWifiHash != wifiFingerprintHash)
                {
                    Console.WriteLine("WiFi fingerprint mismatch. Possible attack.");
                    return;
                }

                string peerId = remoteEP.ToString();
                if (knownPublicKeys.ContainsKey(peerId))
                {
                    if (!AreKeysEqual(knownPublicKeys[peerId], receivedPublicKey))
                    {
                        Console.WriteLine("Public key change detected for peer {0}.", peerId);
                        return;
                    }
                }
                else
                {
                    knownPublicKeys[peerId] = receivedPublicKey;
                }

                EstablishSharedSecret(receivedPublicKey);

                // Notify subscribers about the discovered peer.
                PeerDiscovered?.Invoke(remoteEP, receivedPublicKey);
            }
        }

        #endregion

        #region Replay Protection

        private bool IsValidNonce(string nonce)
        {
            return !usedNonces.Contains(nonce);
        }

        private void MarkNonceUsed(string nonce)
        {
            usedNonces.Add(nonce);
        }

        private bool IsTimestampStale(long timestamp)
        {
            long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return Math.Abs(currentTimestamp - timestamp) > 60;
        }

        private string GenerateNonce()
        {
            byte[] nonceBytes = new byte[16];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonceBytes);
            }
            return Convert.ToBase64String(nonceBytes);
        }

        #endregion

        #region Secure Key Exchange and AES Encryption

        /// <summary>
        /// Establishes a shared secret using ECDH key exchange.
        /// </summary>
        public void EstablishSharedSecret(byte[] peerPublicKey)
        {
            try
            {
                using (CngKey peerKey = CngKey.Import(peerPublicKey, CngKeyBlobFormat.EccPublicBlob))
                {
                    sharedSecret = ecdh.DeriveKeyMaterial(peerKey);
                }
                using (SHA256 sha256 = SHA256.Create())
                {
                    aesKey = sha256.ComputeHash(sharedSecret);
                }
                Console.WriteLine("Shared secret established and AES key derived.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in key exchange: " + ex.Message);
            }
        }

        /// <summary>
        /// Encrypts a plaintext message using AES-256.
        /// The IV is generated and prepended to the ciphertext.
        /// </summary>
        public byte[] EncryptMessage(string plainText)
        {
            aes.GenerateIV();
            ICryptoTransform encryptor = aes.CreateEncryptor(aesKey, aes.IV);
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(aes.IV, 0, aes.IV.Length);
                using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    cs.Write(plainBytes, 0, plainBytes.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Decrypts a ciphertext message using AES-256.
        /// Expects the IV to be prepended to the ciphertext.
        /// </summary>
        public string DecryptMessage(byte[] cipherData)
        {
            byte[] iv = new byte[aes.BlockSize / 8];
            Array.Copy(cipherData, 0, iv, 0, iv.Length);
            ICryptoTransform decryptor = aes.CreateDecryptor(aesKey, iv);
            using (MemoryStream ms = new MemoryStream(cipherData, iv.Length, cipherData.Length - iv.Length))
            {
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    byte[] plainBytes = new byte[cipherData.Length];
                    int decryptedByteCount = cs.Read(plainBytes, 0, plainBytes.Length);
                    return Encoding.UTF8.GetString(plainBytes, 0, decryptedByteCount);
                }
            }
        }

        #endregion

        #region Session Key Rotation

        private void RotateSessionKey()
        {
            Console.WriteLine("Rotating session key...");
            sharedSecret = null;
            aesKey = null;
            // Optionally: trigger a re-discovery or key exchange with peers.
        }

        #endregion

        #region Evil Twin WiFi Detection

        public bool VerifyWiFiBSSID(string currentBSSID)
        {
            string storedBSSID = "00:11:22:33:44:55"; // Replace with actual stored BSSID.
            return string.Equals(currentBSSID, storedBSSID, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Secure Private Key Storage Using DPAPI

        public byte[] ProtectPrivateKey(byte[] privateKeyBytes)
        {
            return ProtectedData.Protect(privateKeyBytes, null, DataProtectionScope.CurrentUser);
        }

        public byte[] UnprotectPrivateKey(byte[] protectedBytes)
        {
            return ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        }

        #endregion

        #region Utility Methods

        private bool AreKeysEqual(byte[] key1, byte[] key2)
        {
            if (key1.Length != key2.Length)
                return false;
            for (int i = 0; i < key1.Length; i++)
            {
                if (key1[i] != key2[i])
                    return false;
            }
            return true;
        }

        #endregion
    }
}
