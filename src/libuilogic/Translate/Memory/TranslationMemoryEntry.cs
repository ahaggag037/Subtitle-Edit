using System.Text.Json;

namespace Nikse.SubtitleEdit.UiLogic.Translate.Memory
{
    /// <summary>
    /// One local translation-memory entry: a source segment and its accepted translation, plus
    /// optional provenance. Entries are matched by normalized exact source text and language pair;
    /// <see cref="StyleId"/> optionally narrows a match to a translation style (e.g. an Arabic
    /// dialect profile id).
    /// </summary>
    public sealed class TranslationMemoryEntry
    {
        public string SourceText { get; set; } = string.Empty;
        public string TargetText { get; set; } = string.Empty;
        public string SourceLanguageCode { get; set; } = string.Empty;
        public string TargetLanguageCode { get; set; } = string.Empty;

        /// <summary>Optional style/profile id the translation was made under (e.g. "ar-eg"). Null/empty matches any style.</summary>
        public string? StyleId { get; set; }

        /// <summary>Optional engine name that produced the translation (informational only).</summary>
        public string? Engine { get; set; }

        /// <summary>Disabled entries are kept but never matched.</summary>
        public bool Enabled { get; set; } = true;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
