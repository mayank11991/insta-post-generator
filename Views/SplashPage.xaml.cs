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
        await Task.Delay(1500);
        Application.Current.MainPage = new AppShell();
    }
}
