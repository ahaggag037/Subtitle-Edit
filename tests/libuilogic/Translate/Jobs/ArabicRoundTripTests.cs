using System.Text;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.UiLogic.Translate.Arabic;
using Nikse.SubtitleEdit.UiLogic.Translate.Qa;

namespace LibUiLogicTests.Translate.Jobs
{
    /// <summary>
    /// Focused Arabic round-trip coverage (Phase C 9): Arabic text survives a full
    /// export/parse cycle unchanged, and the QA safe-fix pass edits text only - timing and
    /// numbering are never mutated by the new Arabic layers.
    /// </summary>
    public class ArabicRoundTripTests : IDisposable
    {
        private readonly string _folder;

        public ArabicRoundTripTests()
        {
            _folder = Path.Combine(Path.GetTempPath(), "se-arabic-roundtrip-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch
            {
            }
        }

        private static Subtitle BuildArabicSubtitle()
        {
            var subtitle = new Subtitle();
            subtitle.Paragraphs.Add(new Paragraph("مرحبا بكم في هذا الفيلم", 1000, 3500) { Number = 1 });
            subtitle.Paragraphs.Add(new Paragraph("هل هذا «سؤال» حقيقي؟", 4000, 6000) { Number = 2 });
            subtitle.Paragraphs.Add(new Paragraph("نعم؛ وهنا الجواب، بأرقام ١٢٣", 6500, 9000) { Number = 3 });
            return subtitle;
        }

        [Fact]
        public void ArabicSrt_ExportReParse_PreservesTextTimingAndNumbering()
        {
            var original = BuildArabicSubtitle();
            var path = Path.Combine(_folder, "arabic-out.srt");

            var srtText = original.ToText(new SubRip());
            File.WriteAllText(path, srtText, new UTF8Encoding(false));

            var reParsed = new Subtitle();
            new SubRip().LoadSubtitle(reParsed, File.ReadAllLines(path).ToList(), null);

            Assert.Equal(original.Paragraphs.Count, reParsed.Paragraphs.Count);
            for (var i = 0; i < original.Paragraphs.Count; i++)
            {
                Assert.Equal(original.Paragraphs[i].Text, reParsed.Paragraphs[i].Text);
                Assert.Equal(original.Paragraphs[i].Number, reParsed.Paragraphs[i].Number);
                Assert.Equal(original.Paragraphs[i].StartTime.TotalMilliseconds, reParsed.Paragraphs[i].StartTime.TotalMilliseconds);
                Assert.Equal(original.Paragraphs[i].EndTime.TotalMilliseconds, reParsed.Paragraphs[i].EndTime.TotalMilliseconds);
            }
        }

        [Fact]
        public void MixedArabicLatin_SurvivesExportReParse()
        {
            var subtitle = new Subtitle();
            subtitle.Paragraphs.Add(new Paragraph("كلمة WiFi ثم جهاز GPU في الجملة", 1000, 3000) { Number = 1 });
            subtitle.Paragraphs.Add(new Paragraph("He said \"tayyib\" and wrote 42", 3500, 5500) { Number = 2 });

            var path = Path.Combine(_folder, "mixed.srt");
            File.WriteAllText(path, subtitle.ToText(new SubRip()), new UTF8Encoding(false));

            var reParsed = new Subtitle();
            new SubRip().LoadSubtitle(reParsed, File.ReadAllLines(path).ToList(), null);

            Assert.Equal(subtitle.Paragraphs[0].Text, reParsed.Paragraphs[0].Text);
            Assert.Equal(subtitle.Paragraphs[1].Text, reParsed.Paragraphs[1].Text);
        }

        [Fact]
        public void QaSafeFixes_NeverMutateTimingOrNumbering()
        {
            var target = new Subtitle();
            target.Paragraphs.Add(new Paragraph("سطر بأقواس لاتينية?", 1000, 3500) { Number = 1 });
            target.Paragraphs.Add(new Paragraph("سطر ثانٍ، بالفواصل اللاتينية", 4000, 6000) { Number = 2 });
            target.Paragraphs.Add(new Paragraph("A latin line stays as it is", 6500, 8000) { Number = 3 });

            var beforeStart = target.Paragraphs.Select(p => p.StartTime.TotalMilliseconds).ToList();
            var beforeEnd = target.Paragraphs.Select(p => p.EndTime.TotalMilliseconds).ToList();
            var beforeNumbers = target.Paragraphs.Select(p => p.Number).ToList();

            var fixes = SubtitleQaService.ApplySafeFixes(target, ArabicProfileCatalog.ModernStandard);

            // Text was polished, but never the timeline or the numbering.
            Assert.True(fixes.Count > 0);
            Assert.Equal(beforeStart, target.Paragraphs.Select(p => p.StartTime.TotalMilliseconds).ToList());
            Assert.Equal(beforeEnd, target.Paragraphs.Select(p => p.EndTime.TotalMilliseconds).ToList());
            Assert.Equal(beforeNumbers, target.Paragraphs.Select(p => p.Number).ToList());

            Assert.Contains("؟", target.Paragraphs[0].Text, StringComparison.Ordinal);
            Assert.Contains("،", target.Paragraphs[1].Text, StringComparison.Ordinal);
            Assert.DoesNotContain("؟", target.Paragraphs[2].Text, StringComparison.Ordinal);
        }

        [Fact]
        public void EgyptianProfile_PoliciesDoNotClaimDialectGeneration()
        {
            // The Egyptian profile is honest about what it is: a prompt instruction plus text
            // policies. Nothing in the pipeline claims to "generate" dialect - that is a model
            // capability. Guard the contract: the profile is a distinct target code and its
            // prompt is an instruction, not a claim.
            var egyptian = ArabicProfileCatalog.Egyptian;
            Assert.Equal("arz", egyptian.TargetLanguageCode);
            Assert.NotEqual(ArabicProfileCatalog.ModernStandard.TargetLanguageCode, egyptian.TargetLanguageCode);
            Assert.Contains("Egyptian", egyptian.PromptAddendum, StringComparison.OrdinalIgnoreCase);
        }
    }
}
