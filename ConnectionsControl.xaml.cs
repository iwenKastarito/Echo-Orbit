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
            // Sample data; replace with actual live data.
            OnlineUsers = new ObservableCollection<OnlineUser>
            {
                new OnlineUser { DisplayName = "Alice", ProfileImage = new BitmapImage(new Uri("pack://application:,,,/defaultProfile.png", UriKind.Absolute)) },
                new OnlineUser { DisplayName = "Bob", ProfileImage = new BitmapImage(new Uri("pack://application:,,,/defaultProfile.png", UriKind.Absolute)) },
                new OnlineUser { DisplayName = "Charlie", ProfileImage = new BitmapImage(new Uri("pack://application:,,,/defaultProfile.png", UriKind.Absolute)) },
                new OnlineUser { DisplayName = "David", ProfileImage = new BitmapImage(new Uri("pack://application:,,,/defaultProfile.png", UriKind.Absolute)) }
            };

            Groups = new ObservableCollection<Group>
            {
                new Group { GroupName = "Developers", GroupImage = new BitmapImage(new Uri("pack://application:,,,/defaultGroup.png", UriKind.Absolute)) },
                new Group { GroupName = "Designers", GroupImage = new BitmapImage(new Uri("pack://application:,,,/defaultGroup.png", UriKind.Absolute)) },
                new Group { GroupName = "Managers", GroupImage = new BitmapImage(new Uri("pack://application:,,,/defaultGroup.png", UriKind.Absolute)) }
            };
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
