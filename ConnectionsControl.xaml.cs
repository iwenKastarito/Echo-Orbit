using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace EchoOrbit.Controls
{
    public partial class ConnectionsControl : UserControl
    {
        public ObservableCollection<OnlineUser> OnlineUsers { get; set; }
        public ObservableCollection<Group> Groups { get; set; }

        public ConnectionsControl()
        {
            InitializeComponent();
            DataContext = this;
            // Sample data for online users.
            OnlineUsers = new ObservableCollection<OnlineUser>
            {
                new OnlineUser { DisplayName = "Alice", ProfileImage = new BitmapImage(new Uri("pack://application:,,,/defaultProfile.png", UriKind.Absolute)) },
                new OnlineUser { DisplayName = "Bob", ProfileImage = new BitmapImage(new Uri("pack://application:,,,/defaultProfile.png", UriKind.Absolute)) },
                new OnlineUser { DisplayName = "Charlie", ProfileImage = new BitmapImage(new Uri("pack://application:,,,/defaultProfile.png", UriKind.Absolute)) },
                new OnlineUser { DisplayName = "David", ProfileImage = new BitmapImage(new Uri("pack://application:,,,/defaultProfile.png", UriKind.Absolute)) }
            };

            // Start with no groups.
            Groups = new ObservableCollection<Group>();

            // Populate NewGroupMembersListBox with OnlineUsers for selection.
            NewGroupMembersListBox.ItemsSource = OnlineUsers;
        }

        private void NewGroupButton_Click(object sender, RoutedEventArgs e)
        {
            NewGroupPanel.Visibility = (NewGroupPanel.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void NewGroupChangeImageButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
            if (dlg.ShowDialog() == true)
            {
                NewGroupImagePreview.Source = new BitmapImage(new Uri(dlg.FileName, UriKind.Absolute));
                NewGroupImagePreview.Tag = dlg.FileName;
            }
        }

        private void CancelNewGroupButton_Click(object sender, RoutedEventArgs e)
        {
            NewGroupPanel.Visibility = Visibility.Collapsed;
        }

        private void CreateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            string groupName = NewGroupNameTextBox.Text;
            if (string.IsNullOrWhiteSpace(groupName))
            {
                MessageBox.Show("Please enter a group name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string imagePath = NewGroupImagePreview.Tag as string;
            BitmapImage groupImage;
            if (!string.IsNullOrEmpty(imagePath) && System.IO.File.Exists(imagePath))
            {
                groupImage = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
            }
            else
            {
                groupImage = new BitmapImage(new Uri("pack://application:,,,/defaultGroup.png", UriKind.Absolute));
            }
            Group newGroup = new Group { GroupName = groupName, GroupImage = groupImage };
            Groups.Add(newGroup);
            MessageBox.Show("New group created.", "Group", MessageBoxButton.OK, MessageBoxImage.Information);
            NewGroupPanel.Visibility = Visibility.Collapsed;
            NewGroupNameTextBox.Text = "";
            NewGroupImagePreview.Source = null;
            NewGroupImagePreview.Tag = null;
            NewGroupMembersListBox.SelectedItems.Clear();
        }

        private void RemoveGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Group group)
            {
                if (MessageBox.Show($"Are you sure you want to remove group '{group.GroupName}'?", "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Groups.Remove(group);
                }
            }
        }
    }

    public class OnlineUser
    {
        public string DisplayName { get; set; }
        public BitmapImage ProfileImage { get; set; }
    }

    public class Group
    {
        public string GroupName { get; set; }
        public BitmapImage GroupImage { get; set; }
    }
}
