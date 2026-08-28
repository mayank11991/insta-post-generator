using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using InstaPostGenerator.Models;
using InstaPostGenerator.Services;

namespace InstaPostGenerator.ViewModels;

public class PostItemViewModel : INotifyPropertyChanged
{
    private readonly MainPageViewModel _parentViewModel;
    private string _copyButtonText = "📋 Copy Caption + Hashtags";
    private string _postButtonText = "📤 Send to Instagram";
    private bool _isPosting;
    private bool _isPosted;

    public PostItemViewModel(PostDisplayItem item, int index, MainPageViewModel parentViewModel)
    {
        _parentViewModel = parentViewModel;
        Item = item;
        Index = index + 1;
        CopyCommand = new Command(async () => await CopyToClipboardAsync());
        PostCommand = new Command(async () => await PostToInstagramAsync(), () => !IsPosting && !IsPosted);
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

    public string PostButtonText
    {
        get => _postButtonText;
        set { _postButtonText = value; OnPropertyChanged(); }
    }

    public bool IsPosting
    {
        get => _isPosting;
        set { _isPosting = value; OnPropertyChanged(); OnPropertyChanged(nameof(PostButtonEnabled)); ((Command)PostCommand).ChangeCanExecute(); }
    }

    public bool IsPosted
    {
        get => _isPosted;
        set { _isPosted = value; OnPropertyChanged(); OnPropertyChanged(nameof(PostButtonEnabled)); ((Command)PostCommand).ChangeCanExecute(); }
    }

    public bool PostButtonEnabled => !IsPosting && !IsPosted;

    public ICommand CopyCommand { get; }
    public ICommand PostCommand { get; }

    private async Task CopyToClipboardAsync()
    {
        var fullText = $"{Item.Caption}\n\n{Item.Hashtags}";
        await Clipboard.SetTextAsync(fullText);
        CopyButtonText = "Copied ✓";
        await Task.Delay(2000);
        CopyButtonText = "📋 Copy Caption + Hashtags";
    }

    private async Task PostToInstagramAsync()
    {
        if (IsPosting || IsPosted) return;

        IsPosting = true;
        PostButtonText = "📤 Uploading...";

        try
        {
            var result = await InstagramService.PostToInstagramAsync(
                Item.ImagePath,
                Item.Caption,
                Item.Hashtags,
                status => PostButtonText = $"📤 {status}");

            if (result.StartsWith("Posted!"))
            {
                IsPosted = true;
                PostButtonText = "✅ Posted!";
            }
            else
            {
                PostButtonText = $"❌ {result}";
                await Task.Delay(3000);
                PostButtonText = "📤 Send to Instagram";
                IsPosting = false;
            }
        }
        catch (Exception ex)
        {
            PostButtonText = $"❌ Error: {ex.Message}";
            await Task.Delay(3000);
            PostButtonText = "📤 Send to Instagram";
            IsPosting = false;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
