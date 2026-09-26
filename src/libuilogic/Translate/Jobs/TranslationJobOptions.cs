namespace Nikse.SubtitleEdit.UiLogic.Translate.Jobs
{
    /// <summary>
    /// Everything a translate-video job needs to know about *what to produce* - never about which
    /// engine instance to use (that is passed to the runner, so engines stay replaceable).
    /// Plain serializable data: this is what the checkpoint file persists for resume.
    /// Credentials are deliberately NOT part of the options; they live only in the app settings.
    /// </summary>
    public sealed class TranslationJobOptions
    {
        public string SourceLanguageCode { get; set; } = "en";
        public string TargetLanguageCode { get; set; } = "ar";

        /// <summary>English display name of the source language (for prompts), e.g. "English".</summary>
        public string SourceLanguageName { get; set; } = "English";

        /// <summary>English display name of the target language (for prompts), e.g. "Arabic".</summary>
        public string TargetLanguageName { get; set; } = "Arabic";

        /// <summary>Arabic profile id ("ar-msa"/"ar-eg") when the target is Arabic; null otherwise.</summary>
        public string? ArabicProfileId { get; set; }

        /// <summary>Optional extra style instruction block (documentary/technical/...) appended to the prompt.</summary>
        public string? StylePromptAddendum { get; set; }

        /// <summary>Name of the engine used, for the checkpoint file only.</summary>
        public string EngineName { get; set; } = string.Empty;

        /// <summary>Use the translation memory for pre-fills and accepted-translation recording.</summary>
        public bool UseTranslationMemory { get; set; }

        /// <summary>Folder holding translation_memory.json; empty/null = the app-wide default.</summary>
        public string? TranslationMemoryFolder { get; set; }

        /// <summary>Optional project glossary file path.</summary>
        public string? GlossaryFilePath { get; set; }

        /// <summary>Apply the Arabic profile text policies (punctuation/quotes/bidi) after translation.</summary>
        public bool ApplyArabicPolicies { get; set; } = true;

        /// <summary>Run the QA layer after translation.</summary>
        public bool RunQualityCheck { get; set; } = true;

        /// <summary>Automatically apply the safe QA fixes (profile text policies) without asking.</summary>
        public bool ApplySafeFixes { get; set; } = true;
    }
}
