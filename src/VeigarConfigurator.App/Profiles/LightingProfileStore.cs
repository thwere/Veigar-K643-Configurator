using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace VeigarConfigurator.App.Profiles
{
    public static class LightingProfileStore
    {
        public static void Save(string path, LightingProfile profile)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A profile path is required.", "path");
            }

            if (profile == null)
            {
                throw new ArgumentNullException("profile");
            }

            var serializer = new DataContractJsonSerializer(typeof(LightingProfile));
            using (FileStream stream = File.Create(path))
            {
                serializer.WriteObject(stream, profile);
            }
        }

        public static LightingProfile Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A profile path is required.", "path");
            }

            var serializer = new DataContractJsonSerializer(typeof(LightingProfile));
            using (FileStream stream = File.OpenRead(path))
            {
                LightingProfile profile = serializer.ReadObject(stream) as LightingProfile;
                if (profile == null)
                {
                    throw new InvalidDataException("The selected file does not contain a lighting profile.");
                }

                return profile;
            }
        }
    }
}
