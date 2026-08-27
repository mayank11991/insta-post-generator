using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using InstaPostGenerator.Models;

namespace InstaPostGenerator.ViewModels;

public class PostItemViewModel : INotifyPropertyChanged
{
    private readonly MainPageViewModel _parentViewModel;
    private string _copyButtonText = "📋 Copy Caption + Hashtags";

    public PostItemViewModel(PostDisplayItem item, int index, MainPageViewModel parentViewModel)
    {
        _parentViewModel = parentViewModel;
        Item = item;
        Index = index + 1;
        CopyCommand = new Command(async () => await CopyToClipboardAsync());
    }

    public PostDisplayItem Item { get; }
    public int Index { get; }
    public string DisplayTitle => $"{Index}. {Item.Hook}";
    public string CategoryDisplay => $"[{Item.CategoryLabel}]";
    public string SeriesDisplay => $"Series: {Item.SeriesName}";
    public string CaptionPreview => Item.Caption;
    public string HashtagsDisplay => Item.Hashtags;
    public string SourceDisplay => $"Source: {Item.SourceUrl}";

    public string CopyButtonText
    {
        get => _copyButtonText;
        set { _copyButtonText = value; OnPropertyChanged(); }
    }

    public ICommand CopyCommand { get; }

    private async Task CopyToClipboardAsync()
    {
        var fullText = $"{Item.Caption}\n\n{Item.Hashtags}";
        await Clipboard.SetTextAsync(fullText);
        CopyButtonText = "Copied ✓";
        await Task.Delay(2000);
        CopyButtonText = "📋 Copy Caption + Hashtags";
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}