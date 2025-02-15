using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace EchoOrbit.Controls
{
    public partial class ConnectionsControl : UserControl
    {
        public ObservableCollection<OnlineUser> OnlineUsers { get; set; }
        public ObservableCollection<Group> Groups { get; set; }

        public event Action<OnlineUser> OnlineUserChatRequested;

        public ConnectionsControl()
        {
            InitializeComponent();
            DataContext = this;

            // Sample data for online users.
            OnlineUsers = new ObservableCollection<OnlineUser>();

            // Initialize groups (empty at start).
            Groups = new ObservableCollection<Group>();
        }

        // Called when the Chat button is clicked.
        private void ChatButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OnlineUser user)
            {
                OnlineUserChatRequested?.Invoke(user);
            }
        }

        // Called when the New Group button is clicked.
        private void NewGroupButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle visibility of the New Group creation panel.
            NewGroupPanel.Visibility = (NewGroupPanel.Visibility == Visibility.Visible)
                                        ? Visibility.Collapsed
                                        : Visibility.Visible;
        }

        // Called when the Change Image button (for group image) is clicked.
        private void NewGroupChangeImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
            if (dlg.ShowDialog() == true)
            {
                NewGroupImagePreview.Source = new BitmapImage(new Uri(dlg.FileName, UriKind.Absolute));
                NewGroupImagePreview.Tag = dlg.FileName;
            }
        }

        // Called when the Cancel button is clicked in the New Group panel.
        private void CancelNewGroupButton_Click(object sender, RoutedEventArgs e)
        {
            NewGroupPanel.Visibility = Visibility.Collapsed;
        }

        // Called when the Create Group button is clicked.
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

            // Reset UI.
            NewGroupPanel.Visibility = Visibility.Collapsed;
            NewGroupNameTextBox.Text = "";
            NewGroupImagePreview.Source = null;
            NewGroupImagePreview.Tag = null;
            NewGroupMembersListBox.SelectedItems.Clear();
        }

        // Called when the Remove Group button is clicked.
        private void RemoveGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Group group)
            {
                if (MessageBox.Show($"Are you sure you want to remove group '{group.GroupName}'?",
                                    "Confirm Removal",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Groups.Remove(group);
                }
            }
        }
    }

    // Definition for OnlineUser.
    public class OnlineUser
    {
        public string DisplayName { get; set; }
        public BitmapImage ProfileImage { get; set; }

        // The network endpoint for connecting to this peer.
        public IPEndPoint PeerEndpoint { get; set; }
    }

    // Definition for Group.
    public class Group
    {
        public string GroupName { get; set; }
        public BitmapImage GroupImage { get; set; }
    }
}
