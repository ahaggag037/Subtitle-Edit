using Nikse.SubtitleEdit.UiLogic.Translate.Arabic;

namespace LibUiLogicTests.Translate.Jobs
{
    public class ArabicProfileTests
    {
        [Fact]
        public void Catalog_ContainsModernStandardAndEgyptian()
        {
            Assert.Equal(2, ArabicProfileCatalog.Profiles.Count);
            Assert.NotNull(ArabicProfileCatalog.GetById("ar-msa"));
            Assert.NotNull(ArabicProfileCatalog.GetById("ar-eg"));
        }

        [Fact]
        public void ModernStandard_TargetsArCode()
        {
            var profile = ArabicProfileCatalog.ModernStandard;
            Assert.Equal("ar", profile.TargetLanguageCode);
            Assert.True(profile.IsModernStandard);
            Assert.False(profile.IsEgyptian);
        }

        [Fact]
        public void GetById_UnknownId_ReturnsNull()
        {
            Assert.Null(ArabicProfileCatalog.GetById("nope"));
            Assert.Null(ArabicProfileCatalog.GetById(null));
            Assert.Null(ArabicProfileCatalog.GetByLanguageCode("xx"));
        }

        [Fact]
        public void GetByLanguageCode_ResolvesBothDialects()
        {
            Assert.Equal("ar-msa", ArabicProfileCatalog.GetByLanguageCode("ar")!.Id);
            Assert.Equal("ar-eg", ArabicProfileCatalog.GetByLanguageCode("arz")!.Id);
        }

        [Fact]
        public void PromptAddendum_KeepsEnginePlaceholdersAndAvoidsOtherBraces()
        {
            // Engines substitute {0}/{1} themselves; several run the prompt through string.Format,
            // so ANY other brace would throw a FormatException at translation time.
            foreach (var profile in ArabicProfileCatalog.Profiles)
            {
                Assert.Contains("{0}", profile.PromptAddendum);
                Assert.Contains("{1}", profile.PromptAddendum);
                var withoutPlaceholders = profile.PromptAddendum
                    .Replace("{0}", string.Empty)
                    .Replace("{1}", string.Empty);
                Assert.DoesNotContain("{", withoutPlaceholders);
                Assert.DoesNotContain("}", withoutPlaceholders);
            }
        }

        [Fact]
        public void Profiles_UseArabicPunctuationAndQuotePolicies()
        {
            foreach (var profile in ArabicProfileCatalog.Profiles)
            {
                Assert.Equal(ArabicPunctuationPolicy.ConvertToArabic, profile.PunctuationPolicy);
                Assert.Equal(ArabicQuotePolicy.ConvertToGuillemets, profile.QuotePolicy);
                Assert.True(profile.StripBidiControls);
            }
        }
    }

    public class EgyptianArabicProfileTests
    {
        [Fact]
        public void Egyptian_TargetsArzCode()
        {
            var profile = ArabicProfileCatalog.Egyptian;
            Assert.Equal("arz", profile.TargetLanguageCode);
            Assert.True(profile.IsEgyptian);
            Assert.False(profile.IsModernStandard);
        }

        [Fact]
        public void Egyptian_IsDistinctFromModernStandard()
        {
            var msa = ArabicProfileCatalog.ModernStandard;
            var egyptian = ArabicProfileCatalog.Egyptian;
            Assert.NotEqual(msa.Id, egyptian.Id);
            Assert.NotEqual(msa.TargetLanguageCode, egyptian.TargetLanguageCode);
            Assert.NotEqual(msa.PromptAddendum, egyptian.PromptAddendum);
        }

        [Fact]
        public void Egyptian_PromptAsksForSpokenConversationalStyle()
        {
            var addendum = ArabicProfileCatalog.Egyptian.PromptAddendum;
            Assert.Contains("Egyptian Arabic", addendum);
            Assert.Contains("not Modern Standard Arabic", addendum);
            Assert.Contains("conversational", addendum, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ModernStandard_PromptAsksForStandardArabic()
        {
            var addendum = ArabicProfileCatalog.ModernStandard.PromptAddendum;
            Assert.Contains("Modern Standard Arabic", addendum);
        }
    }
}
