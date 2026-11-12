namespace OyeEscucha;

public partial class LanguagePage : ContentPage
{
    private TaskCompletionSource<string> tcs = new();
    public Task<string> ResultTask => tcs.Task;
    public LanguagePage() { InitializeComponent(); }
    private async void Close(string lang)
    {
        tcs.TrySetResult(lang);
        await Navigation.PopModalAsync();
    }
    private void OnSpanish(object sender, EventArgs e) => Close("es");
    private void OnEnglish(object sender, EventArgs e) => Close("en");
    private void OnFrench(object sender, EventArgs e) => Close("fr");
    private void OnCreole(object sender, EventArgs e) => Close("ht");
}
