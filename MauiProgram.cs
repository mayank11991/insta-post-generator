using Microsoft.Extensions.Logging;
using InstaPostGenerator.Views;
using InstaPostGenerator.ViewModels;

namespace InstaPostGenerator;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("BricolageGrotesque.ttf", "BricolageGrotesque");
                fonts.AddFont("Montserrat.ttf", "Montserrat");
                fonts.AddFont("Montserrat-Italic.ttf", "Montserrat-Italic");
                fonts.AddFont("NotoSansDevanagari-Regular.ttf", "NotoSansDevanagari");
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler<Microsoft.Maui.Controls.Button, Microsoft.Maui.Handlers.ButtonHandler>();
#endif
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Register services
        builder.Services.AddSingleton<MainPageViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<AboutPage>();

        return builder.Build();
    }
}