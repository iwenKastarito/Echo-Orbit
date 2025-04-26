using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace EchoOrbit.Helpers
{
    public static class TCPFileHandler
    {
        public static int StartFileTransferServer(string filePath, IProgress<float> progress = null)
        {
            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Any, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task.Run(async () =>
                {
                    try
                    {
                        using (TcpClient client = await listener.AcceptTcpClientAsync())
                        using (NetworkStream ns = client.GetStream())
                        using (FileStream fs = File.OpenRead(filePath))
                        {
                            long totalBytes = fs.Length;
                            long bytesSent = 0;
                            byte[] buffer = new byte[8192];
                            int bytesRead;
                            while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                await ns.WriteAsync(buffer, 0, bytesRead);
                                bytesSent += bytesRead;
                                progress?.Report((float)bytesSent / totalBytes);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"File transfer server error for '{filePath}': {ex.Message}");
                    }
                    finally
                    {
                        listener.Stop();
                    }
                });
                return port;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting file transfer server for '{filePath}': {ex.Message}");
                listener?.Stop();
                return 0;
            }
        }

        public static async Task DownloadFileAsync(IPAddress senderIP, int port, string filePath, IProgress<float> progress = null)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync(senderIP, port);
                    using (NetworkStream ns = client.GetStream())
                    using (FileStream fs = File.Create(filePath))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        long bytesReceived = 0;
                        FileInfo fi = new FileInfo(filePath);
                        while ((bytesRead = await ns.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fs.WriteAsync(buffer, 0, bytesRead);
                            bytesReceived += bytesRead;
                            if (fi.Length > 0)
                                progress?.Report((float)bytesReceived / fi.Length);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading file to '{filePath}': {ex.Message}");
                throw;
            }
        }
    }
}