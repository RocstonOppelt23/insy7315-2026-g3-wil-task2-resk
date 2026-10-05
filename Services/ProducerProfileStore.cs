using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using RESK.WIL.Models;

namespace RESK.WIL.Services
{
    /*
     * Saves each producer's settings and profile photo in
     * App_Data (outside wwwroot, so nobody can open the files
     * directly):
     *
     *   App_Data/Profiles/{userId}.json
     *   App_Data/ProfilePhotos/{userId}.jpg  (or .png)
     *
     * Email and phone number are stored on the ASP.NET Identity
     * user instead (AspNetUsers table).
     */
    public static class ProducerProfileStore
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions { WriteIndented = true };

        private static readonly object FileLock = new object();

        private static readonly string[] PhotoExtensions =
        {
            ".jpg",
            ".png"
        };


        // =====================================================
        // PROFILE
        // =====================================================

        public static ProducerProfile Load(
            string contentRoot,
            string userId)
        {
            string path = ProfilePath(contentRoot, userId);

            try
            {
                if (File.Exists(path))
                {
                    ProducerProfile? profile =
                        JsonSerializer.Deserialize<ProducerProfile>(
                            File.ReadAllText(path));

                    if (profile != null)
                    {
                        return profile;
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (JsonException)
            {
            }

            return new ProducerProfile();
        }


        public static void Save(
            string contentRoot,
            string userId,
            ProducerProfile profile)
        {
            string path = ProfilePath(contentRoot, userId);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            lock (FileLock)
            {
                File.WriteAllText(
                    path,
                    JsonSerializer.Serialize(profile, JsonOptions));
            }
        }


        // Full name if the producer set one, otherwise their email.
        public static string DisplayName(
            ProducerProfile profile,
            IdentityUser? user)
        {
            if (!string.IsNullOrWhiteSpace(profile.FullName))
            {
                return profile.FullName.Trim();
            }

            return user?.Email
                   ?? user?.UserName
                   ?? "Producer";
        }


        // "Kuan Chi" -> "KC", "freddy@mail.com" -> "F"
        public static string Initials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "P";
            }

            string clean = name.Trim();

            // For an email address, use the part before the @.
            int at = clean.IndexOf('@');

            if (at > 0)
            {
                clean = clean.Substring(0, at);
            }

            string[] words =
                clean.Split(
                    new[] { ' ', '.', '_', '-' },
                    StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
            {
                return "P";
            }

            string initials =
                words.Length == 1 || at > 0
                    ? words[0].Substring(0, 1)
                    : words[0].Substring(0, 1) +
                      words[words.Length - 1].Substring(0, 1);

            return initials.ToUpperInvariant();
        }


        // =====================================================
        // PHOTO
        // =====================================================

        public static string? PhotoPath(
            string contentRoot,
            string userId)
        {
            foreach (string extension in PhotoExtensions)
            {
                string path =
                    Path.Combine(
                        PhotoFolder(contentRoot),
                        SafeId(userId) + extension);

                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }


        // Address of the photo, with a version number so the
        // browser shows a new photo straight away.
        public static string? PhotoUrl(
            string contentRoot,
            string userId)
        {
            string? path = PhotoPath(contentRoot, userId);

            if (path == null)
            {
                return null;
            }

            long version = File.GetLastWriteTimeUtc(path).Ticks;

            return "/Producer/ProfilePhoto?v=" + version;
        }


        // extension must be ".jpg" or ".png"
        public static async Task SavePhotoAsync(
            string contentRoot,
            string userId,
            IFormFile file,
            string extension)
        {
            string folder = PhotoFolder(contentRoot);

            Directory.CreateDirectory(folder);

            string finalPath =
                Path.Combine(folder, SafeId(userId) + extension);

            string tempPath = finalPath + ".uploading";

            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.Create,
                             FileAccess.Write))
            {
                await file.CopyToAsync(stream);
            }

            // Remove the old photo (it may have the other extension).
            DeletePhoto(contentRoot, userId);

            File.Move(tempPath, finalPath, overwrite: true);
        }


        public static void DeletePhoto(
            string contentRoot,
            string userId)
        {
            foreach (string extension in PhotoExtensions)
            {
                string path =
                    Path.Combine(
                        PhotoFolder(contentRoot),
                        SafeId(userId) + extension);

                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (IOException)
                {
                }
            }
        }


        // =====================================================
        // PATHS
        // =====================================================

        private static string ProfilePath(
            string contentRoot,
            string userId)
        {
            return Path.Combine(
                contentRoot,
                "App_Data",
                "Profiles",
                SafeId(userId) + ".json");
        }


        private static string PhotoFolder(string contentRoot)
        {
            return Path.Combine(
                contentRoot,
                "App_Data",
                "ProfilePhotos");
        }


        // User ids are GUIDs; strip anything else so an id can
        // never point outside the folder.
        private static string SafeId(string userId)
        {
            string safe =
                Regex.Replace(userId ?? string.Empty, "[^A-Za-z0-9-]", string.Empty);

            return safe.Length == 0 ? "unknown" : safe;
        }
    }
}