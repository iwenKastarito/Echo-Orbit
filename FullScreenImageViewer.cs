using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;

namespace EchoOrbit.Helpers
{
    public static class FullScreenImageViewer
    {
        public static void Show(List<ImageSource> images, int selectedIndex)
        {
            if (images == null || images.Count == 0)
                return;

            int currentViewerIndex = selectedIndex;

            Window fullScreenViewer = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowState = WindowState.Maximized,
                Topmost = true,
                ShowInTaskbar = false
            };

            Grid rootGrid = new Grid();

            // Semi‑transparent blurred background.
            Rectangle backgroundRect = new Rectangle
            {
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 0, 0, 0))
            };
            backgroundRect.Effect = new BlurEffect { Radius = 10 };
            rootGrid.Children.Add(backgroundRect);

            // Image container in the center.
            Border imageContainer = new Border
            {
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Image fullScreenImage = new Image
            {
                Source = images[currentViewerIndex],
                Stretch = Stretch.Uniform,
                MaxWidth = SystemParameters.PrimaryScreenWidth,
                MaxHeight = SystemParameters.PrimaryScreenHeight
            };
            imageContainer.Child = fullScreenImage;
            rootGrid.Children.Add(imageContainer);

            // Navigation buttons.
            Grid navGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            navGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            navGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button leftButton = new Button
            {
                Content = "❮",
                Width = 50,
                Height = 50,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 30,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 0, 0, 0)
            };
            Grid.SetColumn(leftButton, 0);
            navGrid.Children.Add(leftButton);

            Button rightButton = new Button
            {
                Content = "❯",
                Width = 50,
                Height = 50,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 30,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 20, 0)
            };
            Grid.SetColumn(rightButton, 1);
            navGrid.Children.Add(rightButton);

            rootGrid.Children.Add(navGrid);

            // Close the viewer if background is clicked.
            rootGrid.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == backgroundRect)
                {
                    fullScreenViewer.Close();
                }
            };

            leftButton.Click += (s, e) =>
            {
                currentViewerIndex = (currentViewerIndex - 1 + images.Count) % images.Count;
                fullScreenImage.Source = images[currentViewerIndex];
            };

            rightButton.Click += (s, e) =>
            {
                currentViewerIndex = (currentViewerIndex + 1) % images.Count;
                fullScreenImage.Source = images[currentViewerIndex];
            };

            fullScreenViewer.Content = rootGrid;
            fullScreenViewer.ShowDialog();
        }
    }
}
