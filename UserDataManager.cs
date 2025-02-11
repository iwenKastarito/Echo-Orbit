using EchoOrbit.Models;
using System;
using System.IO;
using System.Text.Json;

namespace EchoOrbit
{
    public class UserDataManager
    {
        private const string UserDataFileName = "userdata.json";

        public static UserData LoadUserData()
        {
            if (File.Exists(UserDataFileName))
            {
                try
                {
                    string json = File.ReadAllText(UserDataFileName);
                    return JsonSerializer.Deserialize<UserData>(json);
                }
                catch
                {
                    return new UserData { UserName = "Default", SomeValue = 0 };
                }
            }
            else
            {
                return new UserData { UserName = "Default", SomeValue = 0 };
            }
        }

        public static void SaveUserData(UserData data)
        {
            try
            {
                string json = JsonSerializer.Serialize(data);
                File.WriteAllText(UserDataFileName, json);
            }
            catch
            {
                // Handle errors if needed.
            }
        }
    }
}
