using System.Text.Json;

namespace RESK.WIL.Services
{
    /*
     * =========================================================
     * EXPORT HISTORY
     * =========================================================
     *
     * Every CSV export an admin downloads is recorded here, so
     * the Reports page can show who exported what and when, and
     * offer "Run again" with the same settings.
     *
     *   App_Data/ExportLog.json   (newest first, last 100 kept)
     */
    public class ReskExportEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 10);

        public DateTime AtUtc { get; set; } = DateTime.UtcNow;

        public string By { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public int Rows { get; set; }

        public int Columns { get; set; }

        // Short description of the filters, e.g. "1 Aug - 31 Aug 2026 • Approved"
        public string Summary { get; set; } = string.Empty;

        // Query string that re-opens the export form with the same settings.
        public string Query { get; set; } = string.Empty;
    }


    public static class ReskExportLog
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        private static readonly object FileLock = new object();


        public static List<ReskExportEntry> All(string contentRoot)
        {
            lock (FileLock)
            {
                try
                {
                    string path = FilePath(contentRoot);

                    if (File.Exists(path))
                    {
                        return JsonSerializer.Deserialize<List<ReskExportEntry>>(File.ReadAllText(path)) ?? new List<ReskExportEntry>();
                    }
                }
                catch (IOException)
                {
                }
                catch (JsonException)
                {
                }

                return new List<ReskExportEntry>();
            }
        }


        public static void Add(string contentRoot, ReskExportEntry entry)
        {
            List<ReskExportEntry> list = All(contentRoot);

            lock (FileLock)
            {
                list.Insert(0, entry);

                if (list.Count > 100)
                {
                    list.RemoveRange(100, list.Count - 100);
                }

                string path = FilePath(contentRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(list, JsonOptions));
            }
        }


        private static string FilePath(string contentRoot)
        {
            return Path.Combine(contentRoot, "App_Data", "ExportLog.json");
        }
    }
}