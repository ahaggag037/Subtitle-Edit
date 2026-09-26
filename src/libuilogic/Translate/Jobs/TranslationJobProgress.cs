namespace Nikse.SubtitleEdit.UiLogic.Translate.Jobs
{
    /// <summary>
    /// A progress snapshot for the unified pipeline. Percent is null when the stage has no
    /// measurable total; the estimated time remaining is null until at least two progress
    /// samples in the translating stage allow a rate to be computed - the UI must never show
    /// a fabricated ETA.
    /// </summary>
    public sealed class TranslationJobProgress
    {
        public TranslationJobStage Stage { get; }

        /// <summary>0..100 when measurable for the current stage; null when indeterminate.</summary>
        public double? Percent { get; }

        public int ItemsDone { get; }
        public int ItemsTotal { get; }

        public TimeSpan Elapsed { get; }

        /// <summary>Seconds remaining for the whole job, or null when not yet measurable.</summary>
        public double? EstimatedSecondsRemaining { get; }

        public TranslationJobProgress(TranslationJobStage stage, double? percent, int itemsDone, int itemsTotal,
            TimeSpan elapsed, double? estimatedSecondsRemaining)
        {
            Stage = stage;
            Percent = percent;
            ItemsDone = itemsDone;
            ItemsTotal = itemsTotal;
            Elapsed = elapsed;
            EstimatedSecondsRemaining = estimatedSecondsRemaining;
        }
    }
}
