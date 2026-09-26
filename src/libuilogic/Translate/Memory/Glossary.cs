using System.Text.Json;

namespace Nikse.SubtitleEdit.UiLogic.Translate.Memory
{
    /// <summary>
    /// One glossary term: a source term and its preferred translation.
    /// Used for prompt injection (translation engines), QA (terminology compliance) and
    /// highlighting in the review workspace. A term can be disabled without deleting it.
    /// </summary>
    public sealed class GlossaryTerm
    {
        public string Term { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;

        /// <summary>Optional comment for the user (e.g. "character name", "brand").</summary>
        public string? Comment { get; set; }

        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// A project glossary: a JSON-file backed list of term→preferred-translation pairs.
    /// The default file lives next to the video/subtitle (<c>&lt;name&gt;.glossary.json</c>) so a
    /// series or project can carry its glossary with it; a custom path can be passed instead.
    /// </summary>
    public sealed class Glossary
    {
        private const string FileSuffix = ".glossary.json";

        private readonly object _lock = new object();
        private readonly List<GlossaryTerm> _terms;
        private readonly string _filePath;

        public Glossary(string? filePath)
        {
            _filePath = string.IsNullOrWhiteSpace(filePath) ? "glossary" + FileSuffix : filePath;
            _terms = new List<GlossaryTerm>();
        }

        public string FilePath => _filePath;

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _terms.Count;
                }
            }
        }

        /// <summary>Builds the conventional per-media glossary file path for a subtitle/video path.</summary>
        public static string GetDefaultFilePath(string mediaOrSubtitlePath)
        {
            if (string.IsNullOrWhiteSpace(mediaOrSubtitlePath))
            {
                return "glossary" + FileSuffix;
            }

            var dir = Path.GetDirectoryName(mediaOrSubtitlePath);
            var name = Path.GetFileNameWithoutExtension(mediaOrSubtitlePath);
            var fileName = name + FileSuffix;
            return string.IsNullOrEmpty(dir) ? fileName : Path.Combine(dir, fileName);
        }

        /// <summary>Loads terms from the backing file. A missing file is not an error (empty glossary).</summary>
        public void Load()
        {
            lock (_lock)
            {
                _terms.Clear();
                if (!File.Exists(_filePath))
                {
                    return;
                }

                try
                {
                    var json = File.ReadAllText(_filePath);
                    var loaded = JsonSerializer.Deserialize<List<GlossaryTerm>>(json, JsonOptions);
                    if (loaded != null)
                    {
                        _terms.AddRange(loaded);
                    }
                }
                catch (Exception)
                {
                    // A corrupt glossary must never break a translation run - start empty.
                    _terms.Clear();
                }
            }
        }

        /// <summary>Writes all terms back to the backing file.</summary>
        public void Save()
        {
            List<GlossaryTerm> copy;
            lock (_lock)
            {
                copy = new List<GlossaryTerm>(_terms);
            }

            var folder = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(copy, JsonOptions), new System.Text.UTF8Encoding(false));
        }

        public void AddOrUpdate(GlossaryTerm term)
        {
            if (term == null || string.IsNullOrWhiteSpace(term.Term))
            {
                return;
            }

            lock (_lock)
            {
                var key = term.Term.Trim();
                foreach (var existing in _terms)
                {
                    if (string.Equals(existing.Term.Trim(), key, StringComparison.OrdinalIgnoreCase))
                    {
                        existing.Translation = term.Translation;
                        existing.Comment = term.Comment;
                        existing.Enabled = term.Enabled;
                        return;
                    }
                }

                _terms.Add(term);
            }
        }

        /// <summary>Removes a term (case-insensitive term match). Returns true when something was removed.</summary>
        public bool Remove(string term)
        {
            lock (_lock)
            {
                for (var i = 0; i < _terms.Count; i++)
                {
                    if (string.Equals(_terms[i].Term.Trim(), term?.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        _terms.RemoveAt(i);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>All enabled terms (snapshot).</summary>
        public List<GlossaryTerm> GetEnabledTerms()
        {
            lock (_lock)
            {
                return _terms.Where(t => t.Enabled && !string.IsNullOrWhiteSpace(t.Term)).ToList();
            }
        }

        /// <summary>All terms (snapshot), including disabled ones - for the editing UI.</summary>
        public List<GlossaryTerm> GetAllTerms()
        {
            lock (_lock)
            {
                return new List<GlossaryTerm>(_terms);
            }
        }

        /// <summary>
        /// Finds enabled terms whose source term occurs in the given text (ordinal, case-insensitive;
        /// Arabic diacritic-insensitive on both sides). Returns the matched terms.
        /// </summary>
        public List<GlossaryTerm> FindMatches(string text)
        {
            var result = new List<GlossaryTerm>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            var normalizedText = TranslationMemory.NormalizeForMatch(text);
            foreach (var term in GetEnabledTerms())
            {
                var normalizedTerm = TranslationMemory.NormalizeForMatch(term.Term);
                if (normalizedTerm.Length > 0 && normalizedText.Contains(normalizedTerm, StringComparison.Ordinal))
                {
                    result.Add(term);
                }
            }

            return result;
        }

        /// <summary>
        /// Renders the glossary as prompt text lines ("term = translation"), or an empty string
        /// when there are no enabled terms. Matches the format the llama.cpp advanced engine's
        /// own glossary box uses, so the same lines work everywhere.
        /// </summary>
        public string ToPromptText()
        {
            var terms = GetEnabledTerms();
            if (terms.Count == 0)
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder();
            foreach (var term in terms)
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }

                sb.Append(term.Term.Trim()).Append(" = ").Append(term.Translation.Trim());
            }

            return sb.ToString();
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };
    }
}
