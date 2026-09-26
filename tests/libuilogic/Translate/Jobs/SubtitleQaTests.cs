using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.UiLogic.Translate.Arabic;
using Nikse.SubtitleEdit.UiLogic.Translate.Memory;
using Nikse.SubtitleEdit.UiLogic.Translate.Qa;

namespace LibUiLogicTests.Translate.Jobs
{
    public class SubtitleQaTests
    {
        private static QaSettings Settings() => new QaSettings();

        private static Paragraph Source(string text, double startMs, double endMs, int number)
        {
            return new Paragraph(text, startMs, endMs) { Number = number };
        }

        [Fact]
        public void EmptyTranslation_IsError()
        {
            var source = new List<Paragraph> { Source("Hello there", 1000, 3000, 1) };
            var target = new List<Paragraph> { Source("", 1000, 3000, 1) };

            var result = SubtitleQaService.Check(source, target, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, null);

            Assert.Equal(1, result.ErrorCount);
            Assert.Contains(result.Issues, i => i.CheckId == QaCheckIds.EmptyTranslation && i.Severity == QaSeverity.Error);
        }

        [Fact]
        public void MusicLineWithEmptyTarget_Passes()
        {
            var source = new List<Paragraph> { Source("♪ La la la ♪", 1000, 3000, 1) };
            var target = new List<Paragraph> { Source("", 1000, 3000, 1) };

            var result = SubtitleQaService.Check(source, target, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, null);

            Assert.Equal(0, result.ErrorCount);
            Assert.Equal(QaRowStatus.Pass, result.GetRowStatus(0));
        }

        [Fact]
        public void UntranslatedLine_IsWarning()
        {
            var source = new List<Paragraph> { Source("Hello there my good friend", 1000, 3000, 1) };
            var target = new List<Paragraph> { Source("Hello there my good friend", 1000, 3000, 1) };

            var result = SubtitleQaService.Check(source, target, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, null);

            Assert.Contains(result.Issues, i => i.CheckId == QaCheckIds.UntranslatedLine && i.Severity == QaSeverity.Warning);
        }

        [Fact]
        public void LatinPunctuationInArabicLine_IsAutoFixableWarning_AndSafeFixApplies()
        {
            var source = new List<Paragraph> { Source("Are you done?", 1000, 3000, 1) };
            var target = new List<Paragraph> { Source("هل انتهيت?", 1000, 3000, 1) };

            var result = SubtitleQaService.Check(source, target, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, null);

            var issue = result.Issues.FirstOrDefault(i => i.CheckId == QaCheckIds.LatinPunctuation);
            Assert.NotNull(issue);
            Assert.True(issue!.AutoFixable);

            var subtitle = new Subtitle();
            subtitle.Paragraphs.Add(new Paragraph("هل انتهيت?", 1000, 3000));
            var fixes = SubtitleQaService.ApplySafeFixes(subtitle, ArabicProfileCatalog.ModernStandard);
            Assert.Single(fixes);
            Assert.Equal("هل انتهيت؟", subtitle.Paragraphs[0].Text);
        }

        [Fact]
        public void BidiControls_AreFlaggedAndStrippedBySafeFix()
        {
            var text = "نص" + '\u202B' + "مختلط" + '\u202C';
            var source = new List<Paragraph> { Source("Mixed text", 1000, 3000, 1) };
            var target = new List<Paragraph> { Source(text, 1000, 3000, 1) };

            var result = SubtitleQaService.Check(source, target, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, null);

            var issue = result.Issues.FirstOrDefault(i => i.CheckId == QaCheckIds.BidiControls);
            Assert.NotNull(issue);
            Assert.True(issue!.AutoFixable);

            var subtitle = new Subtitle();
            subtitle.Paragraphs.Add(new Paragraph(text, 1000, 3000));
            SubtitleQaService.ApplySafeFixes(subtitle, ArabicProfileCatalog.ModernStandard);
            Assert.False(ArabicTextPostProcessor.ContainsBidiControls(subtitle.Paragraphs[0].Text));
        }

        [Fact]
        public void LineTooLong_IsWarning()
        {
            var longArabic = new string('ا', 60);
            var source = new List<Paragraph> { Source("Short", 1000, 3000, 1) };
            var target = new List<Paragraph> { Source(longArabic, 1000, 3000, 1) };

            var result = SubtitleQaService.Check(source, target, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, null);

            Assert.Contains(result.Issues, i => i.CheckId == QaCheckIds.LineTooLong);
        }

        [Fact]
        public void ReadingSpeed_ExceedingMaxCps_IsWarning_AndCountsDiacriticsAsFree()
        {
            // 30 Arabic letters in 1 second = 30 CPS > default max of 25.
            var plain = new string('ا', 30);
            var diacritized = new string('ا', 30).Replace("ا", "اَ"); // letter + fatha pairs, still 30 letters
            var source = new List<Paragraph> { Source("Some long source text", 1000, 2000, 1) };
            var targetPlain = new List<Paragraph> { Source(plain, 1000, 2000, 1) };
            var targetDiacritized = new List<Paragraph> { Source(diacritized, 1000, 2000, 1) };

            var resultPlain = SubtitleQaService.Check(source, targetPlain, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, null);
            var resultDiacritized = SubtitleQaService.Check(source, targetDiacritized, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, null);

            Assert.Contains(resultPlain.Issues, i => i.CheckId == QaCheckIds.ReadingSpeed);
            // The diacritized version has the same number of *letters*; its CPS must not be higher.
            var cpsPlain = resultPlain.Issues.Count(i => i.CheckId == QaCheckIds.ReadingSpeed);
            var cpsDiacritized = resultDiacritized.Issues.Count(i => i.CheckId == QaCheckIds.ReadingSpeed);
            Assert.Equal(cpsPlain, cpsDiacritized);
        }

        [Fact]
        public void DurationLimits_AreReported()
        {
            var source = new List<Paragraph> { Source("Hello there", 1000, 1150, 1) };
            var target = new List<Paragraph> { Source("مرحبا بك", 1000, 1150, 1) };

            var result = SubtitleQaService.Check(source, target, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, null);

            Assert.Contains(result.Issues, i => i.CheckId == QaCheckIds.MinDuration);
        }

        [Fact]
        public void TimeCodeOverlap_IsError()
        {
            var source = new List<Paragraph>
            {
                Source("First line", 1000, 4000, 1),
                Source("Second line", 3000, 5000, 2),
            };
            var target = new List<Paragraph>
            {
                Source("السطر الأول", 1000, 4000, 1),
                Source("السطر الثاني", 3000, 5000, 2),
            };

            var result = SubtitleQaService.Check(source, target, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, null);

            Assert.Contains(result.Issues, i => i.CheckId == QaCheckIds.TimeCodeOverlap && i.Severity == QaSeverity.Error);
        }

        [Fact]
        public void DuplicateTranslation_IsWarning()
        {
            var source = new List<Paragraph>
            {
                Source("Good morning everyone", 1000, 3000, 1),
                Source("Hello and welcome friends", 3000, 5000, 2),
            };
            var target = new List<Paragraph>
            {
                Source("صباح الخير جميعا", 1000, 3000, 1),
                Source("صباح الخير جميعا", 3000, 5000, 2),
            };

            var result = SubtitleQaService.Check(source, target, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, null);

            Assert.Contains(result.Issues, i => i.CheckId == QaCheckIds.DuplicateTranslation);
        }

        [Fact]
        public void GlossaryViolation_IsWarning_WhenPreferredTranslationMissing()
        {
            var glossary = new Glossary(null);
            glossary.AddOrUpdate(new GlossaryTerm { Term = "Stark Industries", Translation = "صناعات ستارك" });

            var source = new List<Paragraph> { Source("Stark Industries signed the contract", 1000, 3000, 1) };
            var target = new List<Paragraph> { Source("وقعت الشركة العقد أمس", 1000, 3000, 1) };

            var result = SubtitleQaService.Check(source, target, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, glossary);

            Assert.Contains(result.Issues, i => i.CheckId == QaCheckIds.GlossaryViolation);
        }

        [Fact]
        public void Glossary_Complied_WhenPreferredTranslationPresent()
        {
            var glossary = new Glossary(null);
            glossary.AddOrUpdate(new GlossaryTerm { Term = "contract", Translation = "العقد" });

            var source = new List<Paragraph> { Source("They signed the contract", 1000, 3000, 1) };
            var target = new List<Paragraph> { Source("وقعوا العقد أمس", 1000, 3000, 1) };

            var result = SubtitleQaService.Check(source, target, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, glossary);

            Assert.DoesNotContain(result.Issues, i => i.CheckId == QaCheckIds.GlossaryViolation);
        }

        [Fact]
        public void MixedDirectionLongLatinRun_IsHinted()
        {
            var source = new List<Paragraph> { Source("Open the configuration file", 1000, 3000, 1) };
            var target = new List<Paragraph> { Source("افتح الملف configurationfile هنا", 1000, 3000, 1) };

            var result = SubtitleQaService.Check(source, target, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, null);

            Assert.Contains(result.Issues, i => i.CheckId == QaCheckIds.MixedDirectionHint);
        }

        [Fact]
        public void RowStatus_Classification()
        {
            var source = new List<Paragraph> { Source("Hello", 1000, 3000, 1) };
            var target = new List<Paragraph> { Source("مرحبا", 1000, 3000, 1) };

            var result = SubtitleQaService.Check(source, target, Settings(), isTargetArabic: true, ArabicProfileCatalog.ModernStandard, null);

            Assert.Equal(0, result.ErrorCount);
            Assert.Equal(QaRowStatus.Pass, result.GetRowStatus(0));
        }

        [Fact]
        public void NonArabicTarget_SkipsArabicChecks()
        {
            var source = new List<Paragraph> { Source("Hello?", 1000, 3000, 1) };
            var target = new List<Paragraph> { Source("Hola?", 1000, 3000, 1) };

            var result = SubtitleQaService.Check(source, target, Settings(), isTargetArabic: false, null, null);

            Assert.DoesNotContain(result.Issues, i => i.CheckId == QaCheckIds.LatinPunctuation);
            Assert.DoesNotContain(result.Issues, i => i.CheckId == QaCheckIds.StraightQuotes);
        }
    }
}
