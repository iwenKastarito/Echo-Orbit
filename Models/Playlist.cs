using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.ObjectModel;
using System.Windows.Media;

namespace EchoOrbit.Models
{
    public class Playlist
    {
        public string Name { get; set; }
        public ImageSource Thumbnail { get; set; }
        public ObservableCollection<Song> Songs { get; set; } = new ObservableCollection<Song>();
    }
}

