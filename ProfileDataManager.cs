using Newtonsoft.Json;
using System;
using System.IO;
using System.Windows;

namespace EchoOrbit
{
    public static class ProfileDataManager
    {
        private static string profileFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profile.json");

        public static Models.ProfileData LoadProfileData()
        {
            if (File.Exists(profileFile))
            {
                try
                {
                    string json = File.ReadAllText(profileFile);
                    return JsonConvert.DeserializeObject<Models.ProfileData>(json) ?? new Models.ProfileData();
                }
                catch
                {
                    return new Models.ProfileData();
                }
            }
            else
            {
                return new Models.ProfileData();
            }
        }

        public static void SaveProfileData(Models.ProfileData data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(profileFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving profile data: " + ex.Message);
            }
        }
    }
}
