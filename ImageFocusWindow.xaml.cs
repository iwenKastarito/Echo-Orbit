using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EchoOrbit.Controls
{
    public partial class ImageFocusWindow : Window
    {
        private List<ImageSource> _images;
        private int _currentIndex;

        /// <summary>
        /// Constructs the focus window with a list of images and an optional starting index.
        /// </summary>
        public ImageFocusWindow(List<ImageSource> images, int startIndex = 0)
        {
            InitializeComponent();
            _images = images;
            _currentIndex = startIndex;
            ShowImage();
        }

        /// <summary>
        /// Displays the current image.
        /// </summary>
        private void ShowImage()
        {
            if (_images != null && _images.Count > 0)
            {
                FocusedImage.Source = _images[_currentIndex];
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_images != null && _images.Count > 0)
            {
                _currentIndex = (_currentIndex + 1) % _images.Count;
                ShowImage();
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (_images != null && _images.Count > 0)
            {
                _currentIndex = (_currentIndex - 1 + _images.Count) % _images.Count;
                ShowImage();
            }
        }

        /// <summary>
        /// Closes the window when clicking on the background (outside of the buttons and image).
        /// </summary>
        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // If the click was not on one of the controls, close the window.
            if (e.OriginalSource is FrameworkElement fe)
            {
                if (fe.Name != "FocusedImage" && fe.Name != "CloseButton" && fe.Name != "NextButton" && fe.Name != "PreviousButton")
                {
                    this.Close();
                }
            }
            else
            {
                this.Close();
            }
        }
    }
}
