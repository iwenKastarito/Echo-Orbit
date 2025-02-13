using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;

namespace EchoOrbit.Models
{
    public class Group
    {
        public string GroupName { get; set; }
        public BitmapImage GroupImage { get; set; }
        public ObservableCollection<OnlineUser> Members { get; set; } = new ObservableCollection<OnlineUser>();
    }
}
