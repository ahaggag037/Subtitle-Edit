using Nikse.SubtitleEdit.UiLogic.Translate.Arabic;

namespace LibUiLogicTests.Translate.Jobs
{
    public class ArabicTextPostProcessorTests
    {
        [Fact]
        public void IsMostlyArabic_RequiresArabicMajority()
        {
            Assert.True(ArabicTextPostProcessor.IsMostlyArabic("هذا نص عربي طويل"));
            Assert.False(ArabicTextPostProcessor.IsMostlyArabic("This is English text"));
            Assert.False(ArabicTextPostProcessor.IsMostlyArabic("اب")); // too few Arabic letters
            Assert.False(ArabicTextPostProcessor.IsMostlyArabic(""));
            Assert.False(ArabicTextPostProcessor.IsMostlyArabic(null));
        }

        [Fact]
        public void ConvertPunctuation_ConvertsQuestionMarkAtEndOfArabicLine()
        {
            var result = ArabicTextPostProcessor.ConvertPunctuation("هل أنهيت التقرير?");
            Assert.Equal("هل أنهيت التقرير؟", result);
        }

        [Fact]
        public void ConvertPunctuation_ConvertsCommaAndSemicolonInArabic()
        {
            Assert.Equal("واحد، اثنان", ArabicTextPostProcessor.ConvertPunctuation("واحد, اثنان"));
            Assert.Equal("انتبه؛ الأمر مهم", ArabicTextPostProcessor.ConvertPunctuation("انتبه; الأمر مهم"));
        }

        [Fact]
        public void ConvertPunctuation_DoesNotTouchUrlQuestionMark()
        {
            var line = "راجع https://example.com/page?id=42 اليوم";
            Assert.Equal(line, ArabicTextPostProcessor.ConvertPunctuation(line));
        }

        [Fact]
        public void ConvertPunctuation_DoesNotTouchMostlyLatinLine()
        {
            // The caller gates on IsMostlyArabic, but the function itself is line-local:
            // document that a Latin question mark after Latin text stays put even when called directly.
            Assert.Equal("Really?", ArabicTextPostProcessor.ConvertPunctuation("Really?"));
        }

        [Fact]
        public void ConvertPunctuation_SkipsAssaOverrideBlocks()
        {
            var line = "{\\i1}هل هذا صحيح?{\\i0}";
            var result = ArabicTextPostProcessor.ConvertPunctuation(line);
            Assert.Contains("{\\i1}", result);
            // The question mark sits inside the tag region? No - it is inside the text between tags,
            // so it converts; the braces themselves must remain intact.
            Assert.Equal("{\\i1}هل هذا صحيح؟{\\i0}", result);
        }

        [Fact]
        public void ConvertStraightQuotes_PairsToGuillemets()
        {
            Assert.Equal("قال «مرحبا» ثم ذهب", ArabicTextPostProcessor.ConvertStraightQuotes("قال \"مرحبا\" ثم ذهب"));
        }

        [Fact]
        public void ConvertStraightQuotes_OddCount_LeavesLastAsIs()
        {
            var result = ArabicTextPostProcessor.ConvertStraightQuotes("قال \"مرحبا ثم ذهب");
            Assert.StartsWith("قال «مرحبا ثم ذهب", result, StringComparison.Ordinal);
            Assert.DoesNotContain('»', result);
        }

        [Fact]
        public void ConvertDigits_ToArabicIndic_AndBack()
        {
            Assert.Equal("عدد ٤٢", ArabicTextPostProcessor.ConvertDigits("عدد 42", toArabicIndic: true));
            Assert.Equal("عدد 42", ArabicTextPostProcessor.ConvertDigits("عدد ٤٢", toArabicIndic: false));
        }

        [Fact]
        public void BidiControls_CountStripRoundTrip()
        {
            var text = "نص" + '\u202B' + "مختلط" + '\u202C' + "أطول";
            Assert.True(ArabicTextPostProcessor.ContainsBidiControls(text));
            Assert.Equal(2, ArabicTextPostProcessor.CountBidiControls(text));
            var stripped = ArabicTextPostProcessor.StripBidiControls(text);
            Assert.Equal("نصمختلطأطول", stripped);
            Assert.False(ArabicTextPostProcessor.ContainsBidiControls(stripped));
        }

        [Fact]
        public void ApplyProfile_StripsBidiControlsEvenWhenLineIsMostlyLatin()
        {
            var profile = ArabicProfileCatalog.ModernStandard;
            var text = "Open the file " + '\u202A' + "C:\\temp" + '\u202C' + " now";
            var result = ArabicTextPostProcessor.ApplyProfile(text, profile);
            Assert.DoesNotContain('\u202A', result);
            Assert.Contains("C:\\temp", result);
        }

        [Fact]
        public void ApplyProfile_ConvertsPunctuationAndQuotesInArabicLine()
        {
            var profile = ArabicProfileCatalog.ModernStandard;
            var result = ArabicTextPostProcessor.ApplyProfile("قال \"جيد\", هل انتهى?", profile);
            Assert.Equal("قال «جيد»، هل انتهى؟", result);
        }

        [Fact]
        public void ApplyProfile_KeepsLatinLineUntouchedExceptBidi()
        {
            var profile = ArabicProfileCatalog.ModernStandard;
            var result = ArabicTextPostProcessor.ApplyProfile("Stay as is, really?", profile);
            Assert.Equal("Stay as is, really?", result);
        }

        [Fact]
        public void ContainsArabicPresentationForms_Detected()
        {
            // U+FEE0 is an Arabic presentation-forms code point (full-width-ish contextual glyph form).
            Assert.True(ArabicTextPostProcessor.ContainsArabicPresentationForms("ﻻ" + "ﻡ"));
            Assert.False(ArabicTextPostProcessor.ContainsArabicPresentationForms("نص عربي عادي"));
        }

        [Fact]
        public void ApplyProfile_IsIdempotent()
        {
            var profile = ArabicProfileCatalog.Egyptian;
            var once = ArabicTextPostProcessor.ApplyProfile("مرحبا, كيف الحال?", profile);
            var twice = ArabicTextPostProcessor.ApplyProfile(once, profile);
            Assert.Equal(once, twice);
        }
    }
}
