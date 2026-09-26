using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Features.TranslateVideo;

namespace Nikse.SubtitleEdit.Features.Main;

/// <summary>
/// The guided "Translate video" entry point, kept in its own partial file so the main
/// <see cref="MainViewModel"/> body stays untouched (it is already very large).
/// </summary>
public partial class MainViewModel
{
    [RelayCommand]
    private async Task ShowTranslateVideo()
    {
        if (Window == null)
        {
            return;
        }

        Subtitle? currentSubtitle = null;
        if (!IsEmpty)
        {
            currentSubtitle = GetUpdateSubtitle();
        }

        await ShowDialogAsync<TranslateVideoWindow, TranslateVideoViewModel>(vm => { vm.Initialize(currentSubtitle); });
    }
}
