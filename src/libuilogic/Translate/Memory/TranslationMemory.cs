using System.Text.Json;

namespace Nikse.SubtitleEdit.UiLogic.Translate.Memory
{
    /// <summary>
    /// Optional local translation memory: a JSON-file backed store of accepted source→target
    /// segment pairs. Lookups are normalized exact matches (whitespace-collapsed,
    /// case-insensitive, Arabic diacritic-insensitive) restricted to the language pair and,
    /// when set, the style/profile id.
    /// <para>
    /// The memory is deliberately dumb: no fuzzy matching, no database. It answers one question -
    /// "have we already accepted a translation for this exact line?" - reliably and transparently.
    /// </para>
    /// <para>
    /// The UI sets <see cref="DefaultFolderPathProvider"/> at startup (same pattern as
    /// SpellCheckConfig) so the headless logic layer never depends on the UI's config type.
    /// </para>
    /// </summary>
    public sealed class TranslationMemory
    {
        /// <summary>Set by the UI at startup to the user's data folder; null = no default location.</summary>
        public static Func<string>? DefaultFolderPathProvider { get; set; }

        private const string FileName = "translation_memory.json";

        private readonly object _lock = new object();
        private readonly List<TranslationMemoryEntry> _entries;
        private readonly string _filePath;

        public TranslationMemory(string? folderPath = null)
        {
            var path = folderPath;
            if (string.IsNullOrEmpty(path))
            {
                path = DefaultFolderPathProvider?.Invoke();
            }

            _filePath = string.IsNullOrEmpty(path)
                ? FileName
                : Path.Combine(path, FileName);
            _entries = new List<TranslationMemoryEntry>();
        }

        public string FilePath => _filePath;

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _entries.Count;
                }
            }
        }

        /// <summary>Normalizes source text for matching: trims, collapses internal whitespace, drops Arabic diacritics and lower-cases.</summary>
        public static string NormalizeForMatch(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder(text.Length);
            var lastWasSpace = true;
            foreach (var c in text.Trim())
            {
                // Arabic diacritics (tashkeel) U+064B..U+0652 + U+0653, and tatweel, never affect matching.
                if ((c >= '\u064B' && c <= '\u0653') || c == '\u0640')
                {
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace)
                    {
                        sb.Append(' ');
                        lastWasSpace = true;
                    }

                    continue;
                }

                sb.Append(char.ToLowerInvariant(c));
                lastWasSpace = false;
            }

            return sb.ToString().Trim();
        }

        /// <summary>Loads entries from the backing file. A missing file is not an error (empty memory).</summary>
        public void Load()
        {
            lock (_lock)
            {
                _entries.Clear();
                if (!File.Exists(_filePath))
                {
                    return;
                }

                try
                {
                    var json = File.ReadAllText(_filePath);
                    var loaded = JsonSerializer.Deserialize<List<TranslationMemoryEntry>>(json, JsonOptions);
                    if (loaded != null)
                    {
                        _entries.AddRange(loaded);
                    }
                }
                catch (Exception)
                {
                    // A corrupt memory file must never break a translation run - start empty and
                    // let the next Save() rewrite the file from scratch.
                    _entries.Clear();
                }
            }
        }

        /// <summary>Writes all entries back to the backing file.</summary>
        public void Save()
        {
            List<TranslationMemoryEntry> copy;
            lock (_lock)
            {
                copy = new List<TranslationMemoryEntry>(_entries);
            }

            var folder = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(copy, JsonOptions), new System.Text.UTF8Encoding(false));
        }

        /// <summary>Adds an entry. Returns true when a new entry was added, false when an identical enabled entry already existed.</summary>
        public bool Add(TranslationMemoryEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.SourceText) || string.IsNullOrWhiteSpace(entry.TargetText))
            {
                return false;
            }

            lock (_lock)
            {
                var key = NormalizeForMatch(entry.SourceText);
                foreach (var existing in _entries)
                {
                    if (existing.Enabled &&
                        existing.SourceLanguageCode == entry.SourceLanguageCode &&
                        existing.TargetLanguageCode == entry.TargetLanguageCode &&
                        NormalizeForMatch(existing.SourceText) == key)
                    {
                        // Refresh the accepted translation (latest wins) but keep one entry.
                        existing.TargetText = entry.TargetText;
                        existing.StyleId = entry.StyleId;
                        existing.Engine = entry.Engine;
                        existing.CreatedUtc = entry.CreatedUtc;
                        return false;
                    }
                }

                _entries.Add(entry);
                return true;
            }
        }

        /// <summary>Looks up an accepted translation, or null. When <paramref name="styleId"/> is given, style-specific entries win over style-agnostic ones.</summary>
        public string? Lookup(string sourceText, string sourceLanguageCode, string targetLanguageCode, string? styleId = null)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                return null;
            }

            lock (_lock)
            {
                var key = NormalizeForMatch(sourceText);
                TranslationMemoryEntry? styleMatch = null;
                TranslationMemoryEntry? plainMatch = null;
                foreach (var entry in _entries)
                {
                    if (!entry.Enabled ||
                        entry.SourceLanguageCode != sourceLanguageCode ||
                        entry.TargetLanguageCode != targetLanguageCode)
                    {
                        continue;
                    }

                    if (NormalizeForMatch(entry.SourceText) != key)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(styleId) && entry.StyleId == styleId)
                    {
                        styleMatch = entry;
                        break;
                    }

                    if (string.IsNullOrEmpty(entry.StyleId))
                    {
                        plainMatch ??= entry;
                    }
                }

                var match = styleMatch ?? plainMatch;
                return match?.TargetText;
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };
    }
}
