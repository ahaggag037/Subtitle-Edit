using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.UiLogic.AutoTranslate;
using Nikse.SubtitleEdit.UiLogic.Translate.Arabic;
using Nikse.SubtitleEdit.UiLogic.Translate.Memory;
using Nikse.SubtitleEdit.UiLogic.Translate.Qa;

namespace Nikse.SubtitleEdit.UiLogic.Translate.Jobs
{
    /// <summary>The outcome of one runner execution.</summary>
    public sealed class TranslationJobResult
    {
        public bool Success { get; internal set; }
        public bool WasCancelled { get; internal set; }
        public Subtitle? TranslatedSubtitle { get; internal set; }
        public List<TranslationJobRow> Rows { get; internal set; } = new List<TranslationJobRow>();
        public SubtitleQaResult QaResult { get; internal set; } = new SubtitleQaResult();
        public List<string> SafeFixesApplied { get; internal set; } = new List<string>();
        public string? OutputSrtPath { get; internal set; }
        public JobErrorInfo? Error { get; internal set; }
    }

    /// <summary>
    /// Thin orchestration for the translate-video workflow:
    /// prepare → (translation memory pre-fill) → translate (via the existing
    /// <see cref="DoAutoTranslate"/> engine orchestration) → Arabic text policies →
    /// quality check → optional export → persistent checkpoint after every chunk.
    /// <para>
    /// The runner owns no translation logic of its own: engines are the existing
    /// <see cref="IAutoTranslator"/> implementations, batch/context engines included, and line
    /// merging stays in <see cref="MergeAndSplitHelper"/>. The one deliberate deviation from a
    /// single <see cref="DoAutoTranslate"/> call is chunking (see <see cref="ChunkSize"/>): each
    /// chunk's results are merged into the job rows and checkpointed, so an unexpected close
    /// costs one chunk, not the whole subtitle. Merge heuristics work inside a chunk; lines are
    /// rarely merged across a chunk boundary, which is an accepted, bounded trade-off.
    /// </para>
    /// </summary>
    public sealed class TranslationJobRunner
    {
        /// <summary>How many subtitle lines one translation chunk contains (checkpoint granularity).</summary>
        public int ChunkSize { get; set; } = 40;

        private readonly IAutoTranslator _translator;
        private readonly TranslationMemory? _memory;
        private readonly Glossary? _glossary;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private TranslationJobProgress? _lastProgress;

        public TranslationJobOptions Options { get; }

        /// <summary>The live job state - persisted after every chunk; the UI can also save it on demand.</summary>
        public TranslationJobState State { get; }

        /// <summary>Single progress stream for the whole pipeline. Raised on a worker thread.</summary>
        public event Action<TranslationJobProgress>? Progress;

        public TranslationJobRunner(IAutoTranslator translator, TranslationJobOptions options)
        {
            _translator = translator;
            Options = options;
            State = new TranslationJobState { Options = options, Stage = TranslationJobStage.Queued };

            if (options.UseTranslationMemory)
            {
                _memory = new TranslationMemory(options.TranslationMemoryFolder);
                _memory.Load();
            }

            if (!string.IsNullOrWhiteSpace(options.GlossaryFilePath))
            {
                _glossary = new Glossary(options.GlossaryFilePath);
                _glossary.Load();
            }
        }

        private TranslationJobRunner(IAutoTranslator translator, TranslationJobState state) : this(translator, state.Options)
        {
            State.Stage = state.Stage;
            State.InputSubtitlePath = state.InputSubtitlePath;
            State.OutputSrtPath = state.OutputSrtPath;
            State.Rows = state.Rows;
        }

        /// <summary>Builds a runner that continues a persisted job with the given engine.</summary>
        public static TranslationJobRunner FromState(TranslationJobState state, IAutoTranslator translator)
        {
            return new TranslationJobRunner(translator, state);
        }

        /// <summary>Builds the translation pair an engine actually expects: engines differ in whether Code carries a real code or an English name.</summary>
        private static TranslationPair? ResolvePair(List<TranslationPair> supported, string code, string englishName)
        {
            foreach (var pair in supported)
            {
                if (string.Equals(pair.TwoLetterIsoLanguageName, code, StringComparison.OrdinalIgnoreCase))
                {
                    return pair;
                }
            }

            foreach (var pair in supported)
            {
                if (string.Equals(pair.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    return pair;
                }
            }

            foreach (var pair in supported)
            {
                if (string.Equals(pair.Name, englishName, StringComparison.OrdinalIgnoreCase))
                {
                    return pair;
                }
            }

            return null;
        }

        private void Report(TranslationJobStage stage, double? percent, int done, int total)
        {
            double? eta = null;
            if (stage == TranslationJobStage.Translating && total > 0 && done > 0 && _stopwatch.IsRunning)
            {
                // Rate needs at least two samples *and* some real elapsed time; before that there
                // is no honest estimate, so EstimatedSecondsRemaining stays null.
                var elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
                if (elapsedSeconds >= 0.5 && _lastProgress != null && _lastProgress.Stage == TranslationJobStage.Translating && done > _lastProgress.ItemsDone)
                {
                    var rowsPerSecond = done / elapsedSeconds;
                    if (rowsPerSecond > 0.0001 && !double.IsInfinity(rowsPerSecond))
                    {
                        eta = (total - done) / rowsPerSecond;
                    }
                }
            }

            var progress = new TranslationJobProgress(stage, percent, done, total, _stopwatch.Elapsed, eta);
            _lastProgress = progress;
            Progress?.Invoke(progress);
        }

        private void SaveCheckpoint(TranslationJobStage stage)
        {
            State.Stage = stage;
            State.Rows = Rows;
            try
            {
                State.Save();
            }
            catch (Exception)
            {
                // A checkpoint write failure must never fail the translation itself.
            }
        }

        private List<TranslationJobRow> Rows => State.Rows;

        /// <summary>
        /// Runs the job on the given source subtitle. <paramref name="outputSrtPath"/> is optional;
        /// when set, an SRT file is written at the export stage and the checkpoint file lives next to it.
        /// </summary>
        public async Task<TranslationJobResult> RunAsync(Subtitle sourceSubtitle, string? outputSrtPath, CancellationToken cancellationToken)
        {
            var result = new TranslationJobResult();
            _stopwatch.Restart();

            try
            {
                // ---- prepare ----
                State.OutputSrtPath = outputSrtPath ?? State.OutputSrtPath;
                if (State.StateFilePath == null)
                {
                    State.StateFilePath = TranslationJobState.GetDefaultStateFilePath(State.OutputSrtPath ?? string.Empty);
                }

                if (Rows.Count == 0 && sourceSubtitle != null)
                {
                    foreach (var p in sourceSubtitle.Paragraphs)
                    {
                        Rows.Add(new TranslationJobRow
                        {
                            Number = p.Number,
                            StartMilliseconds = p.StartTime.TotalMilliseconds,
                            EndMilliseconds = p.EndTime.TotalMilliseconds,
                            SourceText = p.Text ?? string.Empty,
                        });
                    }

                    State.InputSubtitlePath = sourceSubtitle.FileName;
                    State.SourceSignature = ComputeSourceSignature(sourceSubtitle);
                }
                else if (sourceSubtitle != null && Rows.Count > 0)
                {
                    // Resume with an explicit source: refuse to mix this checkpoint with a
                    // different input file. Checked BEFORE any checkpoint write, so the saved
                    // state on disk stays coherent and resumable with the correct source.
                    var incomingSignature = ComputeSourceSignature(sourceSubtitle);
                    if (!string.IsNullOrEmpty(State.SourceSignature) && State.SourceSignature != incomingSignature)
                    {
                        result.Error = new JobErrorInfo("This checkpoint belongs to a different subtitle.",
                            "The saved progress was created from another subtitle/transcript than the one given now (content fingerprint mismatch).",
                            "Resume with the original subtitle file, or delete the checkpoint file ("
                            + (State.StateFilePath ?? "<output>.job.json") + ") to start this subtitle from scratch.",
                            null);
                        result.Rows = Rows;
                        return result;
                    }
                }

                Report(TranslationJobStage.Preparing, null, 0, Rows.Count);
                SaveCheckpoint(TranslationJobStage.Preparing);

                if (Rows.Count == 0)
                {
                    result.Error = new JobErrorInfo("Nothing to translate.",
                        "The job has no source subtitle or transcript.",
                        "Open or generate a subtitle/transcript for the video first, then start the job.",
                    null);
                    State.ErrorMessage = result.Error.WhatHappened;
                    SaveCheckpoint(TranslationJobStage.Failed);
                    result.Rows = Rows;
                    return result;
                }

                // ---- translation memory pre-fill ----
                var profile = ArabicProfileCatalog.GetById(Options.ArabicProfileId);
                var totalRows = Rows.Count;
                if (_memory != null)
                {
                    var styleId = profile?.Id;
                    for (var i = 0; i < Rows.Count; i++)
                    {
                        var row = Rows[i];
                        if (!string.IsNullOrWhiteSpace(row.TargetText))
                        {
                            continue;
                        }

                        var remembered = _memory.Lookup(row.SourceText, Options.SourceLanguageCode, Options.TargetLanguageCode, styleId);
                        if (remembered != null)
                        {
                            row.TargetText = remembered;
                            row.FromMemory = true;
                        }
                    }

                    var filled = Rows.Count(r => !string.IsNullOrWhiteSpace(r.TargetText));
                    Report(TranslationJobStage.Preparing, 100.0 * filled / Math.Max(1, totalRows), filled, totalRows);
                }

                // ---- translate (existing engine orchestration, chunked for checkpointing) ----
                cancellationToken.ThrowIfCancellationRequested();
                _translator.Initialize();

                var sourcePair = ResolvePair(_translator.GetSupportedSourceLanguages(), Options.SourceLanguageCode, Options.SourceLanguageName)
                                 ?? new TranslationPair(Options.SourceLanguageName, Options.SourceLanguageCode);
                var targetPair = ResolvePair(_translator.GetSupportedTargetLanguages(), Options.TargetLanguageCode, Options.TargetLanguageName)
                                 ?? new TranslationPair(Options.TargetLanguageName, Options.TargetLanguageCode);

                var pendingIndexes = new List<int>();
                for (var i = 0; i < Rows.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(Rows[i].TargetText))
                    {
                        pendingIndexes.Add(i);
                    }
                }

                var doneBeforeTranslate = Rows.Count - pendingIndexes.Count;
                Report(TranslationJobStage.Translating, totalRows > 0 ? 100.0 * doneBeforeTranslate / totalRows : null, doneBeforeTranslate, totalRows);

                var chunkStart = 0;
                while (chunkStart < pendingIndexes.Count && !cancellationToken.IsCancellationRequested)
                {
                    var count = Math.Min(ChunkSize, pendingIndexes.Count - chunkStart);
                    var chunkRows = new List<TranslationJobRow>(count);
                    for (var i = chunkStart; i < chunkStart + count; i++)
                    {
                        chunkRows.Add(Rows[pendingIndexes[i]]);
                    }

                    var chunkSubtitle = BuildSubtitleFromRows(chunkRows, useTargetText: false);
                    var doTranslate = new DoAutoTranslate
                    {
                        TranslateEachLineSeparately = false,
                        Progress = (linesDone, linesTotal) =>
                        {
                            var overallDone = doneBeforeTranslate + chunkStart + linesDone;
                            Report(TranslationJobStage.Translating,
                                totalRows > 0 ? 100.0 * overallDone / totalRows : null,
                                overallDone, totalRows);
                        },
                    };

                    var translatedRows = await doTranslate.DoTranslate(chunkSubtitle, sourcePair, targetPair, _translator, cancellationToken);

                    // Merge the chunk's translations back into the job rows by cue number.
                    foreach (var translatedRow in translatedRows)
                    {
                        var jobRow = chunkRows.FirstOrDefault(r => r.Number == translatedRow.Number);
                        if (jobRow != null && !string.IsNullOrWhiteSpace(translatedRow.TranslatedText))
                        {
                            jobRow.TargetText = translatedRow.TranslatedText;
                        }
                    }

                    chunkStart += count;
                    SaveCheckpoint(TranslationJobStage.Translating);

                    var doneNow = doneBeforeTranslate + chunkStart;
                    Report(TranslationJobStage.Translating, totalRows > 0 ? 100.0 * doneNow / totalRows : null, doneNow, totalRows);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    SaveCheckpoint(TranslationJobStage.Cancelled);
                    result.WasCancelled = true;
                    result.Rows = Rows;
                    result.Success = false;
                    return result;
                }

                // ---- Arabic text policies ----
                if (profile != null && Options.ApplyArabicPolicies)
                {
                    for (var i = 0; i < Rows.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(Rows[i].TargetText))
                        {
                            Rows[i].TargetText = ArabicTextPostProcessor.ApplyProfile(Rows[i].TargetText, profile);
                        }
                    }
                }

                // ---- quality check ----
                Report(TranslationJobStage.QualityChecking, null, 0, Rows.Count);
                var targetSubtitle = BuildSubtitleFromRows(Rows, useTargetText: true);
                var sourceForQa = sourceSubtitle ?? new Subtitle();
                SubtitleQaResult qaResult;
                if (Options.RunQualityCheck)
                {
                    qaResult = SubtitleQaService.Check(sourceForQa.Paragraphs, targetSubtitle.Paragraphs, new QaSettings(),
                        profile != null, profile, _glossary);

                    if (Options.ApplySafeFixes && profile != null)
                    {
                        var fixes = SubtitleQaService.ApplySafeFixes(targetSubtitle, profile);
                        result.SafeFixesApplied = fixes;
                        // Write fixed text back into the job rows.
                        for (var i = 0; i < Rows.Count && i < targetSubtitle.Paragraphs.Count; i++)
                        {
                            Rows[i].TargetText = targetSubtitle.Paragraphs[i].Text;
                        }
                    }
                }
                else
                {
                    qaResult = new SubtitleQaResult { TotalRows = Rows.Count };
                }

                result.QaResult = qaResult;

                // ---- record accepted outputs into the translation memory ----
                if (_memory != null)
                {
                    foreach (var row in Rows)
                    {
                        if (string.IsNullOrWhiteSpace(row.TargetText) || row.FromMemory)
                        {
                            continue;
                        }

                        _memory.Add(new TranslationMemoryEntry
                        {
                            SourceText = row.SourceText,
                            TargetText = row.TargetText,
                            SourceLanguageCode = Options.SourceLanguageCode,
                            TargetLanguageCode = Options.TargetLanguageCode,
                            StyleId = profile?.Id,
                            Engine = Options.EngineName,
                        });
                    }

                    try
                    {
                        _memory.Save();
                    }
                    catch (Exception)
                    {
                        // Memory persistence is best-effort.
                    }
                }

                // ---- export ----
                Report(TranslationJobStage.Exporting, null, 0, Rows.Count);
                if (!string.IsNullOrWhiteSpace(State.OutputSrtPath))
                {
                    var srt = targetSubtitle.ToText(new SubRip());
                    File.WriteAllText(State.OutputSrtPath, srt, new System.Text.UTF8Encoding(false));
                    result.OutputSrtPath = State.OutputSrtPath;
                }

                SaveCheckpoint(TranslationJobStage.Completed);
                Report(TranslationJobStage.Completed, 100.0, Rows.Count, Rows.Count);

                result.Success = true;
                result.Rows = Rows;
                result.TranslatedSubtitle = targetSubtitle;
                return result;
            }
            catch (OperationCanceledException)
            {
                SaveCheckpoint(TranslationJobStage.Cancelled);
                result.WasCancelled = true;
                result.Rows = Rows;
                result.Error = JobErrorInfo.FromException(new OperationCanceledException(), _translator.Name);
                return result;
            }
            catch (Exception exception)
            {
                SeLogger.Error(exception, "Translate-video job failed");
                var error = JobErrorInfo.FromException(exception, _translator.Name);
                State.ErrorMessage = error.WhatHappened;
                State.ErrorTechnical = error.TechnicalDetails;
                SaveCheckpoint(TranslationJobStage.Failed);
                result.Success = false;
                result.Rows = Rows;
                result.Error = error;
                return result;
            }
            finally
            {
                _stopwatch.Stop();
            }
        }

        private static Subtitle BuildSubtitleFromRows(List<TranslationJobRow> rows, bool useTargetText)
        {
            var subtitle = new Subtitle();
            foreach (var row in rows)
            {
                subtitle.Paragraphs.Add(new Paragraph(row.SourceText ?? string.Empty, row.StartMilliseconds, row.EndMilliseconds)
                {
                    Number = row.Number,
                    Text = useTargetText ? (row.TargetText ?? string.Empty) : (row.SourceText ?? string.Empty),
                });
            }

            return subtitle;
        }

        /// <summary>
        /// Stable content fingerprint of a subtitle (number, timing and text of every cue),
        /// used to detect resume attempts against a different input file.
        /// </summary>
        internal static string ComputeSourceSignature(Subtitle subtitle)
        {
            var sb = new StringBuilder();
            foreach (var p in subtitle.Paragraphs)
            {
                sb.Append(p.Number).Append('|')
                  .Append(p.StartTime.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)).Append('|')
                  .Append(p.EndTime.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)).Append('|')
                  .AppendLine(p.Text ?? string.Empty);
            }

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
            var hex = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }
    }
}
