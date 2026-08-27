using InstaPostGenerator.Views;
using InstaPostGenerator.Services;

namespace InstaPostGenerator;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Fetch config on startup (non-blocking)
        _ = InitializeConfigAsync();
        return new Window(new SplashPage());
    }

    private static async Task InitializeConfigAsync()
    {
        try
        {
            // Set your GitHub raw config URL here
            // RemoteConfigService.ConfigUrl = "https://raw.githubusercontent.com/YOUR_USERNAME/YOUR_REPO/main/config.json";

            await RemoteConfigService.GetConfigAsync();
            System.Diagnostics.Debug.WriteLine("[App] Config loaded successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Config load failed, using defaults: {ex.Message}");
        }
    }
}
