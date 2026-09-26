using Nikse.SubtitleEdit.Core.Common;

namespace Nikse.SubtitleEdit.UiLogic.Translate.Qa
{
    /// <summary>
    /// Thresholds for the subtitle QA layer. Defaults come from the user's existing Subtitle Edit
    /// general settings (line maximum length, max CPS, min/max display time) so the QA engine
    /// never disagrees with the editor's own error list about what is acceptable.
    /// </summary>
    public sealed class QaSettings
    {
        public int MaxLineLength { get; set; }
        public double MaxCharactersPerSecond { get; set; }
        public int MinDurationMilliseconds { get; set; }
        public int MaxDurationMilliseconds { get; set; }

        /// <summary>Minimum Latin-run length in an Arabic line before a mixed-direction hint is raised.</summary>
        public int MixedDirectionMinLatinRun { get; set; } = 15;

        public QaSettings()
        {
            var general = Configuration.Settings.General;
            MaxLineLength = general.SubtitleLineMaximumLength > 0 ? general.SubtitleLineMaximumLength : 43;
            MaxCharactersPerSecond = general.SubtitleMaximumCharactersPerSeconds > 0 ? general.SubtitleMaximumCharactersPerSeconds : 25.0;
            MinDurationMilliseconds = general.SubtitleMinimumDisplayMilliseconds > 0 ? general.SubtitleMinimumDisplayMilliseconds : 1000;
            MaxDurationMilliseconds = general.SubtitleMaximumDisplayMilliseconds > 0 ? general.SubtitleMaximumDisplayMilliseconds : 8000;
        }
    }
}
