using System.Text;

namespace Nikse.SubtitleEdit.UiLogic.Translate.Arabic
{
    /// <summary>
    /// How Latin punctuation inside an Arabic line is handled after translation.
    /// </summary>
    public enum ArabicPunctuationPolicy
    {
        /// <summary>Leave the engine output untouched.</summary>
        KeepAsIs,

        /// <summary>Convert sentence punctuation (?,;) to Arabic forms (؟،؛) in mostly-Arabic lines.</summary>
        ConvertToArabic,
    }

    /// <summary>
    /// How straight quotation marks (<c>"</c>) inside an Arabic line are handled after translation.
    /// </summary>
    public enum ArabicQuotePolicy
    {
        /// <summary>Leave the engine output untouched.</summary>
        KeepAsIs,

        /// <summary>Convert paired straight quotes to Arabic guillemets (« ») in mostly-Arabic lines.</summary>
        ConvertToGuillemets,
    }

    /// <summary>
    /// Which digit forms an Arabic target line should use.
    /// </summary>
    public enum ArabicNumeralPolicy
    {
        /// <summary>Keep whatever the engine produced.</summary>
        KeepEngineOutput,

        /// <summary>Convert Western digits (0-9) to Arabic-Indic digits (٠-٩) in mostly-Arabic lines.</summary>
        ArabicIndic,

        /// <summary>Convert Arabic-Indic digits to Western digits in mostly-Arabic lines.</summary>
        Western,
    }

    /// <summary>
    /// A first-class Arabic translation profile: language/dialect target plus the text policies the
    /// engine itself must stay ignorant of. Profiles are data only - they never select an engine and
    /// never contain engine-specific code - so the same profile can ride any <see cref="IAutoTranslator"/>.
    /// <para>
    /// The prompt addendum uses the same {0}/{1} placeholders the engines already substitute
    /// (source/target language), so it can be appended to any engine prompt verbatim. It deliberately
    /// contains no literal braces of its own: several engines run their prompt through
    /// <see cref="string.Format(string, object[])"/>, where a stray brace would throw.
    /// </para>
    /// </summary>
    public sealed class ArabicTranslationProfile
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string TargetLanguageCode { get; }

        /// <summary>Engine-neutral instruction block appended to the engine prompt when this profile is active.</summary>
        public string PromptAddendum { get; }

        public ArabicPunctuationPolicy PunctuationPolicy { get; }
        public ArabicQuotePolicy QuotePolicy { get; }
        public ArabicNumeralPolicy NumeralPolicy { get; }

        /// <summary>
        /// When true, orphaned/unbalanced Unicode bidi control characters (U+202A..U+202E, U+2066..U+2069)
        /// found in the translated line are stripped. Stripping is safe: they are invisible controls,
        /// and the editor's own "Remove RTL Unicode tags" command does the same by hand.
        /// </summary>
        public bool StripBidiControls { get; }

        public ArabicTranslationProfile(
            string id,
            string displayName,
            string targetLanguageCode,
            string promptAddendum,
            ArabicPunctuationPolicy punctuationPolicy,
            ArabicQuotePolicy quotePolicy,
            ArabicNumeralPolicy numeralPolicy,
            bool stripBidiControls)
        {
            Id = id;
            DisplayName = displayName;
            TargetLanguageCode = targetLanguageCode;
            PromptAddendum = promptAddendum;
            PunctuationPolicy = punctuationPolicy;
            QuotePolicy = quotePolicy;
            NumeralPolicy = numeralPolicy;
            StripBidiControls = stripBidiControls;
        }

        /// <summary>True when this profile targets Egyptian Arabic (ISO 639-3 arz).</summary>
        public bool IsEgyptian => TargetLanguageCode == "arz";

        /// <summary>True when this profile targets Modern Standard Arabic (ar).</summary>
        public bool IsModernStandard => TargetLanguageCode == "ar";
    }
}
