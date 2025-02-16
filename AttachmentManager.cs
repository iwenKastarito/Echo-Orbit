using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace EchoOrbit
{
    public class AttachmentManager
    {
        public List<object> ImageAttachments { get; private set; } = new List<object>();
        public List<string> AudioAttachments { get; private set; } = new List<string>();
        public List<string> ZipAttachments { get; private set; } = new List<string>();
        public IPAddress SenderIP { get; set; }



        private StackPanel attachmentsSummaryPanel;
        private Border imageIndicator;
        private TextBlock imageCount;
        private Border audioIndicator;
        private TextBlock audioCount;
        private Border zipIndicator;
        private TextBlock zipCount;



        public AttachmentManager(StackPanel summaryPanel, Border imgIndicator, TextBlock imgCount,
            Border audioIndicator, TextBlock audioCount, Border zipIndicator, TextBlock zipCount)
        {
            attachmentsSummaryPanel = summaryPanel;
            this.imageIndicator = imgIndicator;
            this.imageCount = imgCount;
            this.audioIndicator = audioIndicator;
            this.audioCount = audioCount;
            this.zipIndicator = zipIndicator;
            this.zipCount = zipCount;
        }

        public void ProcessAttachedFiles(string[] fileNames)
        {
            foreach (string selectedFile in fileNames)
            {
                AddAttachment(selectedFile);
            }
        }

        public void AddAttachment(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLower();
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
            {
                ImageAttachments.Add(fileName);
            }
            else if (ext == ".mp3" || ext == ".wav" || ext == ".wma")
            {
                AudioAttachments.Add(fileName);
            }
            else if (ext == ".zip")
            {
                ZipAttachments.Add(fileName);
            }
            UpdateAttachmentsUI();
        }

        public void ClearAttachments()
        {
            ImageAttachments.Clear();
            AudioAttachments.Clear();
            ZipAttachments.Clear();
            UpdateAttachmentsUI();
        }

        public void UpdateAttachmentsUI()
        {
            imageIndicator.Visibility = (ImageAttachments.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
            imageCount.Text = ImageAttachments.Count.ToString();

            audioIndicator.Visibility = (AudioAttachments.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
            audioCount.Text = AudioAttachments.Count.ToString();

            zipIndicator.Visibility = (ZipAttachments.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
            zipCount.Text = ZipAttachments.Count.ToString();

            attachmentsSummaryPanel.Visibility = (ImageAttachments.Count > 0 || AudioAttachments.Count > 0 || ZipAttachments.Count > 0)
                ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
