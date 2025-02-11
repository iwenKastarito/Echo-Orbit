using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EchoOrbit.Controls
{
    public partial class PlaylistControl : UserControl
    {
        public PlaylistControl()
        {
            InitializeComponent();
        }

        // Adds one or more audio files to the playlist.
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Audio Files|*.mp3;*.wav;*.wma",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                foreach (string file in dlg.FileNames)
                {
                    PlaylistListBox.Items.Add(file);
                }
            }
        }

        // Removes the selected item from the playlist.
        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistListBox.SelectedItem != null)
            {
                PlaylistListBox.Items.Remove(PlaylistListBox.SelectedItem);
            }
        }

        // Shares the selected file (for example, by copying its path to the clipboard).
        private void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistListBox.SelectedItem != null)
            {
                Clipboard.SetText(PlaylistListBox.SelectedItem.ToString());
                MessageBox.Show("File path copied to clipboard.");
            }
        }

        // Plays the selected file. (In a real app, you might integrate with your MusicController.)
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistListBox.SelectedItem != null)
            {
                MessageBox.Show("Playing: " + PlaylistListBox.SelectedItem.ToString());
            }
        }
    }
}
