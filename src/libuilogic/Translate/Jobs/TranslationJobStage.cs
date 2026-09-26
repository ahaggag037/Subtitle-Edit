namespace Nikse.SubtitleEdit.UiLogic.Translate.Jobs
{
    /// <summary>
    /// The unified pipeline stages of a translate-video job, in execution order.
    /// Matches the single progress model shown to the user: one job, one timeline.
    /// </summary>
    public enum TranslationJobStage
    {
        /// <summary>Job created, not started.</summary>
        Queued,

        /// <summary>Inputs being loaded/validated.</summary>
        Preparing,

        /// <summary>Speech-to-text producing the source transcript (skipped when a subtitle is supplied).</summary>
        Transcribing,

        /// <summary>Translating the transcript lines.</summary>
        Translating,

        /// <summary>Running the subtitle QA layer.</summary>
        QualityChecking,

        /// <summary>Waiting for/performing user review (transient; not persisted as a block).</summary>
        Reviewing,

        /// <summary>Writing output files.</summary>
        Exporting,

        Completed,
        Failed,
        Cancelled,
    }

    /// <summary>Canonical display names for the stages (kept engine- and UI-language-free; the UI translates).</summary>
    public static class TranslationJobStageExtensions
    {
        public static string ToDisplayText(this TranslationJobStage stage)
        {
            switch (stage)
            {
                case TranslationJobStage.Queued: return "Queued";
                case TranslationJobStage.Preparing: return "Preparing";
                case TranslationJobStage.Transcribing: return "Transcribing";
                case TranslationJobStage.Translating: return "Translating";
                case TranslationJobStage.QualityChecking: return "Quality check";
                case TranslationJobStage.Reviewing: return "Review";
                case TranslationJobStage.Exporting: return "Exporting";
                case TranslationJobStage.Completed: return "Completed";
                case TranslationJobStage.Failed: return "Failed";
                case TranslationJobStage.Cancelled: return "Cancelled";
                default: return stage.ToString();
            }
        }
    }
}
