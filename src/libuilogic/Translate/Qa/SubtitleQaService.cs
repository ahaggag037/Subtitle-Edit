using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Common.TextLengthCalculator;
using Nikse.SubtitleEdit.UiLogic.Translate.Arabic;
using Nikse.SubtitleEdit.UiLogic.Translate.Memory;

namespace Nikse.SubtitleEdit.UiLogic.Translate.Qa
{
    /// <summary>Outcome for a single row.</summary>
    public enum QaRowStatus
    {
        Pass,
        Warning,
        Error,
    }

    /// <summary>Aggregated result of one QA pass.</summary>
    public sealed class SubtitleQaResult
    {
        public List<QaIssue> Issues { get; } = new List<QaIssue>();
        public int TotalRows { get; set; }

        public int WarningCount => Issues.Count(i => i.Severity == QaSeverity.Warning);
        public int ErrorCount => Issues.Count(i => i.Severity == QaSeverity.Error);

        /// <summary>Rows that have at least one Error finding.</summary>
        public List<int> GetRowIndexesWithErrors()
        {
            return Issues.Where(i => i.Severity == QaSeverity.Error && i.RowIndex >= 0)
                         .Select(i => i.RowIndex)
                         .Distinct()
                         .ToList();
        }

        /// <summary>True when the row has no issues at all.</summary>
        public QaRowStatus GetRowStatus(int rowIndex)
        {
            var hasWarning = false;
            foreach (var issue in Issues)
            {
                if (issue.RowIndex != rowIndex)
                {
                    continue;
                }

                if (issue.Severity == QaSeverity.Error)
                {
                    return QaRowStatus.Error;
                }

                hasWarning = true;
            }

            return hasWarning ? QaRowStatus.Warning : QaRowStatus.Pass;
        }
    }

    /// <summary>
    /// Deterministic Arabic-aware subtitle QA. Checks the translated lines (not the source) for
    /// the failure modes that actually occur after machine translation, plus timing checks that
    /// the editor's own fix engine covers but a translation workflow should catch *before* the
    /// user sees the result.
    /// <para>
    /// No check rewrites meaning. The only automatic fixes are the profile-driven text policies
    /// (punctuation, quotes, bidi-control stripping) applied via
    /// <see cref="ArabicTextPostProcessor.ApplyProfile"/> - everything else is reported for review.
    /// </para>
    /// </summary>
    public static class SubtitleQaService
    {
        /// <summary>
        /// Runs all checks. <paramref name="source"/> and <paramref name="target"/> must be index-aligned
        /// (same paragraph order); target paragraphs carry the translated text.
        /// </summary>
        public static SubtitleQaResult Check(IList<Paragraph> source, IList<Paragraph> target, QaSettings settings,
            bool isTargetArabic, ArabicTranslationProfile? profile, Glossary? glossary)
        {
            var result = new SubtitleQaResult { TotalRows = target.Count };
            var seenTargets = new Dictionary<string, int>();

            for (var index = 0; index < target.Count; index++)
            {
                var sourceParagraph = index < source.Count ? source[index] : null;
                var paragraph = target[index];
                var number = paragraph.Number > 0 ? paragraph.Number : index + 1;
                var text = paragraph.Text ?? string.Empty;

                // --- empty translation ---
                if (string.IsNullOrWhiteSpace(text))
                {
                    var sourceIsEmpty = sourceParagraph == null || string.IsNullOrWhiteSpace(sourceParagraph.Text);
                    if (!sourceIsEmpty && (sourceParagraph == null || !MergeAndSplitHelper.IsMusicLine(sourceParagraph.Text)))
                    {
                        result.Issues.Add(new QaIssue(QaCheckIds.EmptyTranslation, QaSeverity.Error, index, number,
                            "Translation is empty", autoFixable: false));
                        continue;
                    }

                    // Music lines kept in the source language are legitimately untranslated+untargeted.
                    continue;
                }

                var sourceText = sourceParagraph?.Text ?? string.Empty;
                var isMusic = sourceParagraph != null && MergeAndSplitHelper.IsMusicLine(sourceText);

                // --- untranslated line ---
                if (!isMusic && sourceText.Length > 0 && text.Trim() == sourceText.Trim())
                {
                    result.Issues.Add(new QaIssue(QaCheckIds.UntranslatedLine, QaSeverity.Warning, index, number,
                        "Line was not translated", autoFixable: false));
                }

                if (isTargetArabic)
                {
                    CheckArabicLine(result, index, number, text, settings);
                }

                // --- line length ---
                foreach (var displayLine in text.SplitToLines())
                {
                    var length = displayLine.Trim().Length;
                    if (length > settings.MaxLineLength)
                    {
                        result.Issues.Add(new QaIssue(QaCheckIds.LineTooLong, QaSeverity.Warning, index, number,
                            $"Line is {length} characters (maximum is {settings.MaxLineLength})", autoFixable: false));
                    }
                }

                // --- reading speed (Arabic-aware: diacritics never count) ---
                var durationSeconds = paragraph.DurationTotalSeconds;
                if (durationSeconds > 0.2 && !isMusic)
                {
                    var calculator = isTargetArabic
                        ? (ICalcLength)new CalcIgnoreArabicDiacritics()
                        : new CalcAll();
                    var characters = calculator.CountCharacters(text, true);
                    var cps = (double)characters / durationSeconds;
                    if (cps > settings.MaxCharactersPerSecond)
                    {
                        result.Issues.Add(new QaIssue(QaCheckIds.ReadingSpeed, QaSeverity.Warning, index, number,
                            $"Reading speed is {cps:0.#} characters per second (maximum is {settings.MaxCharactersPerSecond:0.#})", autoFixable: false));
                    }

                    // --- duration limits ---
                    var durationMs = paragraph.DurationTotalMilliseconds;
                    if (durationMs < settings.MinDurationMilliseconds)
                    {
                        result.Issues.Add(new QaIssue(QaCheckIds.MinDuration, QaSeverity.Warning, index, number,
                            $"Duration is {(int)durationMs} ms (minimum is {settings.MinDurationMilliseconds} ms)", autoFixable: false));
                    }
                    else if (durationMs > settings.MaxDurationMilliseconds)
                    {
                        result.Issues.Add(new QaIssue(QaCheckIds.MaxDuration, QaSeverity.Warning, index, number,
                            $"Duration is {(int)durationMs} ms (maximum is {settings.MaxDurationMilliseconds} ms)", autoFixable: false));
                    }
                }

                // --- duplicate translations ---
                if (!isMusic)
                {
                    var key = TranslationMemory.NormalizeForMatch(text);
                    if (key.Length > 0)
                    {
                        if (seenTargets.TryGetValue(key, out var firstIndex))
                        {
                            var firstNumber = target[firstIndex].Number > 0 ? target[firstIndex].Number : firstIndex + 1;
                            result.Issues.Add(new QaIssue(QaCheckIds.DuplicateTranslation, QaSeverity.Warning, index, number,
                                $"Translation is identical to #{firstNumber}", autoFixable: false));
                        }
                        else
                        {
                            seenTargets[key] = index;
                        }
                    }
                }

                // --- glossary compliance ---
                if (glossary != null && sourceText.Length > 0)
                {
                    foreach (var term in glossary.FindMatches(sourceText))
                    {
                        var translationPresent = !string.IsNullOrWhiteSpace(term.Translation) &&
                                                 TranslationMemory.NormalizeForMatch(text)
                                                     .Contains(TranslationMemory.NormalizeForMatch(term.Translation), StringComparison.Ordinal);
                        var termKeptAsIs = TranslationMemory.NormalizeForMatch(text)
                            .Contains(TranslationMemory.NormalizeForMatch(term.Term), StringComparison.Ordinal);
                        if (!translationPresent && !termKeptAsIs)
                        {
                            result.Issues.Add(new QaIssue(QaCheckIds.GlossaryViolation, QaSeverity.Warning, index, number,
                                $"Glossary term '{term.Term}' was not translated as '{term.Translation}'", autoFixable: false));
                        }
                    }
                }
            }

            // --- overlapping time codes (file-level order check) ---
            for (var index = 1; index < target.Count; index++)
            {
                var previous = target[index - 1];
                var current = target[index];
                if (current.StartTime.TotalMilliseconds < previous.EndTime.TotalMilliseconds - 0.01)
                {
                    var number = current.Number > 0 ? current.Number : index + 1;
                    result.Issues.Add(new QaIssue(QaCheckIds.TimeCodeOverlap, QaSeverity.Error, index, number,
                        $"Time code overlaps with #{(previous.Number > 0 ? previous.Number : index)}", autoFixable: false));
                }
            }

            return result;
        }

        private static void CheckArabicLine(SubtitleQaResult result, int index, int number, string text, QaSettings settings)
        {
            foreach (var displayLine in text.SplitToLines())
            {
                if (ArabicTextPostProcessor.ContainsBidiControls(displayLine))
                {
                    result.Issues.Add(new QaIssue(QaCheckIds.BidiControls, QaSeverity.Warning, index, number,
                        "Line contains Unicode bidi control characters", autoFixable: true));
                }

                if (ArabicTextPostProcessor.ContainsArabicPresentationForms(displayLine))
                {
                    result.Issues.Add(new QaIssue(QaCheckIds.PresentationForms, QaSeverity.Warning, index, number,
                        "Line contains Arabic presentation forms (should be standard Arabic code points)", autoFixable: false));
                }

                if (ArabicTextPostProcessor.IsMostlyArabic(displayLine))
                {
                    if (displayLine.Contains('"'))
                    {
                        result.Issues.Add(new QaIssue(QaCheckIds.StraightQuotes, QaSeverity.Warning, index, number,
                            "Line contains straight quotes (Arabic text should use « »)", autoFixable: true));
                    }

                    var converted = ArabicTextPostProcessor.ConvertPunctuation(displayLine);
                    if (converted != displayLine)
                    {
                        result.Issues.Add(new QaIssue(QaCheckIds.LatinPunctuation, QaSeverity.Warning, index, number,
                            "Line contains Latin punctuation where Arabic punctuation is expected", autoFixable: true));
                    }

                    // Long Latin runs inside Arabic text are the classic source of scrambled
                    // rendering; report so the user can add directional marks or keep the run short.
                    var latinRun = 0;
                    for (var i = 0; i < displayLine.Length; i++)
                    {
                        if (ArabicTextPostProcessor.IsLatinLetter(displayLine[i]))
                        {
                            latinRun++;
                            if (latinRun >= settings.MixedDirectionMinLatinRun)
                            {
                                result.Issues.Add(new QaIssue(QaCheckIds.MixedDirectionHint, QaSeverity.Warning, index, number,
                                    $"Arabic line contains a long Latin text run ({latinRun}+ characters) - check bidi rendering", autoFixable: false));
                                break;
                            }
                        }
                        else
                        {
                            latinRun = 0;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Applies the safe, meaning-preserving fixes the QA layer can do on its own:
        /// the profile's text policies per line. Returns human-readable descriptions of
        /// what was changed (one entry per row that changed).
        /// </summary>
        public static List<string> ApplySafeFixes(Subtitle target, ArabicTranslationProfile? profile)
        {
            var applied = new List<string>();
            if (target == null || profile == null)
            {
                return applied;
            }

            for (var index = 0; index < target.Paragraphs.Count; index++)
            {
                var paragraph = target.Paragraphs[index];
                var fixedText = ArabicTextPostProcessor.ApplyProfile(paragraph.Text, profile);
                if (fixedText != paragraph.Text)
                {
                    paragraph.Text = fixedText;
                    var number = paragraph.Number > 0 ? paragraph.Number : index + 1;
                    applied.Add($"#{number}: applied Arabic text policies (punctuation/quotes/bidi controls)");
                }
            }

            return applied;
        }
    }
}
