using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;

namespace LibUiLogicTests.Translate.Jobs
{
    /// <summary>
    /// Deterministic English subtitle fixture used by the translation-workflow tests. It is
    /// original text written for these tests (no private or third-party content) and covers, per
    /// line: dialogue, a proper name, pronouns, an idiom, slang, a technical phrase, a number,
    /// mixed English/Arabic, punctuation, and two speakers.
    /// </summary>
    public static class TranslateFixtures
    {
        public const string EnglishSrt = @"1
00:00:01,000 --> 00:00:03,500
- Nora, did you finish the report?
- Yes, I sent it this morning.

2
00:00:04,000 --> 00:00:06,000
She said the quantum flux capacitor
is finally stable.

3
00:00:06,500 --> 00:00:08,000
Stark Industries signed the contract yesterday!

4
00:00:08,500 --> 00:00:10,500
That presentation was a piece of cake,
honestly.

5
00:00:11,000 --> 00:00:13,000
I'm gonna check the server logs now, okay?

6
00:00:13,500 --> 00:00:15,500
He told me the Arabic word is ""tayyib"",
and wrote 42 samples in the file.

7
00:00:16,000 --> 00:00:18,000
♪ Background music with no lyrics ♪
";

        /// <summary>Parses the fixture into a Subtitle using the real SRT parser.</summary>
        public static Subtitle LoadEnglishSubtitle()
        {
            var subtitle = new Subtitle();
            new SubRip().LoadSubtitle(subtitle, EnglishSrt.SplitToLines(), null);
            return subtitle;
        }

        /// <summary>Writes the fixture to a temp SRT file and returns its path.</summary>
        public static string WriteEnglishSrtToTempFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "se-arabic-test-" + Guid.NewGuid().ToString("N") + ".srt");
            File.WriteAllText(path, EnglishSrt, new System.Text.UTF8Encoding(false));
            return path;
        }
    }
}
