using System;
using System.IO;
using System.Windows.Media.Imaging;

public static class ImageHelper
{
    public static string GetThumbnailBase64(string imagePath, int width, int height)
    {
        try
        {
            // Load the image with decoding options to reduce memory usage.
            BitmapImage bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(imagePath, UriKind.Absolute);
            bmp.DecodePixelWidth = width;   // Force a small width.
            bmp.DecodePixelHeight = height; // Force a small height.
            bmp.EndInit();
            bmp.Freeze();

            // Encode the BitmapImage to PNG format.
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                byte[] imageBytes = stream.ToArray();
                return Convert.ToBase64String(imageBytes);
            }
        }
        catch (Exception ex)
        {
            // If something fails, return an empty string.
            Console.WriteLine("Error generating thumbnail: " + ex.Message);
            return "";
        }
    }
}
