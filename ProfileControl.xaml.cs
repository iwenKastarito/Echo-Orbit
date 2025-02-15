using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EchoOrbit.Models;  // Your ProfileData model
using Microsoft.Win32;   // For OpenFileDialog
using Microsoft.VisualBasic; // For InputBox if needed

namespace EchoOrbit.Controls
{
    public partial class ProfileControl : UserControl
    {
        // Holds the current profile image path.
        private string _currentProfilePicturePath = "";

        public ProfileControl()
        {
            InitializeComponent();
            LoadProfile();
        }

        private void LoadProfile()
        {
            var profile = ProfileDataManager.LoadProfileData();
            if (!string.IsNullOrEmpty(profile.DisplayName))
            {
                DisplayNameTextBox.Text = profile.DisplayName;
                EmailTextBox.Text = profile.Email;
                PasswordBox.Password = profile.PasswordHash; // For demonstration.
                if (profile.Theme == "Light")
                    LightThemeRadioButton.IsChecked = true;
                else
                    DarkThemeRadioButton.IsChecked = true;

                if (!string.IsNullOrEmpty(profile.ProfilePicturePath) && File.Exists(profile.ProfilePicturePath))
                {
                    _currentProfilePicturePath = profile.ProfilePicturePath;
                    ProfileImage.Source = new BitmapImage(new Uri(_currentProfilePicturePath, UriKind.Absolute));
                }
                else
                {
                    try
                    {
                        _currentProfilePicturePath = "pack://application:,,,/defaultProfile.png";
                        ProfileImage.Source = new BitmapImage(new Uri(_currentProfilePicturePath, UriKind.Absolute));
                    }
                    catch
                    {
                        ProfileImage.Source = new BitmapImage(new Uri("https://via.placeholder.com/120", UriKind.Absolute));
                    }
                }
            }
            else
            {
                DisplayNameTextBox.Text = "Echo";
                EmailTextBox.Text = "Default@example.com";
                PasswordBox.Password = "hashed_password_example";
                try
                {
                    _currentProfilePicturePath = "pack://application:,,,/defaultProfile.png";
                    ProfileImage.Source = new BitmapImage(new Uri(_currentProfilePicturePath, UriKind.Absolute));
                }
                catch
                {
                    ProfileImage.Source = new BitmapImage(new Uri("https://via.placeholder.com/120", UriKind.Absolute));
                }
                LightThemeRadioButton.IsChecked = true;
            }
        }

        private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var profile = new ProfileData
            {
                DisplayName = DisplayNameTextBox.Text,
                Email = EmailTextBox.Text,
                PasswordHash = PasswordBox.Password, // For demonstration.
                ProfilePicturePath = _currentProfilePicturePath,
                Theme = (LightThemeRadioButton.IsChecked == true) ? "Light" : "Dark"
            };

            ProfileDataManager.SaveProfileData(profile);
            MessageBox.Show("Profile saved successfully.", "Profile", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ChangeImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
            if (dlg.ShowDialog() == true)
            {
                _currentProfilePicturePath = dlg.FileName;
                ProfileImage.Source = new BitmapImage(new Uri(_currentProfilePicturePath, UriKind.Absolute));
            }
        }

        private void LightThemeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            Application.Current.Resources["PrimaryBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"));
            Application.Current.Resources["SecondaryBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAEAEA"));
            Application.Current.Resources["AccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A8D3A"));
            Application.Current.Resources["CardBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
            Application.Current.Resources["TextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E"));
            Application.Current.MainWindow.InvalidateVisual();
        }

        private void DarkThemeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            Application.Current.Resources["PrimaryBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E2E"));
            Application.Current.Resources["SecondaryBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383838"));
            Application.Current.Resources["AccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#55B155"));
            Application.Current.Resources["CardBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4D4D4D"));
            Application.Current.Resources["TextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CFC9C4"));
            Application.Current.MainWindow.InvalidateVisual();
        }
    }
}
