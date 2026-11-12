using System.Collections.ObjectModel;
using System.Text;

namespace OyeEscucha;

public class FileItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string Icon { get; set; } = "📁";
    public string Info { get; set; } = "";
    public bool IsFolder { get; set; }
}

public partial class MainPage : ContentPage
{
    private string currentPath = "/storage/emulated/0";
    private List<FileItem> allItems = new();
    private ObservableCollection<FileItem> visibleItems = new();

    public MainPage()
    {
        InitializeComponent();
        FilesList.ItemsSource = visibleItems;
        _ = LoadFolder(currentPath);
    }

    private async Task LoadFolder(string path)
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.StorageRead>();
            // En Android 13+ pide fotos/videos
            try { await Permissions.RequestAsync<Permissions.Media>(); } catch {}

            currentPath = path;
            PathLabel.Text = path;
            StatusLabel.Text = "Cargando...";
            allItems.Clear();
            visibleItems.Clear();

            if (!Directory.Exists(path))
            {
                StatusLabel.Text = "No se pudo abrir esta carpeta";
                return;
            }

            // Carpetas primero
            foreach (var dir in Directory.GetDirectories(path))
            {
                try
                {
                    var name = Path.GetFileName(dir);
                    if (name.StartsWith(".")) continue;
                    int count = 0;
                    try { count = Directory.GetFileSystemEntries(dir).Length; } catch {}
                    var item = new FileItem { Name = name, FullPath = dir, Icon = "📁", Info = $"Carpeta • {count} elementos", IsFolder = true };
                    allItems.Add(item);
                }
                catch {}
            }

            // Archivos
            foreach (var file in Directory.GetFiles(path))
            {
                try
                {
                    var name = Path.GetFileName(file);
                    var info = new FileInfo(file);
                    string icon = "📄";
                    string ext = Path.GetExtension(name).ToLower();
                    if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif") icon = "🖼️";
                    else if (ext is ".mp4" or ".mkv" or ".mov" or ".avi") icon = "🎬";
                    else if (ext is ".mp3" or ".wav" or ".m4a" or ".ogg") icon = "🎵";
                    else if (ext is ".pdf" or ".doc" or ".docx" or ".txt" or ".xlsx") icon = "📄";
                    else if (ext is ".apk") icon = "📦";
                    else if (ext is ".zip" or ".rar") icon = "🗜️";

                    string size = info.Length < 1024 ? $"{info.Length} B" : info.Length < 1024*1024 ? $"{info.Length/1024.0:F1} KB" : $"{info.Length/1024.0/1024.0:F1} MB";
                    var item = new FileItem { Name = name, FullPath = file, Icon = icon, Info = $"Archivo • {size}", IsFolder = false };
                    allItems.Add(item);
                }
                catch {}
            }

            RefreshList("");
            StatusLabel.Text = $"{allItems.Count} elementos en esta carpeta";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Necesitas dar permiso: " + ex.Message;
        }
    }

    private void RefreshList(string search)
    {
        visibleItems.Clear();
        foreach (var item in allItems)
        {
            if (string.IsNullOrWhiteSpace(search) || item.Name.ToLower().Contains(search.ToLower()))
                visibleItems.Add(item);
        }
        if (!string.IsNullOrWhiteSpace(search))
            StatusLabel.Text = $"{visibleItems.Count} resultados para '{search}'";
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        RefreshList(e.NewTextValue ?? "");
    }

    private async void OnFileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not FileItem item) return;
        ((CollectionView)sender).SelectedItem = null;

        if (item.IsFolder)
        {
            await LoadFolder(item.FullPath);
        }
        else
        {
            try
            {
                await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(item.FullPath) });
            }
            catch (Exception ex)
            {
                await DisplayAlert("No se puede abrir", ex.Message, "OK");
            }
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        var parent = Directory.GetParent(currentPath);
        if (parent != null)
            await LoadFolder(parent.FullName);
    }

    private async void OnHomeClicked(object sender, EventArgs e)
    {
        await LoadFolder("/storage/emulated/0");
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadFolder(currentPath);
    }
}
