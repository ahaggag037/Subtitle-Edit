namespace Nikse.SubtitleEdit.UiLogic.Translate.Qa
{
    /// <summary>Severity of a single QA finding.</summary>
    public enum QaSeverity
    {
        /// <summary>Should be fixed before export (data loss or unreadable output).</summary>
        Error,

        /// <summary>Worth reviewing; may be intentional.</summary>
        Warning,
    }

    /// <summary>Check identifiers reported by <see cref="QaIssue.CheckId"/>.</summary>
    public static class QaCheckIds
    {
        public const string EmptyTranslation = "EmptyTranslation";
        public const string UntranslatedLine = "UntranslatedLine";
        public const string LatinPunctuation = "LatinPunctuation";
        public const string StraightQuotes = "StraightQuotes";
        public const string BidiControls = "BidiControls";
        public const string PresentationForms = "PresentationForms";
        public const string LineTooLong = "LineTooLong";
        public const string ReadingSpeed = "ReadingSpeed";
        public const string MinDuration = "MinDuration";
        public const string MaxDuration = "MaxDuration";
        public const string TimeCodeOverlap = "TimeCodeOverlap";
        public const string DuplicateTranslation = "DuplicateTranslation";
        public const string GlossaryViolation = "GlossaryViolation";
        public const string MixedDirectionHint = "MixedDirectionHint";
    }

    /// <summary>One QA finding for one subtitle row (RowIndex -1 = whole file).</summary>
    public sealed class QaIssue
    {
        public string CheckId { get; }
        public QaSeverity Severity { get; }

        /// <summary>Zero-based index into the subtitle's paragraphs, or -1 for a file-level finding.</summary>
        public int RowIndex { get; }

        /// <summary>One-based cue number as shown in the grid.</summary>
        public int Number { get; }

        public string Message { get; }

        /// <summary>True when the runner/UI can fix this finding automatically without rewriting meaning.</summary>
        public bool AutoFixable { get; }

        public QaIssue(string checkId, QaSeverity severity, int rowIndex, int number, string message, bool autoFixable)
        {
            CheckId = checkId;
            Severity = severity;
            RowIndex = rowIndex;
            Number = number;
            Message = message;
            AutoFixable = autoFixable;
        }

        public override string ToString()
        {
            var prefix = Number > 0 ? $"#{Number}: " : string.Empty;
            return prefix + Message;
        }
    }
}
