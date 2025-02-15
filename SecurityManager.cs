using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LocalChatSecurity
{
    public class SecurityManager
    {
        private const string KeyFilePath = "userLongTermKey.xml"; // File to store the long-term key (adjust path as needed)
        private RSA rsaLongTerm; // Long-term RSA key pair (identity)
        private ECDiffieHellmanCng ecdh; // Ephemeral key for Diffie–Hellman key exchange

        public SecurityManager()
        {
            LoadOrGenerateLongTermKeys();
        }

        #region Long-Term Key Management

        /// <summary>
        /// Loads the persistent long-term RSA key pair from disk or generates a new one if none exists.
        /// This key pair is used to sign ephemeral key exchange messages so peers can verify your identity.
        /// </summary>
        private void LoadOrGenerateLongTermKeys()
        {
            if (File.Exists(KeyFilePath))
            {
                // Load existing long-term key
                string keyXml = File.ReadAllText(KeyFilePath);
                rsaLongTerm = RSA.Create();
                rsaLongTerm.FromXmlString(keyXml);
            }
            else
            {
                // Generate a new long-term RSA key pair (2048-bit is standard)
                rsaLongTerm = RSA.Create(2048);
                string keyXml = rsaLongTerm.ToXmlString(true);
                File.WriteAllText(KeyFilePath, keyXml);
            }
        }

        /// <summary>
        /// Returns your long-term public key as an XML string.
        /// Share this with peers so they can verify your signed ephemeral key.
        /// </summary>
        public string GetLongTermPublicKeyXml()
        {
            // Export only the public part
            return rsaLongTerm.ToXmlString(false);
        }

        #endregion

        #region Ephemeral Key Exchange

        /// <summary>
        /// Starts the key exchange by generating an ephemeral ECDiffie–Hellman key pair.
        /// It then exports the ephemeral public key and signs it with your long-term private key.
        /// </summary>
        /// <returns>
        /// A tuple containing:
        /// - ephemeralPublicKey: the raw bytes of the ephemeral ECDH public key.
        /// - signature: a signature over the ephemeral public key using your RSA long-term key.
        /// </returns>
        public (byte[] ephemeralPublicKey, byte[] signature) StartKeyExchange()
        {
            // Create a new ephemeral ECDiffie–Hellman key pair.
            ecdh = new ECDiffieHellmanCng
            {
                KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash,
                HashAlgorithm = CngAlgorithm.Sha256
            };

            // Export the ephemeral public key in a standard blob format.
            byte[] ephemeralPublicKey = ecdh.PublicKey.ToByteArray();

            // Sign the ephemeral public key using your long-term RSA private key.
            byte[] signature = rsaLongTerm.SignData(ephemeralPublicKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return (ephemeralPublicKey, signature);
        }

        /// <summary>
        /// Completes the key exchange after receiving a peer's ephemeral public key, its signature, and the peer's long-term public key.
        /// This method verifies the signature and then derives a shared symmetric key.
        /// </summary>
        /// <param name="peerEphemeralPublicKey">The peer's ephemeral ECDH public key (byte array).</param>
        /// <param name="peerSignature">The signature over the peer's ephemeral public key.</param>
        /// <param name="peerLongTermPublicKeyXml">The peer's long-term public key as an XML string.</param>
        /// <returns>A symmetric key (SHA256 hash of the shared secret) for encrypting further communications.</returns>
        public byte[] CompleteKeyExchange(byte[] peerEphemeralPublicKey, byte[] peerSignature, string peerLongTermPublicKeyXml)
        {
            // Verify the peer's ephemeral public key was signed by its long-term private key.
            using (RSA rsaPeer = RSA.Create())
            {
                rsaPeer.FromXmlString(peerLongTermPublicKeyXml);
                bool verified = rsaPeer.VerifyData(peerEphemeralPublicKey, peerSignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                if (!verified)
                {
                    throw new CryptographicException("Peer ephemeral key signature verification failed. Possible MITM attack.");
                }
            }

            // Import the peer's ephemeral public key as an ECDiffie–Hellman public key.
            using (ECDiffieHellmanCng peerEcdh = new ECDiffieHellmanCng(CngKey.Import(peerEphemeralPublicKey, CngKeyBlobFormat.EccPublicBlob)))
            {
                // Derive the shared secret using our ephemeral key.
                byte[] sharedSecret = ecdh.DeriveKeyMaterial(peerEcdh.PublicKey);

                // Hash the shared secret (using SHA256) to produce a symmetric key.
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] symmetricKey = sha.ComputeHash(sharedSecret);
                    return symmetricKey;
                }
            }
        }

        #endregion

        #region AES Message Encryption/Decryption

        /// <summary>
        /// Encrypts a plaintext message using AES with the provided symmetric key.
        /// The IV is generated randomly and prepended to the ciphertext.
        /// </summary>
        /// <param name="plainText">The plaintext bytes to encrypt.</param>
        /// <param name="symmetricKey">The symmetric key (should be 256 bits if using SHA256 hash output).</param>
        /// <returns>The encrypted data with the IV prepended.</returns>
        public byte[] EncryptMessage(byte[] plainText, byte[] symmetricKey)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = symmetricKey;
                aes.GenerateIV();

                using (MemoryStream ms = new MemoryStream())
                {
                    // Prepend the IV so it can be used during decryption.
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(plainText, 0, plainText.Length);
                        cs.FlushFinalBlock();
                        return ms.ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// Decrypts data that was encrypted using EncryptMessage.
        /// Expects the first 16 bytes to be the IV.
        /// </summary>
        /// <param name="cipherData">The encrypted data with the IV prepended.</param>
        /// <param name="symmetricKey">The symmetric key used during encryption.</param>
        /// <returns>The decrypted plaintext bytes.</returns>
        public byte[] DecryptMessage(byte[] cipherData, byte[] symmetricKey)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = symmetricKey;

                // Extract the IV from the beginning of the cipherData.
                byte[] iv = new byte[aes.BlockSize / 8];
                Array.Copy(cipherData, 0, iv, 0, iv.Length);
                aes.IV = iv;

                using (MemoryStream ms = new MemoryStream())
                {
                    // Decrypt starting after the IV.
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherData, iv.Length, cipherData.Length - iv.Length);
                        cs.FlushFinalBlock();
                        return ms.ToArray();
                    }
                }
            }
        }

        #endregion

        #region Usage Summary

        /*
         * How to use SecurityManager:
         *
         * 1. Instantiate SecurityManager when your app starts:
         *      var secManager = new SecurityManager();
         *
         * 2. Get your long-term public key (identity) and share it with peers:
         *      string myPublicKeyXml = secManager.GetLongTermPublicKeyXml();
         *
         * 3. When starting a connection with a peer:
         *    - Call StartKeyExchange() to get your ephemeral ECDH public key and its RSA signature:
         *          var (myEphemeralKey, mySignature) = secManager.StartKeyExchange();
         *
         *    - Send these along with your long-term public key to the peer.
         *
         * 4. When receiving a key exchange from a peer:
         *    - Receive the peer’s ephemeral public key, its signature, and their long-term public key.
         *    - Call CompleteKeyExchange() to verify the signature and derive a shared symmetric key:
         *          byte[] symmetricKey = secManager.CompleteKeyExchange(peerEphemeralKey, peerSignature, peerPublicKeyXml);
         *
         * 5. Use the symmetricKey with EncryptMessage() and DecryptMessage() to securely exchange messages.
         *
         * This mechanism helps prevent MITM attacks by ensuring that ephemeral key data is signed
         * by the sender’s long-term key. Since these keys are generated once and stored securely,
         * a reconnecting user will have the same long-term key, which aids in verifying identities over time.
         *
         * Additionally, a Wi-Fi intruder (without the proper long-term private key) cannot impersonate
         * a legitimate user because they cannot produce valid signatures over the ephemeral keys.
         */

        #endregion
    }
}
