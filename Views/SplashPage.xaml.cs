using InstaPostGenerator.Services;

namespace InstaPostGenerator.Views;

public partial class SplashPage : ContentPage
{
    public SplashPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Fetch config from GitHub before proceeding
        try
        {
            RemoteConfigService.ConfigUrl = "https://raw.githubusercontent.com/mayank11991/insta-post-generator/main/config.json?" + DateTime.UtcNow.Ticks;
            await RemoteConfigService.GetConfigAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Splash] Config fetch failed: {ex.Message}");
        }

        await Task.Delay(1500);
        Application.Current.MainPage = new AppShell();
    }
}
