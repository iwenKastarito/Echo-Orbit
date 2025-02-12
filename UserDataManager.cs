using System;
using System.IO;
using Newtonsoft.Json;
using System.Windows;
using System.Xml;
using Newtonsoft.Json;


namespace EchoOrbit
{
    public static class UserDataManager
    {
        private static string dataFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "userdata.json");

        public static UserData LoadUserData()
        {
            if (File.Exists(dataFile))
            {
                try
                {
                    string json = File.ReadAllText(dataFile);
                    return JsonConvert.DeserializeObject<UserData>(json) ?? new UserData();
                }
                catch
                {
                    return new UserData();
                }
            }
            else
            {
                return new UserData();
            }
        }

        public static void SaveUserData(UserData data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);

                File.WriteAllText(dataFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving user data: " + ex.Message);
            }
        }
    }
}
