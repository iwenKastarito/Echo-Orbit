using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EchoOrbit.Models
{
    public class ProfileData
    {
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string ProfilePicturePath { get; set; }
        public string Theme { get; set; } // "Light" or "Dark"
    }
}
