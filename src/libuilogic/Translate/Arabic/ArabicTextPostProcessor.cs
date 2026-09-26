using System.Text;
using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.UiLogic.Translate.Arabic
{
    /// <summary>
    /// Deterministic, engine-independent Arabic text transforms and checks that run after a
    /// translation comes back, driven purely by an <see cref="ArabicTranslationProfile"/>.
    /// Everything here is line-local and safe to run twice (idempotent).
    /// <para>
    /// A line counts as "mostly Arabic" when at least three Arabic-script letters occur in it and
    /// Latin letters are a minority of all letters. Policies are never applied to lines that are
    /// mostly Latin (URLs, English fragments kept on purpose) and never inside ASSA override
    /// blocks or HTML tags.
    /// </para>
    /// </summary>
    public static class ArabicTextPostProcessor
    {
        /// <summary>First code point of the Arabic block.</summary>
        private const char ArabicBlockStart = '\u0600';
        /// <summary>Last code point of the Arabic block.</summary>
        private const char ArabicBlockEnd = '\u06FF';
        private const char ArabicSupplementStart = '\u0750';
        private const char ArabicSupplementEnd = '\u077F';
        private const char ArabicExtendedStart = '\u08A0';
        private const char ArabicExtendedEnd = '\u08FF';
        private const char ArabicPresentationAStart = '\uFB50';
        private const char ArabicPresentationAEnd = '\uFDFF';
        private const char ArabicPresentationBStart = '\uFE70';
        private const char ArabicPresentationBEnd = '\uFEFF';

        /// <summary>Minimum count of Arabic letters for a line to be treated as Arabic text.</summary>
        private const int MinimumArabicLetters = 3;

        /// <summary>The Arabic question mark.</summary>
        public const char ArabicQuestionMark = '\u061F';
        /// <summary>The Arabic comma.</summary>
        public const char ArabicComma = '\u060C';
        /// <summary>The Arabic semicolon.</summary>
        public const char ArabicSemicolon = '\u061B';
        /// <summary>Arabic-Indic zero (digits ٠-٩ are U+0660..U+0669).</summary>
        private const char ArabicIndicZero = '\u0660';

        public static bool IsArabicLetter(char c)
        {
            return (c >= ArabicBlockStart && c <= ArabicBlockEnd) ||
                   (c >= ArabicSupplementStart && c <= ArabicSupplementEnd) ||
                   (c >= ArabicExtendedStart && c <= ArabicExtendedEnd) ||
                   (c >= ArabicPresentationAStart && c <= ArabicPresentationAEnd) ||
                   (c >= ArabicPresentationBStart && c <= ArabicPresentationBEnd);
        }

        /// <summary>True when the character is a letter in the basic Latin range.</summary>
        public static bool IsLatinLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        /// <summary>
        /// True when the line should be treated as Arabic text for policy purposes: enough Arabic
        /// letters and no Latin-letter majority.
        /// </summary>
        public static bool IsMostlyArabic(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var arabic = 0;
            var latin = 0;
            foreach (var c in text)
            {
                if (IsArabicLetter(c))
                {
                    arabic++;
                }
                else if (IsLatinLetter(c))
                {
                    latin++;
                }
            }

            return arabic >= MinimumArabicLetters && arabic >= latin;
        }

        /// <summary>
        /// Applies the profile's text policies to one subtitle line (a subtitle text may contain
        /// several display lines separated by newlines - each is processed independently).
        /// Returns the new text; when nothing changed the original string instance is returned.
        /// </summary>
        public static string ApplyProfile(string text, ArabicTranslationProfile profile)
        {
            if (string.IsNullOrEmpty(text) || profile == null)
            {
                return text;
            }

            var lines = text.SplitToLines();
            if (lines.Count == 0)
            {
                return text;
            }

            var changed = false;
            var sb = new StringBuilder(text.Length + 8);
            for (var i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(Environment.NewLine);
                }

                var line = lines[i];
                var converted = ApplyProfileToSingleLine(line, profile);
                if (!ReferenceEquals(converted, line) && converted != line)
                {
                    changed = true;
                }

                sb.Append(converted);
            }

            return changed ? sb.ToString() : text;
        }

        private static string ApplyProfileToSingleLine(string line, ArabicTranslationProfile profile)
        {
            var result = line;

            if (profile.StripBidiControls && ContainsBidiControls(result))
            {
                result = StripBidiControls(result);
            }

            if (!IsMostlyArabic(result))
            {
                return result;
            }

            if (profile.PunctuationPolicy == ArabicPunctuationPolicy.ConvertToArabic)
            {
                result = ConvertPunctuation(result);
            }

            if (profile.QuotePolicy == ArabicQuotePolicy.ConvertToGuillemets)
            {
                result = ConvertStraightQuotes(result);
            }

            if (profile.NumeralPolicy == ArabicNumeralPolicy.ArabicIndic)
            {
                result = ConvertDigits(result, toArabicIndic: true);
            }
            else if (profile.NumeralPolicy == ArabicNumeralPolicy.Western)
            {
                result = ConvertDigits(result, toArabicIndic: false);
            }

            return result;
        }

        /// <summary>
        /// Converts Latin sentence punctuation (?,;) to Arabic forms outside of brace blocks and
        /// HTML tags. A question mark only converts when the following character is whitespace,
        /// end of line, or a closing quote/brace - so a Latin "?" inside a URL path is untouched.
        /// </summary>
        public static string ConvertPunctuation(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return line;
            }

            var sb = new StringBuilder(line.Length);
            var inAssaBlock = false;
            var inHtmlTag = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '{')
                {
                    inAssaBlock = true;
                }
                else if (c == '}')
                {
                    inAssaBlock = false;
                }
                else if (c == '<')
                {
                    inHtmlTag = true;
                }
                else if (c == '>')
                {
                    inHtmlTag = false;
                }

                if (inAssaBlock || inHtmlTag)
                {
                    sb.Append(c);
                    continue;
                }

                if (c == '?')
                {
                    var nextIsBoundary = i + 1 >= line.Length ||
                                         char.IsWhiteSpace(line[i + 1]) ||
                                         line[i + 1] == '"' ||
                                         line[i + 1] == '»' ||
                                         line[i + 1] == '\'';
                    if (nextIsBoundary)
                    {
                        sb.Append(ArabicQuestionMark);
                        continue;
                    }
                }
                else if (c == ',')
                {
                    var prevIsArabicOrSpace = i > 0 && (IsArabicLetter(line[i - 1]) || char.IsWhiteSpace(line[i - 1]) || line[i - 1] == '"');
                    var nextIsBoundary = i + 1 >= line.Length || char.IsWhiteSpace(line[i + 1]);
                    if (prevIsArabicOrSpace && nextIsBoundary)
                    {
                        sb.Append(ArabicComma);
                        continue;
                    }
                }
                else if (c == ';')
                {
                    var prevIsArabic = i > 0 && IsArabicLetter(line[i - 1]);
                    var nextIsBoundary = i + 1 >= line.Length || char.IsWhiteSpace(line[i + 1]);
                    if (prevIsArabic && nextIsBoundary)
                    {
                        sb.Append(ArabicSemicolon);
                        continue;
                    }
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Converts paired straight double quotes to Arabic guillemets in an Arabic line: the first
        /// quote of a pair becomes «, the second ». An odd number of quotes leaves the last one as is.
        /// </summary>
        public static string ConvertStraightQuotes(string line)
        {
            if (string.IsNullOrEmpty(line) || !line.Contains('"'))
            {
                return line;
            }

            var sb = new StringBuilder(line.Length);
            var open = true;
            foreach (var c in line)
            {
                if (c == '"')
                {
                    sb.Append(open ? '«' : '»');
                    open = !open;
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>Converts Western digits to Arabic-Indic (or back) in an Arabic line.</summary>
        public static string ConvertDigits(string line, bool toArabicIndic)
        {
            if (string.IsNullOrEmpty(line))
            {
                return line;
            }

            var sb = new StringBuilder(line.Length);
            foreach (var c in line)
            {
                if (toArabicIndic && c >= '0' && c <= '9')
                {
                    sb.Append((char)(ArabicIndicZero + (c - '0')));
                }
                else if (!toArabicIndic && c >= ArabicIndicZero && c <= ArabicIndicZero + 9)
                {
                    sb.Append((char)('0' + (c - ArabicIndicZero)));
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>All Unicode directional control characters the QA layer knows about.</summary>
        public static readonly char[] BidiControls =
        {
            '\u202A', '\u202B', '\u202C', '\u202D', '\u202E',
            '\u2066', '\u2067', '\u2068', '\u2069',
        };

        public static bool ContainsBidiControls(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (var c in text)
            {
                foreach (var control in BidiControls)
                {
                    if (c == control)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static int CountBidiControls(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var count = 0;
            foreach (var c in text)
            {
                foreach (var control in BidiControls)
                {
                    if (c == control)
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        /// <summary>Removes every Unicode directional control character from the text.</summary>
        public static string StripBidiControls(string text)
        {
            if (string.IsNullOrEmpty(text) || !ContainsBidiControls(text))
            {
                return text;
            }

            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                var isControl = false;
                foreach (var control in BidiControls)
                {
                    if (c == control)
                    {
                        isControl = true;
                        break;
                    }
                }

                if (!isControl)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>True when the text contains Arabic presentation forms (should have been normalized away).</summary>
        public static bool ContainsArabicPresentationForms(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (var c in text)
            {
                if ((c >= ArabicPresentationAStart && c <= ArabicPresentationAEnd) ||
                    (c >= ArabicPresentationBStart && c <= ArabicPresentationBEnd))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
