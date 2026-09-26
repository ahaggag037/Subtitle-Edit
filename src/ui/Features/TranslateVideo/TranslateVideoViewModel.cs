using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Features.Main.Layout;
using Nikse.SubtitleEdit.Features.Shared;
using Nikse.SubtitleEdit.Features.Translate.LlamaCppAdvanced;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.Media;
using Nikse.SubtitleEdit.UiLogic.AutoTranslate;
using Nikse.SubtitleEdit.UiLogic.Translate;
using Nikse.SubtitleEdit.UiLogic.Translate.Arabic;
using Nikse.SubtitleEdit.UiLogic.Translate.Jobs;
using Nikse.SubtitleEdit.UiLogic.Translate.Memory;
using Nikse.SubtitleEdit.UiLogic.Translate.Qa;
using System.Collections.ObjectModel;

namespace Nikse.SubtitleEdit.Features.TranslateVideo;

/// <summary>An engine entry in the beginner combo, with an honest LOCAL/ONLINE/HYBRID label.</summary>
public sealed class TranslateEngineItem
{
    public IAutoTranslator Translator { get; }
    public string PrivacyLabel { get; }

    public TranslateEngineItem(IAutoTranslator translator, string privacyLabel)
    {
        Translator = translator;
        PrivacyLabel = privacyLabel;
    }

    public override string ToString() => Translator.Name;
}

/// <summary>A target language entry; Arabic entries exist only when the engine really supports them.</summary>
public sealed class TranslateTargetItem
{
    public TranslationPair Pair { get; }
    public ArabicTranslationProfile? ArabicProfile { get; }

    public TranslateTargetItem(TranslationPair pair, ArabicTranslationProfile? arabicProfile)
    {
        Pair = pair;
        ArabicProfile = arabicProfile;
    }

    public override string ToString() => ArabicProfile != null ? ArabicProfile.DisplayName : Pair.Name;
}

/// <summary>A style entry; prompt-only, applied when the engine is prompt-capable.</summary>
public sealed class TranslateStyleItem
{
    public string DisplayName { get; }
    public string? PromptAddendum { get; }

    public TranslateStyleItem(string displayName, string? promptAddendum)
    {
        DisplayName = displayName;
        PromptAddendum = promptAddendum;
    }

    public override string ToString() => DisplayName;
}

/// <summary>
/// Beginner-first "Translate video" wizard: pick a video (or subtitle/transcript), pick Arabic
/// (Modern Standard or Egyptian, only where the engine really supports it) or another language,
/// pick an optional style, and run one unified job with honest progress, QA and export.
/// <para>
/// This window orchestrates the existing services; it owns no translation, ASR or export logic
/// of its own. Advanced users keep the full Auto-translate / Speech-to-text windows.
/// </para>
/// </summary>
public partial class TranslateVideoViewModel : ObservableObject
{
    private const string PrivacyLocal = "LOCAL";
    private const string PrivacyOnline = "ONLINE";
    private const string PrivacyHybrid = "LOCAL/ONLINE";

    private CancellationTokenSource? _cancellationTokenSource;
    private TranslationJobRunner? _runner;

    public Window? Window { get; set; }

    [ObservableProperty] private string _videoFileName = string.Empty;
    [ObservableProperty] private string _subtitleFileName = string.Empty;
    [ObservableProperty] private bool _hasVideoFile;
    [ObservableProperty] private bool _hasSubtitleFile;

    public ObservableCollection<TranslateEngineItem> Engines { get; } = new();
    [ObservableProperty] private TranslateEngineItem? _selectedEngine;

    public ObservableCollection<TranslateTargetItem> Targets { get; } = new();
    [ObservableProperty] private TranslateTargetItem? _selectedTarget;

    public ObservableCollection<TranslateStyleItem> Styles { get; } = new();
    [ObservableProperty] private TranslateStyleItem? _selectedStyle;

    [ObservableProperty] private bool _useTranslationMemory;

    [ObservableProperty] private string _privacyText = string.Empty;

    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private bool _progressIndeterminate = true;
    [ObservableProperty] private string _etaText = string.Empty;

    [ObservableProperty] private string _resultText = string.Empty;
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private string _outputSrtPath = string.Empty;
    [ObservableProperty] private string _qaSummaryText = string.Empty;

    public RelayCommand PickVideoCommand { get; }
    public RelayCommand PickSubtitleCommand { get; }
    public RelayCommand TranslateCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand OpenInEditorCommand { get; }
    public RelayCommand DoneCommand { get; }

    public TranslateVideoViewModel()
    {
        PickVideoCommand = new RelayCommand(async () => await PickVideoAsync());
        PickSubtitleCommand = new RelayCommand(async () => await PickSubtitleAsync());
        TranslateCommand = new RelayCommand(async () => await TranslateAsync());
        CancelCommand = new RelayCommand(Cancel);
        OpenInEditorCommand = new RelayCommand(async () => await OpenInEditorAsync());
        DoneCommand = new RelayCommand(() => Window?.Close());

        Engines.Add(new TranslateEngineItem(new LlamaCppAdvancedTranslate(), PrivacyLocal));
        Engines.Add(new TranslateEngineItem(new OllamaAdvancedTranslate(), PrivacyLocal));
        Engines.Add(new TranslateEngineItem(new OllamaTranslate(), PrivacyLocal));
        Engines.Add(new TranslateEngineItem(new ChatGptTranslate(), PrivacyOnline));
        Engines.Add(new TranslateEngineItem(new GeminiTranslate(), PrivacyOnline));
        Engines.Add(new TranslateEngineItem(new OpenAiCompatibleTranslate(), PrivacyHybrid));
        Engines.Add(new TranslateEngineItem(new DeepLTranslate(), PrivacyOnline));
        Engines.Add(new TranslateEngineItem(new GoogleTranslateV2(), PrivacyOnline));

        SelectedEngine = Engines[0];

        Styles.Add(new TranslateStyleItem("Default", null));
        Styles.Add(new TranslateStyleItem("Formal", "Use a formal, respectful register throughout."));
        Styles.Add(new TranslateStyleItem("Conversational", "Use a relaxed, everyday conversational register."));
        Styles.Add(new TranslateStyleItem("Cinematic", "This is a drama or film: keep the dialogue natural, character-driven and emotionally faithful."));
        Styles.Add(new TranslateStyleItem("Documentary", "This is a documentary: prefer clear, informative narration phrasing."));
        Styles.Add(new TranslateStyleItem("Technical", "This is technical content: keep terminology precise and consistent; do not localize technical terms loosely."));
        SelectedStyle = Styles[0];
    }

    /// <summary>Called by the menu command; pre-fills the transcript when the editor has a saved subtitle open.</summary>
    public void Initialize(Subtitle? currentSubtitle)
    {
        if (currentSubtitle != null && !string.IsNullOrEmpty(currentSubtitle.FileName) && File.Exists(currentSubtitle.FileName))
        {
            SubtitleFileName = currentSubtitle.FileName;
            HasSubtitleFile = true;
        }

        UpdatePrivacyText();
    }

    private async Task PickVideoAsync()
    {
        if (Window == null)
        {
            return;
        }

        var files = await NativePickers.OpenFilePickerAsync(Window, new FilePickerOpenOptions
        {
            Title = "Open video file",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Video files") { Patterns = new[] { "*.mp4", "*.mkv", "*.avi", "*.mov", "*.wmv", "*.webm", "*.ts", "*.m2ts" } },
                new("All files") { Patterns = new[] { "*.*" } },
            }
        });

        if (files.Count == 0)
        {
            return;
        }

        VideoFileName = files[0].Path.LocalPath;
        HasVideoFile = File.Exists(VideoFileName);

        // Convenience: if a subtitle/transcript with the same name exists, suggest it.
        var sibling = Path.ChangeExtension(VideoFileName, ".srt");
        if (!HasSubtitleFile && File.Exists(sibling))
        {
            SubtitleFileName = sibling;
            HasSubtitleFile = true;
        }
    }

    private async Task PickSubtitleAsync()
    {
        if (Window == null)
        {
            return;
        }

        var files = await NativePickers.OpenFilePickerAsync(Window, new FilePickerOpenOptions
        {
            Title = "Open subtitle or transcript",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Subtitle files") { Patterns = new[] { "*.srt", "*.ass", "*.ssa", "*.vtt", "*.sub", "*.txt" } },
                new("All files") { Patterns = new[] { "*.*" } },
            }
        });

        if (files.Count == 0)
        {
            return;
        }

        SubtitleFileName = files[0].Path.LocalPath;
        HasSubtitleFile = File.Exists(SubtitleFileName);
    }

    private void UpdatePrivacyText()
    {
        if (SelectedEngine == null)
        {
            PrivacyText = string.Empty;
            return;
        }

        var whatLeaves = SelectedEngine.PrivacyLabel == PrivacyOnline
            ? "Subtitle text is sent to the online service. The video and audio never leave this computer on this path."
            : SelectedEngine.PrivacyLabel == PrivacyHybrid
                ? "Subtitle text goes to the configured service address; it can be local or remote depending on the URL."
                : "Nothing leaves this computer: the engine runs locally.";
        PrivacyText = $"{SelectedEngine.PrivacyLabel}: {whatLeaves}";
    }

    partial void OnSelectedEngineChanged(TranslateEngineItem? value)
    {
        UpdatePrivacyText();
        RebuildTargets();
        OnPropertyChanged(nameof(StylesEnabled));
    }

    partial void OnSelectedTargetChanged(TranslateTargetItem? value)
    {
        OnPropertyChanged(nameof(StylesEnabled));
    }

    /// <summary>
    /// Styles are prompt-level instructions: they are only offered when the selected engine
    /// actually honors instructions. For phrase-based engines the UI must not pretend a style
    /// does anything.
    /// </summary>
    public bool StylesEnabled => SelectedEngine != null && IsPromptCapableEngine(SelectedEngine.Translator);

    private void RebuildTargets()
    {
        var oldSelection = SelectedTarget;
        Targets.Clear();
        if (SelectedEngine == null)
        {
            return;
        }

        var supported = SelectedEngine.Translator.GetSupportedTargetLanguages();
        var arabicSeen = false;
        var egyptianSeen = false;
        foreach (var pair in supported)
        {
            var profile = ArabicProfileCatalog.GetByLanguageCode(pair.TwoLetterIsoLanguageName);
            if (profile == null)
            {
                profile = ArabicProfileCatalog.GetByLanguageCode(pair.Code);
            }

            if (profile != null)
            {
                if (profile.IsModernStandard && !arabicSeen)
                {
                    arabicSeen = true;
                    Targets.Add(new TranslateTargetItem(pair, profile));
                }
                else if (profile.IsEgyptian && !egyptianSeen)
                {
                    egyptianSeen = true;
                    Targets.Add(new TranslateTargetItem(pair, profile));
                }
            }
        }

        // Everything the engine offers beyond the Arabic entries, alphabetically, skipping
        // duplicate Arabic entries we already added in their canonical form.
        foreach (var pair in supported.Where(p => ArabicProfileCatalog.GetByLanguageCode(p.TwoLetterIsoLanguageName) == null &&
                                                  ArabicProfileCatalog.GetByLanguageCode(p.Code) == null)
                                     .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            Targets.Add(new TranslateTargetItem(pair, null));
        }

        SelectedTarget = oldSelection != null
            ? Targets.FirstOrDefault(t => t.Pair.Code == oldSelection.Pair.Code)
            : Targets.FirstOrDefault();
    }

    private bool IsPromptCapableEngine(IAutoTranslator translator)
    {
        // Engines that honor a full-sentence instruction block. Classic phrase-based MT engines
        // have no prompt at all, so styles/profiles must not be offered for them.
        return translator is ChatGptTranslate or GeminiTranslate or OllamaTranslate or LlamaCppTranslate
            or LlamaCppAdvancedTranslate or OllamaAdvancedTranslate or OpenAiCompatibleTranslate or OpenRouterTranslate
            or GroqTranslate or DeepSeekTranslate or AnthropicTranslate or MistralTranslate or PerplexityTranslate
            or LmStudioTranslate or KoboldCppTranslate or AvalAi or ApiRouteTranslate;
    }

    private async Task TranslateAsync()
    {
        if (Window == null || IsRunning)
        {
            return;
        }

        if (!HasSubtitleFile || string.IsNullOrWhiteSpace(SubtitleFileName))
        {
            await MessageBox.Show(Window, "Nothing to translate",
                "Choose a video that has a subtitle/transcript next to it, or pick a subtitle file.\n\n" +
                "To create subtitles from the video's audio, use Video ▸ Speech to text first (one-time engine download may be required).",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (SelectedEngine == null || SelectedTarget == null)
        {
            await MessageBox.Show(Window, "Choose an engine and a language",
                "Select a translation engine and a target language.",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var sourceSubtitle = LoadSubtitleFile(SubtitleFileName);
        if (sourceSubtitle == null || sourceSubtitle.Paragraphs.Count == 0)
        {
            await MessageBox.Show(Window, "Could not read the subtitle file",
                "The selected file could not be parsed as a subtitle or plain-text transcript.",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var translator = SelectedEngine.Translator;
        var profile = SelectedTarget.ArabicProfile;
        var style = SelectedStyle;
        var styleAddendum = style?.PromptAddendum;

        var outputSrt = BuildDefaultOutputPath(SubtitleFileName, SelectedTarget);
        var options = new TranslationJobOptions
        {
            SourceLanguageCode = "en",
            SourceLanguageName = "English",
            TargetLanguageCode = profile?.TargetLanguageCode ?? TwoLetterCodeOf(translator, SelectedTarget.Pair),
            TargetLanguageName = SelectedTarget.Pair.Name,
            ArabicProfileId = profile?.Id,
            StylePromptAddendum = styleAddendum,
            EngineName = translator.Name,
            UseTranslationMemory = UseTranslationMemory,
            TranslationMemoryFolder = string.Empty, // falls back to the app-wide default via the provider hook
            GlossaryFilePath = Glossary.GetDefaultFilePath(SubtitleFileName),
            ApplyArabicPolicies = profile != null,
            RunQualityCheck = true,
            ApplySafeFixes = profile != null,
        };

        var promptSnapshot = new EnginePromptSnapshot(translator);
        promptSnapshot.ApplyProfile(profile, styleAddendum);

        _cancellationTokenSource = new CancellationTokenSource();
        _runner = new TranslationJobRunner(translator, options);
        _runner.Progress += OnJobProgress;

        IsRunning = true;
        HasResult = false;
        ResultText = string.Empty;
        QaSummaryText = string.Empty;
        StatusText = "Starting…";
        ProgressIndeterminate = true;

        try
        {
            var result = await _runner.RunAsync(sourceSubtitle, outputSrt, _cancellationTokenSource.Token);
            ShowResult(result);
        }
        finally
        {
            promptSnapshot.Restore();
            _runner.Progress -= OnJobProgress;
            IsRunning = false;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private void OnJobProgress(TranslationJobProgress progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText = progress.Stage.ToDisplayText();
            if (progress.Percent.HasValue)
            {
                ProgressIndeterminate = false;
                ProgressPercent = Math.Clamp(progress.Percent.Value, 0, 100);
            }
            else
            {
                ProgressIndeterminate = true;
            }

            if (progress.EstimatedSecondsRemaining.HasValue)
            {
                var seconds = progress.EstimatedSecondsRemaining.Value;
                EtaText = seconds < 90 ? $"about {seconds:0}s left" : $"about {seconds / 60:0} min left";
            }
            else
            {
                EtaText = string.Empty;
            }
        });
    }

    private void ShowResult(TranslationJobResult result)
    {
        if (result.Error != null && !result.WasCancelled)
        {
            StatusText = "Failed";
            ResultText = $"{result.Error.WhatHappened}\n\nWhy: {result.Error.Why}\n\nWhat you can do: {result.Error.WhatToDo}";
            HasResult = true;
            return;
        }

        if (result.WasCancelled)
        {
            StatusText = "Cancelled";
            ResultText = "The job was cancelled. Progress was saved and can be resumed from this window later.";
            HasResult = true;
            return;
        }

        StatusText = "Completed";
        var warningCount = result.QaResult.WarningCount;
        var errorCount = result.QaResult.ErrorCount;
        QaSummaryText = errorCount == 0 && warningCount == 0
            ? "Quality check: all lines passed."
            : $"Quality check: {errorCount} error(s), {warningCount} warning(s) to review.";
        ResultText = "Translation finished." + (result.SafeFixesApplied.Count > 0 ? $" {result.SafeFixesApplied.Count} line(s) auto-polished (punctuation/quotes)." : string.Empty);
        OutputSrtPath = result.OutputSrtPath ?? string.Empty;
        HasResult = true;
    }

    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    private async Task OpenInEditorAsync()
    {
        if (!string.IsNullOrWhiteSpace(OutputSrtPath) && File.Exists(OutputSrtPath))
        {
            await MainWindowFactory.OpenNewWindowWithFile(OutputSrtPath);
            Window?.Close();
        }
    }

    private static string BuildDefaultOutputPath(string subtitlePath, TranslateTargetItem target)
    {
        var dir = Path.GetDirectoryName(subtitlePath);
        var name = Path.GetFileNameWithoutExtension(subtitlePath);
        var suffix = target.ArabicProfile != null
            ? (target.ArabicProfile.IsEgyptian ? ".egyptian-arabic" : ".arabic")
            : "." + (target.Pair.TwoLetterIsoLanguageName ?? target.Pair.Code);
        var fileName = name + suffix + ".srt";
        return string.IsNullOrEmpty(dir) ? fileName : Path.Combine(dir, fileName);
    }

    private static string TwoLetterCodeOf(TranslationPair pair)
    {
        if (!string.IsNullOrEmpty(pair.TwoLetterIsoLanguageName))
        {
            return pair.TwoLetterIsoLanguageName;
        }

        return pair.Code;
    }

    private static Subtitle? LoadSubtitleFile(string path)
    {
        try
        {
            if (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return ImportPlainTextAsSubtitle(File.ReadAllLines(path));
            }

            return Subtitle.Parse(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Minimal plain-text import: one paragraph per non-empty line pair with even timing spread.</summary>
    private static Subtitle ImportPlainTextAsSubtitle(string[] lines)
    {
        var subtitle = new Subtitle();
        var nonEmpty = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var startMs = 1000.0;
        foreach (var line in nonEmpty)
        {
            var durationMs = Math.Max(1200, 65.0 * line.Length);
            subtitle.Paragraphs.Add(new Paragraph(line.Trim(), startMs, startMs + durationMs));
            startMs += durationMs + 200;
        }

        return subtitle;
    }

    /// <summary>
    /// Applies the Arabic profile / style addendum to the engine's existing prompt seam and
    /// restores the previous value afterwards - the wizard never permanently rewrites user
    /// prompts. Prompt-less engines get no changes (and the UI hides styles for them).
    /// </summary>
    private sealed class EnginePromptSnapshot
    {
        private readonly IAutoTranslator _translator;
        private readonly bool _isAdvanced;
        private string? _savedPrompt;
        private string? _savedGlossary;
        private bool _applied;

        public EnginePromptSnapshot(IAutoTranslator translator)
        {
            _translator = translator;
            _isAdvanced = translator is LlamaCppAdvancedTranslate or OllamaAdvancedTranslate;
        }

        public void ApplyProfile(ArabicTranslationProfile? profile, string? styleAddendum)
        {
            if (profile == null && string.IsNullOrWhiteSpace(styleAddendum))
            {
                return;
            }

            var addendum = string.Join(" ", new[] { profile?.PromptAddendum, styleAddendum }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(addendum))
            {
                return;
            }

            // The addendum keeps the {0}/{1} placeholders so the engine's own substitution keeps working.
            if (_isAdvanced)
            {
                _savedPrompt = Se.Settings.AutoTranslate.LlamaCppAdvanced.Prompt;
                _savedGlossary = Se.Settings.AutoTranslate.LlamaCppAdvanced.Glossary;
                Se.Settings.AutoTranslate.LlamaCppAdvanced.Prompt = CombinePrompts(_savedPrompt, addendum);
            }
            else
            {
                _savedPrompt = ReadEnginePrompt();
                if (_savedPrompt != null)
                {
                    WriteEnginePrompt(CombinePrompts(_savedPrompt, addendum));
                }
            }

            _applied = true;
        }

        public void Restore()
        {
            if (!_applied)
            {
                return;
            }

            if (_isAdvanced)
            {
                Se.Settings.AutoTranslate.LlamaCppAdvanced.Prompt = _savedPrompt;
                Se.Settings.AutoTranslate.LlamaCppAdvanced.Glossary = _savedGlossary;
            }
            else
            {
                WriteEnginePrompt(_savedPrompt);
            }

            _applied = false;
        }

        private static string CombinePrompts(string? existing, string addendum)
        {
            if (string.IsNullOrWhiteSpace(existing) || existing.Trim().StartsWith("Translate from", StringComparison.OrdinalIgnoreCase))
            {
                // Replace the bare default with a fuller instruction set; the base request stays intact.
                return "Translate from {0} to {1}, keep line breaks exactly the same, do not censor the translation, give only the output without comments: " + addendum;
            }

            return existing.TrimEnd() + " " + addendum;
        }

        private string? ReadEnginePrompt()
        {
            var tools = Configuration.Settings.Tools;
            return _translator switch
            {
                ChatGptTranslate => tools.ChatGptPrompt,
                GeminiTranslate => tools.GeminiPrompt,
                OllamaTranslate => tools.OllamaPrompt,
                LlamaCppTranslate => tools.LlamaCppPrompt,
                OpenAiCompatibleTranslate => tools.OpenAiCompatibleTranslatePrompt,
                OpenRouterTranslate => tools.OpenRouterPrompt,
                GroqTranslate => tools.GroqPrompt,
                DeepSeekTranslate => tools.DeepSeekPrompt,
                AnthropicTranslate => tools.AnthropicPrompt,
                MistralTranslate => tools.AutoTranslateMistralPrompt,
                PerplexityTranslate => tools.PerplexityPrompt,
                LmStudioTranslate => tools.LmStudioPrompt,
                KoboldCppTranslate => tools.KoboldCppPrompt,
                AvalAi => tools.AvalAiPrompt,
                ApiRouteTranslate => tools.ApiRoutePrompt,
                _ => null,
            };
        }

        private void WriteEnginePrompt(string? value)
        {
            var tools = Configuration.Settings.Tools;
            switch (_translator)
            {
                case ChatGptTranslate: tools.ChatGptPrompt = value; break;
                case GeminiTranslate: tools.GeminiPrompt = value; break;
                case OllamaTranslate: tools.OllamaPrompt = value; break;
                case LlamaCppTranslate: tools.LlamaCppPrompt = value; break;
                case OpenAiCompatibleTranslate: tools.OpenAiCompatibleTranslatePrompt = value; break;
                case OpenRouterTranslate: tools.OpenRouterPrompt = value; break;
                case GroqTranslate: tools.GroqPrompt = value; break;
                case DeepSeekTranslate: tools.DeepSeekPrompt = value; break;
                case AnthropicTranslate: tools.AnthropicPrompt = value; break;
                case MistralTranslate: tools.AutoTranslateMistralPrompt = value; break;
                case PerplexityTranslate: tools.PerplexityPrompt = value; break;
                case LmStudioTranslate: tools.LmStudioPrompt = value; break;
                case KoboldCppTranslate: tools.KoboldCppPrompt = value; break;
                case AvalAi: tools.AvalAiPrompt = value; break;
                case ApiRouteTranslate: tools.ApiRoutePrompt = value; break;
            }
        }
    }
}
