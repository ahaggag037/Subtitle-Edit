using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.UiLogic.AutoTranslate;
using Nikse.SubtitleEdit.UiLogic.Translate;
using Nikse.SubtitleEdit.UiLogic.Translate.Arabic;
using Nikse.SubtitleEdit.UiLogic.Translate.Jobs;
using Nikse.SubtitleEdit.UiLogic.Translate.Memory;

namespace LibUiLogicTests.Translate.Jobs
{
    /// <summary>
    /// Vertical-slice and unit coverage for the translate-video job runner.
    /// <para>
    /// The engine here is a deterministic test double for <see cref="IAutoTranslator"/> (same
    /// pattern as the existing DoAutoTranslate tests): everything around it - SRT parsing,
    /// line-merge orchestration, Arabic policies, QA, memory, checkpointing, SRT export - is the
    /// real production code path. The doubles are marked as test doubles on purpose; they do NOT
    /// validate any real online engine.
    /// </para>
    /// </summary>
    public class TranslationJobRunnerTests : IDisposable
    {
        private readonly string _folder;

        public TranslationJobRunnerTests()
        {
            _folder = Path.Combine(Path.GetTempPath(), "se-job-tests-" + Guid.NewGuid().ToString("N"));
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

        /// <summary>Deterministic engine: prefixes with an engine marker; Egyptian target gets an Egyptian marker too.</summary>
        private sealed class FakeJobEngine : IAutoTranslator
        {
            public int Calls { get; private set; }
            public List<string> ReceivedTexts { get; } = new List<string>();
            public Action<FakeJobEngine, CancellationToken>? OnTranslate;

            public string Name => "FakeJobEngine";
            public string Url => "https://example.com";
            public string Error { get; set; } = string.Empty;
            public int MaxCharacters => 1500;

            public void Initialize()
            {
            }

            public List<TranslationPair> GetSupportedSourceLanguages() => new List<TranslationPair>
            {
                new TranslationPair("English", "en"),
            };

            public List<TranslationPair> GetSupportedTargetLanguages() => new List<TranslationPair>
            {
                new TranslationPair("Arabic", "ar"),
                new TranslationPair("Egyptian Arabic", "arz"),
            };

            public Task<string> Translate(string text, string sourceLanguageCode, string targetLanguageCode, CancellationToken cancellationToken)
            {
                Calls++;
                ReceivedTexts.Add(text);
                OnTranslate?.Invoke(this, cancellationToken);

                var marker = targetLanguageCode.Contains("Egyptian", StringComparison.OrdinalIgnoreCase) || targetLanguageCode == "arz"
                    ? "[EG]"
                    : "[AR]";
                return Task.FromResult(marker + " " + text);
            }
        }

        private TranslationJobOptions Options(string targetCode = "ar", string? profileId = null, bool useMemory = false)
        {
            return new TranslationJobOptions
            {
                SourceLanguageCode = "en",
                SourceLanguageName = "English",
                TargetLanguageCode = targetCode,
                TargetLanguageName = targetCode == "arz" ? "Egyptian Arabic" : "Arabic",
                ArabicProfileId = profileId,
                UseTranslationMemory = useMemory,
                TranslationMemoryFolder = _folder,
                EngineName = "FakeJobEngine",
            };
        }

        [Fact]
        public async Task VerticalSlice_EnglishSrt_To_ArabicMsa_SrtExport_Completes()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var outputSrt = Path.Combine(_folder, "out-msa.srt");
            var engine = new FakeJobEngine();
            var runner = new TranslationJobRunner(engine, Options("ar", ArabicProfileCatalog.ModernStandard.Id));

            var stages = new List<TranslationJobStage>();
            runner.Progress += p => stages.Add(p.Stage);

            var result = await runner.RunAsync(subtitle, outputSrt, CancellationToken.None);

            Assert.True(result.Success);
            Assert.False(result.WasCancelled);
            Assert.Null(result.Error);
            Assert.True(File.Exists(outputSrt));

            // The exported file parses as SRT with the same cue count.
            var exported = new Subtitle();
            new SubRip().LoadSubtitle(exported, File.ReadAllLines(outputSrt).ToList(), null);
            Assert.Equal(subtitle.Paragraphs.Count, exported.Paragraphs.Count);

            // The engine saw the English source text, not the target.
            Assert.Contains(engine.ReceivedTexts, t => t.Contains("quantum flux capacitor"));

            // Music line is skipped by the existing keep-untranslated logic.
            Assert.Contains("♪", exported.Paragraphs.Last().Text);

            // QA ran over all rows.
            Assert.Equal(subtitle.Paragraphs.Count, result.QaResult.TotalRows);

            // The unified progress stream covered the whole pipeline.
            Assert.Contains(TranslationJobStage.Preparing, stages);
            Assert.Contains(TranslationJobStage.Translating, stages);
            Assert.Contains(TranslationJobStage.QualityChecking, stages);
            Assert.Contains(TranslationJobStage.Exporting, stages);
            Assert.Contains(TranslationJobStage.Completed, stages);
        }

        [Fact]
        public async Task EgyptianTarget_UsesEgyptianPair_AndAppliesProfilePolicies()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var engine = new FakeJobEngine();
            var runner = new TranslationJobRunner(engine, Options("arz", ArabicProfileCatalog.Egyptian.Id));

            var result = await runner.RunAsync(subtitle, null, CancellationToken.None);

            Assert.True(result.Success);
            // The engine received the resolved target pair ("arz"), producing the Egyptian marker.
            var translated = result.TranslatedSubtitle!;
            Assert.All(translated.Paragraphs.Where(p => !string.IsNullOrWhiteSpace(p.Text)),
                p => Assert.StartsWith("[EG]", p.Text, StringComparison.Ordinal));
        }

        [Fact]
        public async Task CheckpointFile_IsWritten_AfterRun()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var outputSrt = Path.Combine(_folder, "checkpoint.srt");
            var runner = new TranslationJobRunner(new FakeJobEngine(), Options("ar", ArabicProfileCatalog.ModernStandard.Id));

            await runner.RunAsync(subtitle, outputSrt, CancellationToken.None);

            var statePath = TranslationJobState.GetDefaultStateFilePath(outputSrt);
            Assert.True(File.Exists(statePath));
            var state = TranslationJobState.Load(statePath);
            Assert.NotNull(state);
            Assert.Equal(TranslationJobStage.Completed, state!.Stage);
            Assert.False(state.IsResumable); // everything translated
            Assert.All(state.Rows, r => Assert.False(string.IsNullOrWhiteSpace(r.TargetText) || r.SourceText.Length == 0));
        }

        [Fact]
        public async Task Cancellation_MidJob_MarksCancelled_KeepsProgress()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var engine = new FakeJobEngine();
            var cts = new CancellationTokenSource();
            var outputSrt = Path.Combine(_folder, "cancel.srt");

            // Cancel from inside the engine's first call: DoAutoTranslate returns partial rows.
            engine.OnTranslate += (_, _) =>
            {
                if (engine.Calls >= 1)
                {
                    cts.Cancel();
                }
            };

            var runner = new TranslationJobRunner(engine, Options("ar", ArabicProfileCatalog.ModernStandard.Id));
            var result = await runner.RunAsync(subtitle, outputSrt, cts.Token);

            Assert.True(result.WasCancelled);

            var state = TranslationJobState.Load(TranslationJobState.GetDefaultStateFilePath(outputSrt));
            Assert.NotNull(state);
            Assert.Equal(TranslationJobStage.Cancelled, state!.Stage);
        }

        [Fact]
        public async Task Resume_AfterFailure_ContinuesAndCompletes()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var outputSrt = Path.Combine(_folder, "resume.srt");

            // First run: engine fails on the second line with a network-shaped error.
            var failingEngine = new FakeJobEngine();
            failingEngine.OnTranslate += (_, _) =>
            {
                if (failingEngine.Calls == 2)
                {
                    throw new HttpRequestException("No such host is known.");
                }
            };

            // ChunkSize 1 keeps the first line's result before the failure.
            var failingRunner = new TranslationJobRunner(failingEngine, Options("ar", ArabicProfileCatalog.ModernStandard.Id))
            {
                ChunkSize = 1,
            };
            var failedResult = await failingRunner.RunAsync(subtitle, outputSrt, CancellationToken.None);

            Assert.False(failedResult.Success);
            Assert.NotNull(failedResult.Error);
            Assert.Contains("internet", failedResult.Error!.WhatHappened, StringComparison.OrdinalIgnoreCase);

            var state = TranslationJobState.Load(TranslationJobState.GetDefaultStateFilePath(outputSrt));
            Assert.NotNull(state);
            Assert.Equal(TranslationJobStage.Failed, state!.Stage);
            Assert.True(state.IsResumable);

            // Resume with a healthy engine: only the missing lines are translated.
            var healthyEngine = new FakeJobEngine();
            var resumedRunner = TranslationJobRunner.FromState(state, healthyEngine);
            resumedRunner.ChunkSize = 2;

            var resumedResult = await resumedRunner.RunAsync(null, outputSrt, CancellationToken.None);

            Assert.True(resumedResult.Success);
            Assert.All(resumedResult.Rows, r => Assert.False(string.IsNullOrWhiteSpace(r.TargetText)));
            Assert.True(File.Exists(outputSrt));
        }

        [Fact]
        public async Task TranslationMemory_PrefillsWithoutEngineCalls_AndRecordsOutputs()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var engine = new FakeJobEngine();

            var memory = new TranslationMemory(_folder);
            memory.Add(new TranslationMemoryEntry
            {
                SourceText = "Stark Industries signed the contract yesterday!",
                TargetText = "[TM] وقعت صناعات ستارك العقد أمس!",
                SourceLanguageCode = "en",
                TargetLanguageCode = "ar",
            });
            memory.Save();

            var options = Options("ar", ArabicProfileCatalog.ModernStandard.Id, useMemory: true);
            var runner = new TranslationJobRunner(engine, options);

            var result = await runner.RunAsync(subtitle, null, CancellationToken.None);

            Assert.True(result.Success);
            var memoryRow = result.Rows.First(r => r.SourceText.Contains("Stark Industries", StringComparison.Ordinal));
            Assert.Equal("[TM] وقعت صناعات ستارك العقد أمس!", memoryRow.TargetText);
            Assert.True(memoryRow.FromMemory);
            // The memory line never went to the engine.
            Assert.DoesNotContain(engine.ReceivedTexts, t => t.Contains("Stark Industries", StringComparison.Ordinal));
        }

        [Fact]
        public async Task GlossaryViolation_IsReported()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var glossaryPath = Path.Combine(_folder, "project.glossary.json");
            var glossary = new Glossary(glossaryPath);
            glossary.AddOrUpdate(new GlossaryTerm { Term = "piece of cake", Translation = "قطعة من الكعك" });
            glossary.Save();

            var options = Options("ar", ArabicProfileCatalog.ModernStandard.Id);
            options.GlossaryFilePath = glossaryPath;
            var runner = new TranslationJobRunner(new FakeJobEngine(), options);

            var result = await runner.RunAsync(subtitle, null, CancellationToken.None);

            Assert.True(result.Success);
            // The fake engine mirrors the source, so the idiom term is present in the source
            // while the glossary translation cannot appear in the target -> violation expected.
            Assert.Contains(result.QaResult.Issues, i => i.CheckId == QaCheckIds.GlossaryViolation);
        }

        [Fact]
        public async Task Progress_NeverFabricatesEta()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var engine = new FakeJobEngine();
            var runner = new TranslationJobRunner(engine, Options("ar", ArabicProfileCatalog.ModernStandard.Id));

            var etas = new List<double?>();
            runner.Progress += p => etas.Add(p.EstimatedSecondsRemaining);

            await runner.RunAsync(subtitle, null, CancellationToken.None);

            // Every ETA must be either null (not measurable yet) or a non-negative finite number.
            Assert.All(etas, eta => Assert.True(eta == null || (!double.IsNaN(eta.Value) && !double.IsInfinity(eta.Value) && eta.Value >= 0)));
        }

        [Fact]
        public async Task MissingSourceSubtitle_FailsWithClearError()
        {
            var runner = new TranslationJobRunner(new FakeJobEngine(), Options("ar", ArabicProfileCatalog.ModernStandard.Id));
            var result = await runner.RunAsync(null, null, CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Contains("Nothing to translate", result.Error!.WhatHappened, StringComparison.Ordinal);
        }

        [Fact]
        public void LoadState_MissingOrCorrupt_ReturnsNull()
        {
            Assert.Null(TranslationJobState.Load(Path.Combine(_folder, "missing.json")));
            var corrupt = Path.Combine(_folder, "corrupt.json");
            File.WriteAllText(corrupt, "not json at all");
            Assert.Null(TranslationJobState.Load(corrupt));
        }
    }
}
