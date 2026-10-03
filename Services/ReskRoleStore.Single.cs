using System.Text.Json;

namespace RESK.WIL.Services
{
    /*
     * =========================================================
     * ONE ADMINISTRATOR
     * =========================================================
     *
     * The Administrator role belongs to one account only. Everyone
     * else who signs in to the admin screens gets another admin role
     * (Proposal Manager unless a different one is assigned).
     *
     * The account is chosen once (ReskSettingsMiddleware) and saved in
     *   App_Data/Administrator.json
     */
    public static partial class ReskRoleStore
    {
        private static bool _ownerLoaded;
        private static string? _ownerRoot;
        private static string? _ownerId;


        // The account that holds the Administrator role (null until it has been chosen).
        public static string? AdministratorUserId(string contentRoot)
        {
            lock (FileLock)
            {
                if (_ownerLoaded && _ownerRoot == contentRoot)
                {
                    return _ownerId;
                }

                _ownerId = null;

                try
                {
                    string path = OwnerPath(contentRoot);

                    if (File.Exists(path))
                    {
                        Dictionary<string, string>? saved = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));

                        if (saved != null && saved.TryGetValue("UserId", out string? id) && !string.IsNullOrWhiteSpace(id))
                        {
                            _ownerId = id;
                        }
                    }
                }
                catch (IOException)
                {
                }
                catch (JsonException)
                {
                }

                _ownerLoaded = true;
                _ownerRoot = contentRoot;
                return _ownerId;
            }
        }


        public static void SetAdministrator(string contentRoot, string userId, string email)
        {
            lock (FileLock)
            {
                Write(OwnerPath(contentRoot), new Dictionary<string, string> { ["UserId"] = userId, ["Email"] = email });

                _ownerId = userId;
                _ownerLoaded = true;
                _ownerRoot = contentRoot;
            }
        }


        public static bool IsAdministratorAccount(string contentRoot, string? userId)
        {
            return !string.IsNullOrEmpty(userId) && AdministratorUserId(contentRoot) == userId;
        }


        // Applied to every admin-area role lookup: only the chosen account is the Administrator.
        private static ReskRole? SingleAdministrator(string contentRoot, string userId, ReskRole? role, List<ReskRole> roles)
        {
            string? owner = AdministratorUserId(contentRoot);

            if (role == null || owner == null || role.Area != "Admin")
            {
                return role;
            }

            if (userId == owner)
            {
                return roles.FirstOrDefault(r => r.Id == AdministratorId) ?? role;
            }

            return role.Id == AdministratorId ? roles.FirstOrDefault(r => r.Id == ManagerId) ?? role : role;
        }


        private static string OwnerPath(string contentRoot) => Path.Combine(contentRoot, "App_Data", "Administrator.json");
    }
}