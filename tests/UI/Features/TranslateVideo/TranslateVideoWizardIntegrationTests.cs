using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Nikse.SubtitleEdit.Features.Main;
using Nikse.SubtitleEdit.Features.Translate;
using Nikse.SubtitleEdit.Features.Translate.LlamaCppAdvanced;
using Nikse.SubtitleEdit.Features.TranslateVideo;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.UiLogic.AutoTranslate;
using Nikse.SubtitleEdit.UiLogic.Translate;
using Xunit;

namespace UITests.Features.TranslateVideo;

/// <summary>
/// Wizard integration coverage at three explicitly separate verification levels - these must
/// never be collapsed into a single "tested" label:
/// <para>
/// VM-ONLY: the view model logic works headless (engine list, privacy labels, capability
/// filtering, style gating). No window, no rendering.
/// </para>
/// <para>
/// CONSTRUCTED-WINDOW: the real TranslateVideoWindow is constructed, shown in the headless
/// Avalonia session, its DataContext/command/property bindings evaluated live, and closed.
/// This proves the window code-behind executes, not that pixels look right (no RENDERED-UI
/// or FULL-USER-WORKFLOW claim is made here).
/// </para>
/// </summary>
public class TranslateVideoWizardIntegrationTests : IDisposable
{
    public TranslateVideoWizardIntegrationTests()
    {
    }

    public void Dispose()
    {
    }

    // Minimal phrase-based engine double: no Arabic support, no prompts.
    private sealed class StubPhraseEngine : IAutoTranslator
    {
        public string Name => "StubPhraseEngine";
        public string Url => "https://example.com";
        public string Error { get; set; } = string.Empty;
        public int MaxCharacters => 500;

        public void Initialize()
        {
        }

        public List<TranslationPair> GetSupportedSourceLanguages() => new List<TranslationPair> { new TranslationPair("English", "en") };

        public List<TranslationPair> GetSupportedTargetLanguages() => new List<TranslationPair>
        {
            new TranslationPair("French", "fr"),
            new TranslationPair("German", "de"),
        };

        public Task<string> Translate(string text, string sourceLanguageCode, string targetLanguageCode, CancellationToken cancellationToken)
        {
            return Task.FromResult(text);
        }
    }

    // ------------------------------------------------------------------
    // Level 1: VM-only (no window)
    // ------------------------------------------------------------------

    [Fact]
    public void Engines_Populate_WithHonestPrivacyLabels()
    {
        var vm = new TranslateVideoViewModel();

        Assert.Equal(8, vm.Engines.Count);
        Assert.Equal(PrivacyExpectedLocal(vm.Engines[0].Translator.Name), vm.Engines[0].PrivacyLabel.StartsWith("LOCAL", StringComparison.Ordinal));

        // Default engine: local llama.cpp advanced.
        Assert.Equal(new LlamaCppAdvancedTranslate().Name, vm.SelectedEngine!.Translator.Name);
        Assert.Contains("LOCAL", vm.PrivacyText, StringComparison.Ordinal);
        Assert.Contains("Nothing leaves this computer", vm.PrivacyText, StringComparison.Ordinal);

        var online = vm.Engines.First(e => e.Translator is ChatGptTranslate);
        Assert.Equal("ONLINE", online.PrivacyLabel);

        var hybrid = vm.Engines.First(e => e.Translator is OpenAiCompatibleTranslate);
        Assert.Equal("LOCAL/ONLINE", hybrid.PrivacyLabel);
    }

    private static bool PrivacyExpectedLocal(string engineName)
    {
        return engineName.Contains("llama.cpp", StringComparison.OrdinalIgnoreCase) ||
               engineName.Contains("Ollama", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectingLlmEngine_OffersExactlyOneMsa_AndOneEgyptian_FromRealEngineList()
    {
        var vm = new TranslateVideoViewModel();
        var chatGptItem = vm.Engines.First(e => e.Translator is ChatGptTranslate);
        vm.SelectedEngine = chatGptItem;

        // The real ChatGpt-style language list contains "Arabic" (ar) AND "Egyptian Arabic"
        // (arz) - the wizard must surface exactly one target for each profile, never invent
        // entries for engines that do not list them.
        var msa = vm.Targets.Where(t => t.ArabicProfile != null && t.ArabicProfile.IsModernStandard).ToList();
        var egyptian = vm.Targets.Where(t => t.ArabicProfile != null && t.ArabicProfile.IsEgyptian).ToList();
        Assert.Single(msa);
        Assert.Single(egyptian);
        Assert.Equal("arz", egyptian[0].ArabicProfile!.TargetLanguageCode);

        // Non-Arabic targets are still offered alongside.
        Assert.Contains(vm.Targets, t => t.ArabicProfile == null && t.Pair.Code == "fr");
    }

    [Fact]
    public void EngineWithoutArabic_ShowsNoArabicTargets_AndDisablesStyles()
    {
        var vm = new TranslateVideoViewModel();
        var stub = new TranslateEngineItem(new StubPhraseEngine(), "ONLINE");
        vm.Engines.Add(stub);
        vm.SelectedEngine = stub;

        Assert.DoesNotContain(vm.Targets, t => t.ArabicProfile != null);
        Assert.Equal(2, vm.Targets.Count);
        Assert.False(vm.StylesEnabled);
    }

    [Fact]
    public void StyleGating_PromptEnginesOnly()
    {
        var vm = new TranslateVideoViewModel();

        // Default (llama.cpp advanced, an LLM): styles are meaningful.
        Assert.True(vm.StylesEnabled);
        Assert.Equal(6, vm.Styles.Count);

        // Phrase-based engines cannot follow style instructions: the UI must not pretend.
        var google = vm.Engines.First(e => e.Translator is GoogleTranslateV2);
        vm.SelectedEngine = google;
        Assert.False(vm.StylesEnabled);

        var llama = vm.Engines.First(e => e.Translator is LlamaCppAdvancedTranslate);
        vm.SelectedEngine = llama;
        Assert.True(vm.StylesEnabled);
    }

    [Fact]
    public void OutputPath_ReflectsArabicProfileChoice()
    {
        var vm = new TranslateVideoViewModel();
        vm.SelectedEngine = vm.Engines.First(e => e.Translator is ChatGptTranslate);

        var msa = vm.Targets.First(t => t.ArabicProfile != null && t.ArabicProfile.IsModernStandard);
        var path1 = InvokeBuildDefaultOutputPath(vm, "/tmp/movie.srt", msa);
        Assert.EndsWith("movie.arabic.srt", path1, StringComparison.Ordinal);

        var egyptian = vm.Targets.First(t => t.ArabicProfile != null && t.ArabicProfile.IsEgyptian);
        var path2 = InvokeBuildDefaultOutputPath(vm, "/tmp/movie.srt", egyptian);
        Assert.EndsWith("movie.egyptian-arabic.srt", path2, StringComparison.Ordinal);
    }

    private static string InvokeBuildDefaultOutputPath(TranslateVideoViewModel vm, string subtitlePath, TranslateTargetItem target)
    {
        var method = typeof(TranslateVideoViewModel).GetMethod("BuildDefaultOutputPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return (string)method!.Invoke(null, new object?[] { subtitlePath, target })!;
    }

    // ------------------------------------------------------------------
    // Level 2: DI construction
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void Di_Registers_AndResolves_TranslateVideoViewModel()
    {
        var services = new ServiceCollection();
        services.AddSubtitleEditServices();
        Locator.Services = services.BuildServiceProvider();

        var vm = Locator.Services.GetRequiredService<TranslateVideoViewModel>();
        Assert.NotNull(vm);
        Assert.Equal(8, vm.Engines.Count);
    }

    // ------------------------------------------------------------------
    // Level 2: constructed window with live bindings (headless render session)
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void ConstructedWindow_BindsLive_AndClosesViaDoneCommand()
    {
        var vm = new TranslateVideoViewModel();
        var window = new TranslateVideoWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            // DataContext wired.
            Assert.Equal(vm, window.DataContext);
            Assert.Same(window, vm.Window);

            // Property binding is live: progress bar follows the view model.
            var progressBar = window.GetVisualDescendants().OfType<Avalonia.Controls.ProgressBar>().First();
            vm.ProgressPercent = 42;
            vm.ProgressIndeterminate = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(42, progressBar.Value);

            // Command bindings are live: the translate button is bound to TranslateCommand and
            // is disabled while a job runs (inverse binding), enabled again afterwards.
            var translateButton = window.GetVisualDescendants().OfType<Avalonia.Controls.Button>()
                .First(b => b.Command == vm.TranslateCommand);
            vm.IsRunning = true;
            Dispatcher.UIThread.RunJobs();
            Assert.False(translateButton.IsEffectivelyEnabled);
            vm.IsRunning = false;
            Dispatcher.UIThread.RunJobs();
            Assert.True(translateButton.IsEffectivelyEnabled);

            // Done closes the window.
            vm.DoneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.False(window.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }
}
