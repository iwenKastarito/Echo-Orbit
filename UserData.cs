using System.Collections.Generic;

namespace EchoOrbit
{
    public class StoredSong
    {
        public string FilePath { get; set; }
        public string Title { get; set; }
    }

    public class StoredPlaylist
    {
        public string Name { get; set; }
        public List<StoredSong> Songs { get; set; } = new List<StoredSong>();
    }

    public class UserData
    {
        public List<StoredPlaylist> Playlists { get; set; } = new List<StoredPlaylist>();
    }
}
