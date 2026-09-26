using System.Text.Json;

namespace Nikse.SubtitleEdit.UiLogic.Translate.Jobs
{
    /// <summary>One persisted row: source line and, once translated, its accepted target line.</summary>
    public sealed class TranslationJobRow
    {
        public int Number { get; set; }
        public double StartMilliseconds { get; set; }
        public double EndMilliseconds { get; set; }
        public string SourceText { get; set; } = string.Empty;
        public string TargetText { get; set; } = string.Empty;

        /// <summary>True when the row was filled by the translation memory rather than an engine.</summary>
        public bool FromMemory { get; set; }
    }

    /// <summary>
    /// Persisted job state for resume: inputs, options, stage, and all rows with whatever
    /// translations exist. Written to disk after every progress batch, so an unexpected close
    /// costs at most one batch of work - never the whole job.
    /// <para>
    /// Never contains credentials or API keys; engine configuration stays in the app settings.
    /// </para>
    /// </summary>
    public sealed class TranslationJobState
    {
        /// <summary>Bump when the layout changes incompatibly; runners refuse to resume other versions.</summary>
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public TranslationJobOptions Options { get; set; } = new TranslationJobOptions();

        public TranslationJobStage Stage { get; set; } = TranslationJobStage.Queued;

        /// <summary>Where this state file lives (filled when saved; not itself persisted twice).</summary>
        public string? StateFilePath { get; set; }

        /// <summary>Original subtitle/transcript file the job started from (null when the source came from memory only).</summary>
        public string? InputSubtitlePath { get; set; }

        /// <summary>Suggested output SRT path.</summary>
        public string? OutputSrtPath { get; set; }

        public string? ErrorMessage { get; set; }

        public string? ErrorTechnical { get; set; }

        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

        public List<TranslationJobRow> Rows { get; set; } = new List<TranslationJobRow>();

        /// <summary>True when the state can be resumed (has untranslated rows left and is not terminal).</summary>
        public bool IsResumable
        {
            get
            {
                if (Version != CurrentVersion)
                {
                    return false;
                }

                if (Stage == TranslationJobStage.Completed || Stage == TranslationJobStage.Failed)
                {
                    return false;
                }

                return Rows.Any(r => string.IsNullOrWhiteSpace(r.TargetText));
            }
        }

        /// <summary>The conventional checkpoint file path next to the intended SRT output.</summary>
        public static string GetDefaultStateFilePath(string outputSrtPath)
        {
            if (string.IsNullOrWhiteSpace(outputSrtPath))
            {
                return "translate-video.job.json";
            }

            return outputSrtPath + ".job.json";
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonOptions);
        }

        public void Save()
        {
            UpdatedUtc = DateTime.UtcNow;
            var path = StateFilePath ?? GetDefaultStateFilePath(OutputSrtPath);
            StateFilePath = path;
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(path, ToJson(), new System.Text.UTF8Encoding(false));
        }

        /// <summary>Loads a state file; returns null when missing/corrupt/wrong version (never throws).</summary>
        public static TranslationJobState? Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                var state = JsonSerializer.Deserialize<TranslationJobState>(File.ReadAllText(path), JsonOptions);
                if (state == null || state.Version != CurrentVersion)
                {
                    return null;
                }

                state.StateFilePath = path;
                return state;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };
    }
}
