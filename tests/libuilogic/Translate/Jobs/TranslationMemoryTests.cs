using Nikse.SubtitleEdit.UiLogic.Translate.Memory;

namespace LibUiLogicTests.Translate.Jobs
{
    public class TranslationMemoryTests : IDisposable
    {
        private readonly string _folder;

        public TranslationMemoryTests()
        {
            _folder = Path.Combine(Path.GetTempPath(), "se-tm-tests-" + Guid.NewGuid().ToString("N"));
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
                // temp cleanup best effort
            }
        }

        private TranslationMemory NewMemory() => new TranslationMemory(_folder);

        [Fact]
        public void Add_And_Lookup_ExactMatch()
        {
            var memory = NewMemory();
            Assert.True(memory.Add(new TranslationMemoryEntry
            {
                SourceText = "Hello world",
                TargetText = "مرحبا بالعالم",
                SourceLanguageCode = "en",
                TargetLanguageCode = "ar",
            }));

            Assert.Equal("مرحبا بالعالم", memory.Lookup("Hello world", "en", "ar"));
        }

        [Fact]
        public void Lookup_IsWhitespaceAndCaseInsensitive()
        {
            var memory = NewMemory();
            memory.Add(new TranslationMemoryEntry
            {
                SourceText = "Hello   world",
                TargetText = "مرحبا",
                SourceLanguageCode = "en",
                TargetLanguageCode = "ar",
            });

            Assert.Equal("مرحبا", memory.Lookup("hello world", "en", "ar"));
            Assert.Equal("مرحبا", memory.Lookup("  HELLO    WORLD  ", "en", "ar"));
        }

        [Fact]
        public void Lookup_IsArabicDiacriticInsensitive_OnTheSource()
        {
            var memory = NewMemory();
            memory.Add(new TranslationMemoryEntry
            {
                SourceText = "قلت لك", // plain
                TargetText = "قلت لك",
                SourceLanguageCode = "ar",
                TargetLanguageCode = "arz",
            });

            // Same words with tashkeel and tatweel must still match.
            Assert.Equal("قلت لك", memory.Lookup("قُـلْـتَ لَك", "ar", "arz"));
        }

        [Fact]
        public void Lookup_RespectsLanguagePair()
        {
            var memory = NewMemory();
            memory.Add(new TranslationMemoryEntry
            {
                SourceText = "Hello",
                TargetText = "مرحبا",
                SourceLanguageCode = "en",
                TargetLanguageCode = "ar",
            });

            Assert.Null(memory.Lookup("Hello", "en", "arz"));
            Assert.Null(memory.Lookup("Hello", "fr", "ar"));
        }

        [Fact]
        public void StyleSpecificEntry_WinsOverStyleAgnostic()
        {
            var memory = NewMemory();
            memory.Add(new TranslationMemoryEntry
            {
                SourceText = "Let's go",
                TargetText = "يلا بينا",
                SourceLanguageCode = "en",
                TargetLanguageCode = "arz",
                StyleId = "ar-eg",
            });
            memory.Add(new TranslationMemoryEntry
            {
                SourceText = "Let's go",
                TargetText = "هيا بنا",
                SourceLanguageCode = "en",
                TargetLanguageCode = "arz",
            });

            Assert.Equal("يلا بينا", memory.Lookup("Let's go", "en", "arz", "ar-eg"));
            // Without a style, the style-agnostic entry matches.
            Assert.Equal("هيا بنا", memory.Lookup("Let's go", "en", "arz", null));
        }

        [Fact]
        public void Add_SameSource_UpdatesInsteadOfDuplicating()
        {
            var memory = NewMemory();
            memory.Add(new TranslationMemoryEntry { SourceText = "Hi", TargetText = "أهلاً", SourceLanguageCode = "en", TargetLanguageCode = "ar" });
            memory.Add(new TranslationMemoryEntry { SourceText = "Hi", TargetText = "مرحباً", SourceLanguageCode = "en", TargetLanguageCode = "ar" });

            Assert.Equal(1, memory.Count);
            Assert.Equal("مرحباً", memory.Lookup("Hi", "en", "ar"));
        }

        [Fact]
        public void DisabledEntry_NeverMatches()
        {
            var memory = NewMemory();
            var entry = new TranslationMemoryEntry { SourceText = "Hi", TargetText = "أهلاً", SourceLanguageCode = "en", TargetLanguageCode = "ar" };
            memory.Add(entry);
            entry.Enabled = false;

            Assert.Null(memory.Lookup("Hi", "en", "ar"));
        }

        [Fact]
        public void SaveAndLoad_RoundTrip()
        {
            var memory = NewMemory();
            memory.Add(new TranslationMemoryEntry
            {
                SourceText = "Stark Industries signed the contract",
                TargetText = "وقعت صناعات ستارك العقد",
                SourceLanguageCode = "en",
                TargetLanguageCode = "ar",
                StyleId = "ar-msa",
                Engine = "TestEngine",
            });
            memory.Save();

            var reloaded = NewMemory();
            Assert.Equal(0, reloaded.Count);
            reloaded.Load();
            Assert.Equal(1, reloaded.Count);
            Assert.Equal("وقعت صناعات ستارك العقد", reloaded.Lookup("Stark Industries signed the contract", "en", "ar", "ar-msa"));
        }

        [Fact]
        public void Load_CorruptFile_StartsEmpty()
        {
            var corrupt = Path.Combine(_folder, "translation_memory.json");
            File.WriteAllText(corrupt, "{ this is not json");

            var memory = NewMemory();
            memory.Load();

            Assert.Equal(0, memory.Count);
        }

        [Fact]
        public void MissingFile_Load_IsNotAnError()
        {
            var memory = NewMemory();
            memory.Load();
            Assert.Equal(0, memory.Count);
        }
    }

    public class GlossaryTests
    {
        [Fact]
        public void Add_Update_Remove()
        {
            var glossary = new Glossary(null);
            glossary.AddOrUpdate(new GlossaryTerm { Term = "Stark Industries", Translation = "صناعات ستارك" });
            Assert.Equal(1, glossary.Count);

            glossary.AddOrUpdate(new GlossaryTerm { Term = "stark industries", Translation = "مصانع ستارك" });
            Assert.Equal(1, glossary.Count); // updated, not duplicated

            Assert.True(glossary.Remove("STARK INDUSTRIES"));
            Assert.Equal(0, glossary.Count);
        }

        [Fact]
        public void FindMatches_IsCaseAndDiacriticInsensitive()
        {
            var glossary = new Glossary(null);
            glossary.AddOrUpdate(new GlossaryTerm { Term = "تقرير", Translation = "report" });

            Assert.Single(glossary.FindMatches("أرسلت التقرير هذا الصباح"));
            Assert.Single(glossary.FindMatches("أرسلت التَقرِير هذا الصباح"));
            Assert.Empty(glossary.FindMatches("لا يوجد شيء هنا"));
        }

        [Fact]
        public void DisabledTerms_AreNotMatched()
        {
            var glossary = new Glossary(null);
            var term = new GlossaryTerm { Term = "quantum", Translation = "كمّي" };
            glossary.AddOrUpdate(term);

            Assert.Single(glossary.FindMatches("The quantum core"));
            term.Enabled = false;
            Assert.Empty(glossary.FindMatches("The quantum core"));
        }

        [Fact]
        public void ToPromptText_UsesTermEqualsTranslationFormat()
        {
            var glossary = new Glossary(null);
            glossary.AddOrUpdate(new GlossaryTerm { Term = "Stark Industries", Translation = "صناعات ستارك" });
            glossary.AddOrUpdate(new GlossaryTerm { Term = "flux capacitor", Translation = "مكثف التدفق", Enabled = false });

            var text = glossary.ToPromptText();
            Assert.Equal("Stark Industries = صناعات ستارك", text);
        }

        [Fact]
        public void SaveAndLoad_RoundTrip()
        {
            var path = Path.Combine(Path.GetTempPath(), "se-glossary-" + Guid.NewGuid().ToString("N") + ".glossary.json");
            try
            {
                var glossary = new Glossary(path);
                glossary.AddOrUpdate(new GlossaryTerm { Term = "quantum flux capacitor", Translation = "مكثف التدفق الكمي", Comment = "technical" });
                glossary.Save();

                var reloaded = new Glossary(path);
                reloaded.Load();
                var terms = reloaded.GetAllTerms();
                Assert.Single(terms);
                Assert.Equal("مكثف التدفق الكمي", terms[0].Translation);
                Assert.Equal("technical", terms[0].Comment);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [Fact]
        public void GetDefaultFilePath_SitsNextToTheMedia()
        {
            var path = Glossary.GetDefaultFilePath(@"/movies/movie.srt");
            Assert.EndsWith("movie.glossary.json", path, StringComparison.Ordinal);
        }
    }
}
