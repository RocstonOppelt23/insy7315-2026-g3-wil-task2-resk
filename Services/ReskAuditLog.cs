using System.Text.Json;
using RESK.WIL.Models;

namespace RESK.WIL.Services
{
    /*
     * =========================================================
     * AUDIT LOG
     * =========================================================
     *
     * A read-only history of important actions: sign-ins, user and
     * role changes, category changes, proposal workflow steps and
     * CSV exports.
     *
     * Events are written by ReskAuditMiddleware. There is no code
     * anywhere that edits or deletes a single event, so the log can
     * only grow. Events older than the retention period are dropped.
     *
     * Saved as one JSON object per line (no database change needed):
     *   App_Data/AuditLog.jsonl
     */
    public static class ReskAuditLog
    {
        public const string Successful = "Successful";
        public const string Failed = "Failed";

        public static int RetentionYears => ReskSettingsStore.AuditRetention;

        public static readonly string[] Modules =
        {
            "Proposals", "Users", "Roles", "Categories", "Reports", "Settings", "Security"
        };

        public static readonly (string Key, string Label)[] Types =
        {
            ("create", "Created"), ("update", "Updated"), ("delete", "Deleted"),
            ("submit", "Submitted"), ("approve", "Approved"), ("reject", "Rejected / changes requested"),
            ("assign", "Assigned"), ("export", "Exported"), ("signin", "Sign-in and sign-out"),
            ("failed", "Failed or blocked")
        };

        private static readonly object Gate = new object();
        private static List<ReskAuditEvent>? _events;
        private static string? _root;


        // Adds one event. Never throws: the audit log must not break the page it records.
        public static ReskAuditEvent Add(string contentRoot, ReskAuditEvent item)
        {
            lock (Gate)
            {
                List<ReskAuditEvent> events = Load(contentRoot);

                item.Id = NextId(events, item.AtUtc);
                events.Add(item);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath(contentRoot))!);
                    File.AppendAllText(FilePath(contentRoot), JsonSerializer.Serialize(item) + Environment.NewLine);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                return item;
            }
        }


        // Every event, newest first.
        public static List<ReskAuditEvent> All(string contentRoot)
        {
            lock (Gate)
            {
                var copy = new List<ReskAuditEvent>(Load(contentRoot));
                copy.Reverse();
                return copy;
            }
        }


        public static ReskAuditEvent? Get(string contentRoot, string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            lock (Gate)
            {
                return Load(contentRoot).FirstOrDefault(e => string.Equals(e.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }


        // Oldest first. Read from the file once, then kept in memory.
        private static List<ReskAuditEvent> Load(string contentRoot)
        {
            if (_events != null && _root == contentRoot)
            {
                return _events;
            }

            var events = new List<ReskAuditEvent>();
            string path = FilePath(contentRoot);
            int unreadable = 0;

            try
            {
                if (File.Exists(path))
                {
                    foreach (string line in File.ReadAllLines(path))
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        try
                        {
                            ReskAuditEvent? item = JsonSerializer.Deserialize<ReskAuditEvent>(line);

                            if (item != null && item.Id.Length > 0)
                            {
                                events.Add(item);
                            }
                        }
                        catch (JsonException)
                        {
                            unreadable++;
                        }
                    }
                }
            }
            catch (IOException)
            {
            }

            // Retention: events older than 7 years are removed.
            DateTime cutoff = DateTime.UtcNow.AddYears(-RetentionYears);

            if (unreadable == 0 && events.RemoveAll(e => e.AtUtc < cutoff) > 0)
            {
                try
                {
                    File.WriteAllLines(path, events.Select(e => JsonSerializer.Serialize(e)));
                }
                catch (IOException)
                {
                }
            }

            _events = events;
            _root = contentRoot;
            return events;
        }


        // AUD-{year}-{number}: the number restarts at 00001 each year.
        private static string NextId(List<ReskAuditEvent> events, DateTime atUtc)
        {
            string prefix = "AUD-" + SouthAfricaTime.ToLocal(atUtc).Year + "-";
            int last = 0;

            for (int i = events.Count - 1; i >= 0; i--)
            {
                if (events[i].Id.StartsWith(prefix, StringComparison.Ordinal))
                {
                    int.TryParse(events[i].Id.Substring(prefix.Length), out last);
                    break;
                }
            }

            return prefix + (last + 1).ToString("00000");
        }


        private static string FilePath(string contentRoot)
        {
            return Path.Combine(contentRoot, "App_Data", "AuditLog.jsonl");
        }
    }
}