namespace Nikse.SubtitleEdit.UiLogic.Translate.Arabic
{
    /// <summary>
    /// The built-in Arabic translation profiles. Only profiles whose behaviour the pipeline can
    /// actually deliver are registered here: the post-processor applies punctuation/quote/numeral
    /// policies locally, while everything that needs model capability (dialect naturalness, humor,
    /// slang) is expressed as prompt text and is therefore only as good as the selected engine -
    /// callers must not present profile selection as a guarantee of dialect quality.
    /// </summary>
    public static class ArabicProfileCatalog
    {
        /// <summary>Modern Standard Arabic (فصحى), target code "ar".</summary>
        public static ArabicTranslationProfile ModernStandard { get; } = new ArabicTranslationProfile(
            "ar-msa",
            "Arabic — Modern Standard",
            "ar",
            "The target language is Modern Standard Arabic (al-fusha). Use standard Arabic grammar and spelling. " +
            "Use Arabic punctuation marks: the Arabic question mark and the Arabic comma. " +
            "Keep proper names, brand names and technical terms recognizable; do not transliterate them into Arabic unless a well-known Arabic form exists. " +
            "Do not add vocalization (tashkeel) to the translation.",
            ArabicPunctuationPolicy.ConvertToArabic,
            ArabicQuotePolicy.ConvertToGuillemets,
            ArabicNumeralPolicy.KeepEngineOutput,
            stripBidiControls: true);

        /// <summary>
        /// Egyptian Arabic (مصري), target code "arz". Conversational register; the prompt asks for
        /// spoken-style Egyptian, but how well a given engine delivers that is a model capability,
        /// not something this profile can guarantee.
        /// </summary>
        public static ArabicTranslationProfile Egyptian { get; } = new ArabicTranslationProfile(
            "ar-eg",
            "Arabic — Egyptian (conversational)",
            "arz",
            "The target language is Egyptian Arabic (masri) as spoken in everyday conversation, not Modern Standard Arabic. " +
            "Write dialogue the way people actually talk: natural spoken wording, common everyday expressions, and direct address. " +
            "Avoid formal or literary constructions; do not use classical rhetorical style. " +
            "Keep humor, sarcasm and slang in a natural Egyptian tone instead of translating them literally. " +
            "Use Arabic punctuation marks: the Arabic question mark and the Arabic comma. " +
            "Keep proper names, brand names and technical terms recognizable; do not transliterate them into Arabic unless a well-known Arabic form exists.",
            ArabicPunctuationPolicy.ConvertToArabic,
            ArabicQuotePolicy.ConvertToGuillemets,
            ArabicNumeralPolicy.KeepEngineOutput,
            stripBidiControls: true);

        private static readonly List<ArabicTranslationProfile> All = new List<ArabicTranslationProfile>
        {
            ModernStandard,
            Egyptian,
        };

        public static IReadOnlyList<ArabicTranslationProfile> Profiles => All;

        /// <summary>Returns the profile with the given id, or null.</summary>
        public static ArabicTranslationProfile? GetById(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (var profile in All)
            {
                if (profile.Id == id)
                {
                    return profile;
                }
            }

            return null;
        }

        /// <summary>Returns the built-in profile for a target language code ("ar"/"arz"), or null.</summary>
        public static ArabicTranslationProfile? GetByLanguageCode(string? code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return null;
            }

            foreach (var profile in All)
            {
                if (profile.TargetLanguageCode == code)
                {
                    return profile;
                }
            }

            return null;
        }
    }
}
