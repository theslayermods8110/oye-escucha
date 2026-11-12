namespace OyeEscucha;

public partial class EditorPage : ContentPage
{
    private string filePath;
    private bool isSaving = false;

    public EditorPage(string path)
    {
        InitializeComponent();
        filePath = path;
        FileNameLabel.Text = Path.GetFileName(path);
        try { CodeEditor.Text = File.ReadAllText(path); } catch { CodeEditor.Text = ""; }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (isSaving) return;
        isSaving = true;
        SaveBtn.IsEnabled = false;
        SaveBtn.Text = "Guardando...";
        try
        {
            File.WriteAllText(filePath, CodeEditor.Text ?? "");
            SavedPopup.IsVisible = true;
            await Task.Delay(1200);
            SavedPopup.IsVisible = false;
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            SavedPopup.IsVisible = false;
            await DisplayAlertAsync("Error", ex.Message, "OK");
            SaveBtn.IsEnabled = true;
            SaveBtn.Text = "Guardar";
            isSaving = false;
        }
    }
}
