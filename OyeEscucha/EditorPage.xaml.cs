namespace OyeEscucha;

public partial class EditorPage : ContentPage
{
    private string filePath;
    public EditorPage(string path)
    {
        InitializeComponent();
        filePath = path;
        FileNameLabel.Text = Path.GetFileName(path);
        try { CodeEditor.Text = File.ReadAllText(path); } catch { CodeEditor.Text = ""; }
    }
    private async void OnCancelClicked(object sender, EventArgs e) => await Navigation.PopAsync();
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            File.WriteAllText(filePath, CodeEditor.Text ?? "");
            await DisplayAlert("Guardado", "Archivo guardado.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex) { await DisplayAlert("Error", ex.Message, "OK"); }
    }
}
