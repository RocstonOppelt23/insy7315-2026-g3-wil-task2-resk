using RESK.WIL.Data;

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
     * the ReskSystemValues table under the key "AdministratorUserId".
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

                using (ApplicationDbContext db = ReskDb.Open())
                {
                    string? id = db.ReskSystemValues.Find(AdministratorKey)?.Value;
                    _ownerId = string.IsNullOrWhiteSpace(id) ? null : id;
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
                using (ApplicationDbContext db = ReskDb.Open())
                {
                    SetValue(db, AdministratorKey, userId);
                    SetValue(db, AdministratorEmailKey, email);
                    db.SaveChanges();
                }

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


        public const string AdministratorKey = "AdministratorUserId";

        public const string AdministratorEmailKey = "AdministratorEmail";


        private static void SetValue(ApplicationDbContext db, string key, string value)
        {
            ReskSystemValue? row = db.ReskSystemValues.Find(key);

            if (row == null)
            {
                db.ReskSystemValues.Add(new ReskSystemValue { Key = key, Value = value });
            }
            else
            {
                row.Value = value;
            }
        }


        // Lets tests start from a clean state.
        internal static void ResetCache()
        {
            lock (FileLock)
            {
                _ownerLoaded = false;
                _ownerRoot = null;
                _ownerId = null;
            }
        }
    }


    // Small key/value settings kept in the database (e.g. which account is the Administrator).
    public class ReskSystemValue
    {
        public string Key { get; set; } = string.Empty;

        public string? Value { get; set; }
    }
}