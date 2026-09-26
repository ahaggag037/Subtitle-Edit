using System.Reflection;
using System.Text;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Settings;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.UiLogic.AutoTranslate;
using Nikse.SubtitleEdit.UiLogic.Translate;
using Nikse.SubtitleEdit.UiLogic.Translate.Arabic;
using Nikse.SubtitleEdit.UiLogic.Translate.Jobs;
using Nikse.SubtitleEdit.UiLogic.Translate.Memory;
using Nikse.SubtitleEdit.UiLogic.Translate.Qa;

namespace LibUiLogicTests.Translate.Jobs
{
    /// <summary>
    /// Deterministic failure-injection coverage for the translate-video job runner: every case
    /// drives the REAL orchestration (SRT parse, chunking, merge orchestration, Arabic policies,
    /// QA, TM/glossary, checkpoints, export) with a scripted <see cref="IAutoTranslator"/> double.
    /// These tests assert honest behavior under failure - no silent corruption, actionable
    /// errors, coherent checkpoints - not engine behavior.
    /// </summary>
    public class TranslationJobFailureInjectionTests : IDisposable
    {
        private readonly string _folder;

        public TranslationJobFailureInjectionTests()
        {
            _folder = Path.Combine(Path.GetTempPath(), "se-job-failure-tests-" + Guid.NewGuid().ToString("N"));
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

        /// <summary>Scriptable engine double: counts calls, records request texts, optional per-call delay/exception/empty override.</summary>
        private sealed class ScriptedEngine : IAutoTranslator
        {
            public int Calls { get; private set; }
            public List<string> ReceivedTexts { get; } = new List<string>();
            public Func<ScriptedEngine, int, string?>? Script { get; set; }
            public int DelayMilliseconds { get; set; }

            public string Name => "ScriptedEngine";
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

            public async Task<string> Translate(string text, string sourceLanguageCode, string targetLanguageCode, CancellationToken cancellationToken)
            {
                Calls++;
                ReceivedTexts.Add(text);
                if (DelayMilliseconds > 0)
                {
                    await Task.Delay(DelayMilliseconds, cancellationToken);
                }

                var scripted = Script?.Invoke(this, Calls);
                if (scripted != null)
                {
                    return scripted;
                }

                return "[AR] " + text;
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
                EngineName = "ScriptedEngine",
            };
        }

        // ------------------------------------------------------------------
        // B. translator exception (generic, not network-shaped)
        // ------------------------------------------------------------------

        [Fact]
        public async Task TranslatorException_MarksFailed_ActionableError_AndKeepsCheckpointResumable()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var outputSrt = Path.Combine(_folder, "exception.srt");

            var engine = new ScriptedEngine();
            engine.Script += (_, call) =>
            {
                if (call == 1)
                {
                    throw new InvalidOperationException("Simulated engine crash.");
                }

                return null;
            };

            var runner = new TranslationJobRunner(engine, Options("ar", ArabicProfileCatalog.ModernStandard.Id))
            {
                ChunkSize = 1,
            };

            var result = await runner.RunAsync(subtitle, outputSrt, CancellationToken.None);

            // User-facing error is actionable: WHAT / WHY / WHAT-TO-DO all present.
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.False(string.IsNullOrWhiteSpace(result.Error!.WhatHappened));
            Assert.False(string.IsNullOrWhiteSpace(result.Error.Why));
            Assert.False(string.IsNullOrWhiteSpace(result.Error.WhatToDo));
            Assert.Contains("Simulated engine crash.", result.Error.TechnicalDetails ?? string.Empty, StringComparison.Ordinal);

            // Checkpoint remains on disk and coherent: rows preserved, stage terminal-failed,
            // and - because untranslated rows remain - it is still resumable.
            var state = TranslationJobState.Load(TranslationJobState.GetDefaultStateFilePath(outputSrt));
            Assert.NotNull(state);
            Assert.Equal(TranslationJobStage.Failed, state!.Stage);
            Assert.Equal(subtitle.Paragraphs.Count, state.Rows.Count);
            Assert.True(state.IsResumable);
            Assert.False(File.Exists(outputSrt)); // no export was produced
        }

        // ------------------------------------------------------------------
        // C. malformed / missing / legacy checkpoint
        // ------------------------------------------------------------------

        [Fact]
        public void StateLoad_MissingFile_ReturnsNull()
        {
            Assert.Null(TranslationJobState.Load(Path.Combine(_folder, "does-not-exist.job.json")));
        }

        [Fact]
        public void StateLoad_TruncatedJson_ReturnsNull()
        {
            var path = Path.Combine(_folder, "truncated.job.json");
            var state = new TranslationJobState();
            state.Rows.Add(new TranslationJobRow { Number = 1, StartMilliseconds = 1000, EndMilliseconds = 2000, SourceText = "Hello" });
            File.WriteAllText(path, state.ToJson().Substring(0, state.ToJson().Length - 15), new UTF8Encoding(false));

            Assert.Null(TranslationJobState.Load(path));
        }

        [Fact]
        public void StateLoad_WrongVersion_ReturnsNull_AndIsNotResumable()
        {
            var path = Path.Combine(_folder, "wrong-version.job.json");
            var state = new TranslationJobState();
            state.Rows.Add(new TranslationJobRow { Number = 1, SourceText = "Hello" });
            var json = state.ToJson().Replace("\"Version\": 1", "\"Version\": 999", StringComparison.Ordinal);
            File.WriteAllText(path, json, new UTF8Encoding(false));

            var loaded = TranslationJobState.Load(path);
            Assert.Null(loaded);

            // Defense in depth: even a hand-constructed wrong-version state is not resumable.
            var direct = new TranslationJobState { Version = 999 };
            direct.Rows.Add(new TranslationJobRow { Number = 1, SourceText = "Hello" });
            Assert.False(direct.IsResumable);
        }

        [Fact]
        public void StateSaveLoad_RoundTrip_PreservesIdentityAndRows()
        {
            var path = Path.Combine(_folder, "roundtrip.job.json");
            var options = Options("arz", ArabicProfileCatalog.Egyptian.Id);
            options.EngineName = "SomeEngine";
            var state = new TranslationJobState
            {
                Options = options,
                Stage = TranslationJobStage.Translating,
                OutputSrtPath = "/tmp/out.egyptian-arabic.srt",
                InputSubtitlePath = "/tmp/in.srt",
                SourceSignature = "abc123",
            };
            state.Rows.Add(new TranslationJobRow { Number = 1, StartMilliseconds = 1000, EndMilliseconds = 2500, SourceText = "One", TargetText = "[AR] One" });
            state.Rows.Add(new TranslationJobRow { Number = 2, StartMilliseconds = 3000, EndMilliseconds = 4500, SourceText = "Two" });
            state.StateFilePath = path;
            state.Save();

            var loaded = TranslationJobState.Load(path);
            Assert.NotNull(loaded);
            Assert.Equal(TranslationJobStage.Translating, loaded!.Stage);
            Assert.Equal("/tmp/out.egyptian-arabic.srt", loaded.OutputSrtPath);
            Assert.Equal("/tmp/in.srt", loaded.InputSubtitlePath);
            Assert.Equal("abc123", loaded.SourceSignature);
            Assert.Equal("arz", loaded.Options.TargetLanguageCode);
            Assert.Equal(ArabicProfileCatalog.Egyptian.Id, loaded.Options.ArabicProfileId);
            Assert.Equal(2, loaded.Rows.Count);
            Assert.Equal("[AR] One", loaded.Rows[0].TargetText);
            Assert.True(loaded.IsResumable);
        }

        // ------------------------------------------------------------------
        // D. resume after partial progress: no duplicate work, no duplicate output
        // ------------------------------------------------------------------

        [Fact]
        public async Task Resume_DoesNotRetranslateCompletedLines_AndOutputHasNoDuplicates()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var outputSrt = Path.Combine(_folder, "no-dup.srt");

            var failingEngine = new ScriptedEngine();
            failingEngine.Script += (_, call) =>
            {
                if (call == 2)
                {
                    throw new HttpRequestException("No such host is known.");
                }

                return null;
            };

            var failingRunner = new TranslationJobRunner(failingEngine, Options("ar", ArabicProfileCatalog.ModernStandard.Id))
            {
                ChunkSize = 1,
            };
            var failedResult = await failingRunner.RunAsync(subtitle, outputSrt, CancellationToken.None);
            Assert.False(failedResult.Success);

            var state = TranslationJobState.Load(TranslationJobState.GetDefaultStateFilePath(outputSrt));
            Assert.NotNull(state);
            Assert.True(state!.IsResumable);
            var firstLineAlreadyTranslated = state.Rows[0].TargetText;

            var healthyEngine = new ScriptedEngine();
            var resumedRunner = TranslationJobRunner.FromState(state, healthyEngine);
            resumedRunner.ChunkSize = 2;

            var resumed = await resumedRunner.RunAsync(null, outputSrt, CancellationToken.None);

            Assert.True(resumed.Success);
            Assert.Equal(subtitle.Paragraphs.Count, resumed.Rows.Count);

            // Numbering preserved exactly once per cue.
            Assert.Equal(resumed.Rows.Select(r => r.Number).OrderBy(n => n).ToList(),
                         resumed.Rows.Select(r => r.Number).Distinct().OrderBy(n => n).ToList());

            // The line translated before the failure is never sent to the engine again...
            Assert.DoesNotContain(healthyEngine.ReceivedTexts,
                t => t.Contains("Nora, did you finish the report", StringComparison.Ordinal));
            // ...and its translation is exactly the one from the first run (no double marker).
            Assert.Equal(firstLineAlreadyTranslated, resumed.Rows[0].TargetText);
            Assert.StartsWith("[AR] ", resumed.Rows[0].TargetText, StringComparison.Ordinal);
            Assert.DoesNotContain("[AR] [AR]", resumed.Rows[0].TargetText, StringComparison.Ordinal);
        }

        // ------------------------------------------------------------------
        // E. empty/missing translation-memory directory
        // ------------------------------------------------------------------

        [Fact]
        public void TranslationMemory_NonexistentDirectory_LoadsEmpty_AndSaveCreatesDirectory()
        {
            var deepFolder = Path.Combine(_folder, "deep", "nested");
            var memory = new TranslationMemory(deepFolder);
            memory.Load();

            // Empty store: lookup misses, no exception.
            Assert.Null(memory.Lookup("Hello world", "en", "ar", null));

            memory.Add(new TranslationMemoryEntry
            {
                SourceText = "Hello world",
                TargetText = "[AR] Hello world",
                SourceLanguageCode = "en",
                TargetLanguageCode = "ar",
            });
            memory.Save();

            Assert.True(File.Exists(Path.Combine(deepFolder, "translation_memory.json")));

            var reloaded = new TranslationMemory(deepFolder);
            reloaded.Load();
            Assert.Equal("[AR] Hello world", reloaded.Lookup("hello WORLD", "en", "ar", null));
        }

        // ------------------------------------------------------------------
        // F. null source during resume (checkpoint-only continuation)
        // ------------------------------------------------------------------

        [Fact]
        public async Task Resume_WithNullSource_CompletesFromCheckpointRows()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var outputSrt = Path.Combine(_folder, "null-source.srt");

            var failingEngine = new ScriptedEngine();
            failingEngine.Script += (_, call) =>
            {
                if (call == 2)
                {
                    throw new HttpRequestException("Connection refused.");
                }

                return null;
            };

            var failingRunner = new TranslationJobRunner(failingEngine, Options("ar", ArabicProfileCatalog.ModernStandard.Id))
            {
                ChunkSize = 1,
            };
            await failingRunner.RunAsync(subtitle, outputSrt, CancellationToken.None);

            var state = TranslationJobState.Load(TranslationJobState.GetDefaultStateFilePath(outputSrt));
            Assert.NotNull(state);

            // No source subtitle object at all: everything needed lives in the checkpoint.
            var healthy = new ScriptedEngine();
            var resumed = TranslationJobRunner.FromState(state!, healthy);
            var result = await resumed.RunAsync(null, outputSrt, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(subtitle.Paragraphs.Count, result.Rows.Count);
            Assert.All(result.Rows, r => Assert.False(string.IsNullOrWhiteSpace(r.TargetText)));
            Assert.True(File.Exists(outputSrt));
        }

        // ------------------------------------------------------------------
        // G. invalid/empty translated text: never reported as silently complete
        // ------------------------------------------------------------------

        [Fact]
        public async Task EngineReturnsOnlyWhitespace_LineFailsActionably_NotSilentCompletion()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var outputSrt = Path.Combine(_folder, "empty-reply.srt");

            // The engine "answers" every request with whitespace: the orchestration must treat
            // this as no progress (bounded retries), then fail the job with an actionable
            // error - never report success while the target lines are still blank.
            var engine = new ScriptedEngine { Script = (_, _) => "   " };

            var runner = new TranslationJobRunner(engine, Options("ar", ArabicProfileCatalog.ModernStandard.Id))
            {
                ChunkSize = 7,
            };

            var result = await runner.RunAsync(subtitle, outputSrt, CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Contains("no translation", result.Error!.WhatHappened + " " + result.Error.Why, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(result.Error.WhatToDo));

            var state = TranslationJobState.Load(TranslationJobState.GetDefaultStateFilePath(outputSrt));
            Assert.NotNull(state);
            Assert.Equal(TranslationJobStage.Failed, state!.Stage);
            Assert.True(state.IsResumable); // nothing was falsely marked complete
            Assert.False(File.Exists(outputSrt));
        }

        // ------------------------------------------------------------------
        // H. glossary hit (compliance) at runner level
        // ------------------------------------------------------------------

        [Fact]
        public async Task GlossaryCompliance_PreferredTranslationUsed_NoViolation()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var glossaryPath = Path.Combine(_folder, "compliant.glossary.json");
            var glossary = new Glossary(glossaryPath);
            glossary.AddOrUpdate(new GlossaryTerm { Term = "quantum flux capacitor", Translation = "المكثف الكمي" });
            glossary.Save();

            var engine = new ScriptedEngine();
            engine.Script += (_, _) => "[AR] المكثف الكمي";

            var options = Options("ar", ArabicProfileCatalog.ModernStandard.Id);
            options.GlossaryFilePath = glossaryPath;
            var runner = new TranslationJobRunner(engine, options);

            var result = await runner.RunAsync(subtitle, null, CancellationToken.None);

            Assert.True(result.Success);
            // Every line got the preferred term -> no glossary violation anywhere.
            Assert.DoesNotContain(result.QaResult.Issues, i => i.CheckId == QaCheckIds.GlossaryViolation);
        }

        // ------------------------------------------------------------------
        // §11 checkpoint identity: never combine state from incompatible jobs
        // ------------------------------------------------------------------

        [Fact]
        public void SourceSignature_IsStable_ForEqualContent_AndDistinctForChanges()
        {
            var a = new Subtitle();
            a.Paragraphs.Add(new Paragraph("Hello", 1000, 2000) { Number = 1 });
            var b = new Subtitle();
            b.Paragraphs.Add(new Paragraph("Hello", 1000, 2000) { Number = 1 });

            Assert.Equal(TranslationJobRunner.ComputeSourceSignature(a), TranslationJobRunner.ComputeSourceSignature(b));

            var changedText = new Subtitle();
            changedText.Paragraphs.Add(new Paragraph("Hello!", 1000, 2000) { Number = 1 });
            Assert.NotEqual(TranslationJobRunner.ComputeSourceSignature(a), TranslationJobRunner.ComputeSourceSignature(changedText));

            var changedTiming = new Subtitle();
            changedTiming.Paragraphs.Add(new Paragraph("Hello", 1000, 2500) { Number = 1 });
            Assert.NotEqual(TranslationJobRunner.ComputeSourceSignature(a), TranslationJobRunner.ComputeSourceSignature(changedTiming));

            var changedNumber = new Subtitle();
            changedNumber.Paragraphs.Add(new Paragraph("Hello", 1000, 2000) { Number = 2 });
            Assert.NotEqual(TranslationJobRunner.ComputeSourceSignature(a), TranslationJobRunner.ComputeSourceSignature(changedNumber));
        }

        [Fact]
        public async Task Resume_WithDifferentSource_IsRefused_AndCheckpointStaysCoherent()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var outputSrt = Path.Combine(_folder, "identity.srt");

            var failingEngine = new ScriptedEngine();
            failingEngine.Script += (_, call) =>
            {
                if (call == 2)
                {
                    throw new HttpRequestException("No such host is known.");
                }

                return null;
            };

            var failingRunner = new TranslationJobRunner(failingEngine, Options("ar", ArabicProfileCatalog.ModernStandard.Id))
            {
                ChunkSize = 1,
            };
            await failingRunner.RunAsync(subtitle, outputSrt, CancellationToken.None);

            var stateBefore = TranslationJobState.Load(TranslationJobState.GetDefaultStateFilePath(outputSrt));
            Assert.NotNull(stateBefore);
            var rowsBefore = stateBefore!.Rows.Count;
            var stageBefore = stateBefore.Stage;

            // A DIFFERENT subtitle: resuming must be refused with an actionable error.
            var different = new Subtitle();
            different.Paragraphs.Add(new Paragraph("A completely other transcript.", 500, 1500) { Number = 1 });

            var healthy = new ScriptedEngine();
            var resumed = TranslationJobRunner.FromState(stateBefore, healthy);
            var result = await resumed.RunAsync(different, outputSrt, CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Contains("different subtitle", result.Error!.WhatHappened, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, healthy.Calls); // nothing was translated from the wrong input

            // The on-disk checkpoint was not touched: still loadable, same stage, same rows.
            var stateAfter = TranslationJobState.Load(TranslationJobState.GetDefaultStateFilePath(outputSrt));
            Assert.NotNull(stateAfter);
            Assert.Equal(stageBefore, stateAfter!.Stage);
            Assert.Equal(rowsBefore, stateAfter.Rows.Count);
            Assert.True(stateAfter.IsResumable);
        }

        [Fact]
        public async Task Resume_WithSameSourceContent_IsAccepted()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var outputSrt = Path.Combine(_folder, "identity-same.srt");

            var failingEngine = new ScriptedEngine();
            failingEngine.Script += (_, call) =>
            {
                if (call == 2)
                {
                    throw new HttpRequestException("No such host is known.");
                }

                return null;
            };

            var failingRunner = new TranslationJobRunner(failingEngine, Options("ar", ArabicProfileCatalog.ModernStandard.Id))
            {
                ChunkSize = 1,
            };
            await failingRunner.RunAsync(subtitle, outputSrt, CancellationToken.None);

            var state = TranslationJobState.Load(TranslationJobState.GetDefaultStateFilePath(outputSrt));
            Assert.NotNull(state);

            var healthy = new ScriptedEngine();
            var resumed = TranslationJobRunner.FromState(state!, healthy);
            var result = await resumed.RunAsync(subtitle, outputSrt, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(subtitle.Paragraphs.Count, result.Rows.Count);
        }

        // ------------------------------------------------------------------
        // §12 honest ETA: deterministic guard checks
        // ------------------------------------------------------------------

        [Fact]
        public async Task Eta_NullBeforeRealProgress_FiniteAfter_AndNeverNegativeOrInfinite()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var engine = new ScriptedEngine { DelayMilliseconds = 650 };

            var runner = new TranslationJobRunner(engine, Options("ar", ArabicProfileCatalog.ModernStandard.Id))
            {
                ChunkSize = 4,
            };

            var events = new List<TranslationJobProgress>();
            runner.Progress += events.Add;

            await runner.RunAsync(subtitle, null, CancellationToken.None);

            Assert.NotEmpty(events);

            // Invariants on every event: ETA is null or a finite, non-negative number.
            Assert.All(events, p =>
            {
                if (p.EstimatedSecondsRemaining.HasValue)
                {
                    Assert.True(double.IsFinite(p.EstimatedSecondsRemaining.Value));
                    Assert.True(p.EstimatedSecondsRemaining.Value >= 0);
                }
            });

            // The first Translating event happens immediately (engine has not answered yet):
            // no honest rate exists, so ETA must be null there.
            var firstTranslating = events.First(p => p.Stage == TranslationJobStage.Translating);
            Assert.Null(firstTranslating.EstimatedSecondsRemaining);

            // After the engine actually spent >= 0.5 s translating, a finite estimate appears.
            Assert.Contains(events, p => p.Stage == TranslationJobStage.Translating && p.EstimatedSecondsRemaining.HasValue);
        }

        // ------------------------------------------------------------------
        // §8/§19 credential hygiene: secrets never reach the checkpoint file
        // ------------------------------------------------------------------

        [Fact]
        public async Task Checkpoint_NeverContainsCredentialValues()
        {
            var subtitle = TranslateFixtures.LoadEnglishSubtitle();
            var outputSrt = Path.Combine(_folder, "secrets.srt");

            // Plant marker secrets everywhere credential-like in the engine settings.
            var tools = Configuration.Settings.Tools;
            var marker = "SE-MARKER-SK-SECRET-9f3a";
            var credentialProps = typeof(ToolsSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string) &&
                            (p.Name.Contains("ApiKey", StringComparison.Ordinal) ||
                             p.Name.Contains("Token", StringComparison.Ordinal) ||
                             p.Name.Contains("Secret", StringComparison.Ordinal) ||
                             p.Name.Contains("ClientSecret", StringComparison.Ordinal)))
                .ToList();
            Assert.NotEmpty(credentialProps);
            foreach (var prop in credentialProps)
            {
                prop.SetValue(tools, marker);
            }

            try
            {
                var engine = new ScriptedEngine();
                var runner = new TranslationJobRunner(engine, Options("ar", ArabicProfileCatalog.ModernStandard.Id));
                var result = await runner.RunAsync(subtitle, outputSrt, CancellationToken.None);
                Assert.True(result.Success);

                var checkpointText = File.ReadAllText(TranslationJobState.GetDefaultStateFilePath(outputSrt));
                Assert.DoesNotContain(marker, checkpointText, StringComparison.Ordinal);

                // And no credential-shaped property names are serialized at all.
                Assert.DoesNotContain("ApiKey", checkpointText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("\"Token\"", checkpointText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Secret", checkpointText, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                foreach (var prop in credentialProps)
                {
                    prop.SetValue(tools, string.Empty);
                }
            }
        }
    }
}
