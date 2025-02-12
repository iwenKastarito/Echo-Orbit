using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Media;

namespace EchoOrbit.Models
{
    public class Song
    {
        public string FilePath { get; set; }
        public string Title { get; set; }
        public ImageSource Thumbnail { get; set; }
    }
}
