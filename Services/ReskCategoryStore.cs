using System.Text.Json;

namespace RESK.WIL.Services
{
    /*
     * =========================================================
     * PROGRAMME CATEGORIES
     * =========================================================
     *
     * The categories producers choose from on the
     * "Programme details" step, managed by admins at
     * /Admin/Categories.
     *
     * Saved as JSON (no database change needed):
     *
     *   App_Data/Categories.json
     *
     * A proposal stores the category NAME (ProducerProposal.Category),
     * so usage is counted by name.
     */
    public class ReskCategory
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        // Short code, e.g. DOC
        public string Code { get; set; } = string.Empty;

        // Active or Inactive
        public string Status { get; set; } = ReskCategoryStore.Active;

        public int DisplayOrder { get; set; }

        // Key from ReskCategoryStore.Colours, e.g. "Cyan"
        public string IconColour { get; set; } = "Cyan";

        // Shown in the producer's category list (when Active).
        public bool ShowOnForm { get; set; } = true;

        // Producers are told to add extra supporting documents.
        public bool RequireSupportingDocuments { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public string? CreatedBy { get; set; }

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public string? UpdatedBy { get; set; }

        public bool IsActive => Status == ReskCategoryStore.Active;

        // Active + shown on the form = producers can pick it.
        public bool AvailableToProducers => IsActive && ShowOnForm;
    }


    public static class ReskCategoryStore
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";

        // Icon colour name -> hex
        public static readonly IReadOnlyDictionary<string, string> Colours = new Dictionary<string, string>
        {
            ["Cyan"] = "#12b8ce",
            ["Navy"] = "#1d3557",
            ["Green"] = "#1f9a66",
            ["Amber"] = "#d39a12",
            ["Red"] = "#c9444d",
            ["Purple"] = "#7b5cd6",
            ["Pink"] = "#d6457f",
            ["Slate"] = "#5b6b85"
        };

        // Used the first time if there is no App_Data/CategorySeed.txt.
        private static readonly (string Name, string Description, string Code, string Colour)[] Defaults =
        {
            ("Documentary", "Non-fiction stories and factual programmes", "DOC", "Cyan"),
            ("Current Affairs", "Topical news, interviews and public issues", "CUR", "Navy"),
            ("Youth", "Programmes created for or by young people", "YTH", "Amber"),
            ("Community", "Local organisations, events and community stories", "COM", "Green"),
            ("Music & Culture", "Music, arts, heritage and cultural content", "MUS", "Purple"),
            ("Lifestyle", "Food, wellness, travel and everyday living", "LIF", "Pink"),
            ("Education", "Learning, skills and educational programmes", "EDU", "Cyan"),
            ("Drama", "Scripted stories, series and short films", "DRA", "Red"),
            ("Sport", "Local sport, fitness and sporting events", "SPO", "Green"),
            ("Faith & Religion", "Faith-based and religious programmes", "FAI", "Slate"),
            ("Health", "Health information and wellbeing", "HEA", "Amber"),
            ("Business", "Entrepreneurs, money and the local economy", "BUS", "Navy")
        };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        private static readonly object FileLock = new object();


        // All categories, ordered for display. Creates the file the first time.
        // usedNames: category names already on proposals (so they are kept).
        public static List<ReskCategory> All(string contentRoot, IEnumerable<string>? usedNames = null)
        {
            lock (FileLock)
            {
                string path = FilePath(contentRoot);

                if (File.Exists(path))
                {
                    try
                    {
                        List<ReskCategory>? list =
                            JsonSerializer.Deserialize<List<ReskCategory>>(File.ReadAllText(path));

                        if (list != null)
                        {
                            return Sort(list);
                        }
                    }
                    catch (JsonException)
                    {
                        // Broken file: keep a copy and start again below.
                        File.Copy(path, path + ".broken-" + DateTime.UtcNow.Ticks, true);
                    }
                    catch (IOException)
                    {
                        return new List<ReskCategory>();
                    }
                }

                List<ReskCategory> seeded = Seed(contentRoot, usedNames);
                Write(contentRoot, seeded);
                return Sort(seeded);
            }
        }


        public static ReskCategory? Get(string contentRoot, int id)
        {
            return All(contentRoot).FirstOrDefault(c => c.Id == id);
        }


        public static void SaveAll(string contentRoot, List<ReskCategory> categories)
        {
            lock (FileLock)
            {
                Write(contentRoot, categories);
            }
        }


        public static string Hex(string? colour)
        {
            return colour != null && Colours.TryGetValue(colour, out string? hex) ? hex : Colours["Cyan"];
        }


        // "Music & Culture" -> "MUS"
        public static string MakeCode(string name)
        {
            string letters = new string((name ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            return letters.Length == 0 ? "CAT" : letters.Substring(0, Math.Min(3, letters.Length));
        }


        // Same name, ignoring case and extra spaces.
        public static bool SameName(string? a, string? b)
        {
            return string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
        }


        // ---------------------------------------------------------

        private static List<ReskCategory> Seed(string contentRoot, IEnumerable<string>? usedNames)
        {
            var result = new List<ReskCategory>();
            string seedFile = Path.Combine(contentRoot, "App_Data", "CategorySeed.txt");
            string[] colourKeys = Colours.Keys.ToArray();

            // 1) The options the producer form had before (written by the install script).
            if (File.Exists(seedFile))
            {
                foreach (string line in File.ReadAllLines(seedFile))
                {
                    string name = line.Trim();

                    if (name.Length > 0 && name.Length <= 60 && !result.Any(c => SameName(c.Name, name)))
                    {
                        var known = Defaults.FirstOrDefault(d => SameName(d.Name, name));
                        result.Add(New(result.Count + 1, name,
                            known.Name != null ? known.Description : name + " programmes",
                            known.Name != null ? known.Code : MakeCode(name),
                            known.Name != null ? known.Colour : colourKeys[result.Count % colourKeys.Length]));
                    }
                }
            }

            // 2) Otherwise the default list.
            if (result.Count == 0)
            {
                foreach (var d in Defaults)
                {
                    result.Add(New(result.Count + 1, d.Name, d.Description, d.Code, d.Colour));
                }
            }

            // 3) Any category already used on a proposal.
            foreach (string used in usedNames ?? Enumerable.Empty<string>())
            {
                string name = (used ?? "").Trim();

                if (name.Length > 0 && name.Length <= 60 && !result.Any(c => SameName(c.Name, name)))
                {
                    result.Add(New(result.Count + 1, name, name + " programmes", MakeCode(name),
                        colourKeys[result.Count % colourKeys.Length]));
                }
            }

            // Codes must be unique.
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ReskCategory c in result)
            {
                string code = c.Code;
                int n = 2;

                while (!codes.Add(code))
                {
                    code = c.Code + n++;
                }

                c.Code = code;
            }

            return result;
        }


        private static ReskCategory New(int id, string name, string description, string code, string colour)
        {
            return new ReskCategory
            {
                Id = id,
                Name = name,
                Description = description,
                Code = code,
                DisplayOrder = id,
                IconColour = colour,
                Status = Active,
                ShowOnForm = true,
                CreatedBy = "System",
                UpdatedBy = "System"
            };
        }


        private static List<ReskCategory> Sort(List<ReskCategory> list)
        {
            return list.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToList();
        }


        private static void Write(string contentRoot, List<ReskCategory> categories)
        {
            string path = FilePath(contentRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(Sort(categories), JsonOptions));
        }


        private static string FilePath(string contentRoot)
        {
            return Path.Combine(contentRoot, "App_Data", "Categories.json");
        }
    }
}