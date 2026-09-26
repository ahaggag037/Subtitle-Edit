using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.ValueConverters;

namespace Nikse.SubtitleEdit.Features.TranslateVideo;

/// <summary>
/// Beginner-first guided "Translate video" window. Deliberately small: every expert control
/// stays in the existing Auto-translate / Speech-to-text / Burn-in windows; this flow links to
/// the result instead of duplicating their options.
/// </summary>
public class TranslateVideoWindow : Window
{
    private readonly TranslateVideoViewModel _vm;

    public TranslateVideoWindow(TranslateVideoViewModel vm)
    {
        UiUtil.InitializeWindow(this, GetType().Name);
        Title = "Translate video — guided";
        Width = 780;
        MinWidth = 640;
        Height = 700;
        MinHeight = 560;
        CanResize = true;

        DataContext = vm;
        vm.Window = this;
        _vm = vm;

        var videoLabel = UiUtil.MakeTextBlock("Video file:");
        var videoValue = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 420,
        };
        videoValue.Bind(TextBlock.TextProperty, new Binding(nameof(vm.VideoFileName)));
        var videoRow = UiUtil.MakeHorizontalPanel(
            videoLabel,
            videoValue,
            UiUtil.MakeBrowseButton(vm.PickVideoCommand));

        var subtitleLabel = UiUtil.MakeTextBlock("Subtitle / transcript:");
        var subtitleValue = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 380,
        };
        subtitleValue.Bind(TextBlock.TextProperty, new Binding(nameof(vm.SubtitleFileName)));
        var subtitleRow = UiUtil.MakeHorizontalPanel(
            subtitleLabel,
            subtitleValue,
            UiUtil.MakeBrowseButton(vm.PickSubtitleCommand));

        var engineCombo = UiUtil.MakeComboBox(vm.Engines, vm, nameof(vm.SelectedEngine));
        var engineRow = UiUtil.MakeHorizontalPanel(UiUtil.MakeTextBlock("Engine:"), engineCombo);

        var privacyBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.85,
            MaxWidth = 600,
            Margin = new Thickness(0, 2, 0, 8),
        };
        privacyBlock.Bind(TextBlock.TextProperty, new Binding(nameof(vm.PrivacyText)));

        var targetCombo = UiUtil.MakeComboBox(vm.Targets, vm, nameof(vm.SelectedTarget));
        var targetRow = UiUtil.MakeHorizontalPanel(UiUtil.MakeTextBlock("Translate to:"), targetCombo);

        var styleCombo = UiUtil.MakeComboBox(vm.Styles, vm, nameof(vm.SelectedStyle));
        styleCombo.Bind(ComboBox.IsEnabledProperty, new Binding(nameof(vm.StylesEnabled)));
        var styleRow = UiUtil.MakeHorizontalPanel(UiUtil.MakeTextBlock("Style:"), styleCombo);

        var memoryCheck = UiUtil.MakeCheckBox("Reuse previous translations (translation memory)", vm, nameof(vm.UseTranslationMemory));

        var translateButton = UiUtil.MakeButton("Translate", vm.TranslateCommand);
        translateButton.Bind(Button.IsEnabledProperty, new Binding(nameof(vm.IsRunning)) { Converter = InverseBooleanConverter.Instance });
        var cancelButton = UiUtil.MakeButton("Cancel", vm.CancelCommand);
        cancelButton.Bind(Button.IsEnabledProperty, new Binding(nameof(vm.IsRunning)));

        var progressBar = UiUtil.MakeProgressBar();
        progressBar.Bind(ProgressBar.ValueProperty, new Binding(nameof(vm.ProgressPercent)) { Mode = BindingMode.TwoWay });
        progressBar.Bind(ProgressBar.IsIndeterminateProperty, new Binding(nameof(vm.ProgressIndeterminate)));
        progressBar.Maximum = 100;

        var statusBlock = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
        };
        statusBlock.Bind(TextBlock.TextProperty, new Binding(nameof(vm.StatusText)));

        var etaBlock = new TextBlock { Opacity = 0.8 };
        etaBlock.Bind(TextBlock.TextProperty, new Binding(nameof(vm.EtaText)));

        var progressRow = UiUtil.MakeHorizontalPanel(statusBlock, progressBar, etaBlock);
        progressBar.Width = 300;
        progressRow.Margin = new Thickness(0, 12, 0, 0);

        var resultBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 640,
            Margin = new Thickness(0, 8, 0, 0),
        };
        resultBlock.Bind(TextBlock.TextProperty, new Binding(nameof(vm.ResultText)));
        resultBlock.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(vm.HasResult)));

        var qaBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.9,
            MaxWidth = 640,
        };
        qaBlock.Bind(TextBlock.TextProperty, new Binding(nameof(vm.QaSummaryText)));
        qaBlock.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(vm.HasResult)));

        var outputBlock = new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = 0.8,
            MaxWidth = 600,
        };
        outputBlock.Bind(TextBlock.TextProperty, new Binding(nameof(vm.OutputSrtPath)));
        outputBlock.Bind(TextBlock.IsVisibleProperty, new Binding(nameof(vm.HasResult)));

        var openInEditorButton = UiUtil.MakeButton("Open result in editor", vm.OpenInEditorCommand);
        var doneButton = UiUtil.MakeButtonDone(vm.DoneCommand);
        var footer = UiUtil.MakeButtonBar(openInEditorButton, doneButton);

        var stack = new StackPanel
        {
            Margin = UiUtil.MakeWindowMargin(),
            Spacing = 10,
        };
        stack.Children.Add(UiUtil.MakeTextBlock("1. Choose the video"));
        stack.Children.Add(videoRow);
        stack.Children.Add(UiUtil.MakeTextBlock("2. Subtitle or transcript to translate (a .srt next to the video is picked up automatically; create one via Video ▸ Speech to text)"));
        stack.Children.Add(subtitleRow);
        stack.Children.Add(UiUtil.MakeTextBlock("3. Engine and privacy"));
        stack.Children.Add(engineRow);
        stack.Children.Add(privacyBlock);
        stack.Children.Add(UiUtil.MakeTextBlock("4. Translate to"));
        stack.Children.Add(targetRow);
        stack.Children.Add(UiUtil.MakeTextBlock("5. Style (for engines that follow instructions)"));
        stack.Children.Add(styleRow);
        stack.Children.Add(memoryCheck);
        stack.Children.Add(UiUtil.MakeHorizontalSeparator());
        stack.Children.Add(UiUtil.MakeHorizontalPanel(translateButton, cancelButton));
        stack.Children.Add(progressRow);
        stack.Children.Add(resultBlock);
        stack.Children.Add(qaBlock);
        stack.Children.Add(outputBlock);

        var scroll = new ScrollViewer { Content = stack };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("* ,Auto"),
            RowSpacing = 10,
        };
        grid.Children.Add(scroll);
        Grid.SetRow(scroll, 0);
        grid.Children.Add(footer);
        Grid.SetRow(footer, 1);

        Content = grid;

        Loaded += (_, _) => UiUtil.RestoreWindowPosition(this);
    }
}
