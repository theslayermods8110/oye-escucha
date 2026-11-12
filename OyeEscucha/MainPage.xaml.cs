using System.Collections.ObjectModel;

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
    private string lang = "es";
    private bool langAsked = false;

    // Textos por idioma
    private Dictionary<string, Dictionary<string,string>> T = new()
    {
        ["es"] = new() { ["title"]="Mis Archivos", ["search"]="Buscar archivos...", ["back"]="⬅ Atrás", ["home"]="🏠 Inicio", ["refresh"]="🔄 Recargar", ["lang"]="Cambiar idioma", ["empty"]="Carpeta vacía o sin permiso. Dale al botón naranja.", ["loading"]="Cargando...", ["folder"]="Carpeta", ["file"]="Archivo", ["items"]="elementos", ["results"]="resultados para", ["choose"]="Elige tu idioma / Choose your language", ["perm"]="⚠️ Dale aquí para dar permiso total y ver dentro de carpetas" },
        ["en"] = new() { ["title"]="My Files", ["search"]="Search files...", ["back"]="⬅ Back", ["home"]="🏠 Home", ["refresh"]="🔄 Reload", ["lang"]="Change language", ["empty"]="Empty folder or no permission. Tap orange button.", ["loading"]="Loading...", ["folder"]="Folder", ["file"]="File", ["items"]="items", ["results"]="results for", ["choose"]="Choose your language", ["perm"]="⚠️ Tap here to give full permission and see inside folders" },
        ["fr"] = new() { ["title"]="Mes Fichiers", ["search"]="Rechercher...", ["back"]="⬅ Retour", ["home"]="🏠 Accueil", ["refresh"]="🔄 Recharger", ["lang"]="Changer de langue", ["empty"]="Dossier vide ou sans permission.", ["loading"]="Chargement...", ["folder"]="Dossier", ["file"]="Fichier", ["items"]="éléments", ["results"]="résultats pour", ["choose"]="Choisis ta langue", ["perm"]="⚠️ Appuie ici pour donner permission totale" },
        ["ht"] = new() { ["title"]="Fichye M yo", ["search"]="Chèche fichye...", ["back"]="⬅ Tounen", ["home"]="🏠 Akèy", ["refresh"]="🔄 Rechaje", ["lang"]="Chanje lang", ["empty"]="Dosye vid oswa san pèmisyon.", ["loading"]="Ap chaje...", ["folder"]="Dosye", ["file"]="Fichye", ["items"]="eleman", ["results"]="rezilta pou", ["choose"]="Chwazi lang ou", ["perm"]="⚠️ Peze isit la pou bay pèmisyon total" },
    };

    public MainPage()
    {
        InitializeComponent();
        FilesList.ItemsSource = visibleItems;
        lang = Preferences.Get("idioma", "");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (string.IsNullOrEmpty(lang) && !langAsked)
        {
            langAsked = true;
            await AskLanguage();
        }
        else if (!string.IsNullOrEmpty(lang))
        {
            ApplyLanguage();
        }
        CheckPermissionButton();
        await LoadFolder(currentPath);
    }

    private async Task AskLanguage()
    {
        string res = await DisplayActionSheet(T["es"]["choose"], "OK", null, "🇩🇴 Español", "🇺🇸 English", "🇫🇷 Français", "🇭🇹 Kreyòl");
        if (res.Contains("English")) lang = "en";
        else if (res.Contains("Français")) lang = "fr";
        else if (res.Contains("Kreyòl")) lang = "ht";
        else lang = "es";
        Preferences.Set("idioma", lang);
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        var d = T[lang];
        TitleLabel.Text = d["title"];
        SearchEntry.Placeholder = d["search"];
        BackBtn.Text = d["back"];
        HomeBtn.Text = d["home"];
        RefreshBtn.Text = d["refresh"];
        LangBtn.Text = d["lang"];
        PermissionBtn.Text = d["perm"];
    }

    private void CheckPermissionButton()
    {
#if ANDROID
        try
        {
            bool hasAll = Android.OS.Environment.IsExternalStorageManager;
            PermissionBtn.IsVisible = !hasAll;
        }
        catch { PermissionBtn.IsVisible = true; }
#else
        PermissionBtn.IsVisible = false;
#endif
    }

    private async void OnPermissionClicked(object sender, EventArgs e)
    {
#if ANDROID
        try
        {
            var intent = new Android.Content.Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
            intent.SetData(Android.Net.Uri.Parse("package:" + Android.App.Application.Context.PackageName));
            Android.App.Application.Context.StartActivity(intent);
            await DisplayAlert("Permiso", "Activa 'Permitir acceso a todos los archivos' y vuelve a la app.", "OK");
        }
        catch
        {
            var intent = new Android.Content.Intent(Android.Provider.Settings.ActionManageAllFilesAccessPermission);
            Android.App.Application.Context.StartActivity(intent);
        }
#endif
        await Task.Delay(500);
        CheckPermissionButton();
        await LoadFolder(currentPath);
    }

    private async void OnChangeLangClicked(object sender, EventArgs e)
    {
        langAsked = false;
        lang = "";
        await AskLanguage();
        await LoadFolder(currentPath);
    }

    private async Task LoadFolder(string path)
    {
        try
        {
            await Permissions.RequestAsync<Permissions.StorageRead>();
            try { await Permissions.RequestAsync<Permissions.Media>(); } catch {}

            currentPath = path;
            PathLabel.Text = path;
            var d = T.ContainsKey(lang) ? T[lang] : T["es"];
            StatusLabel.Text = d["loading"];
            allItems.Clear();
            visibleItems.Clear();

            if (!Directory.Exists(path))
            {
                StatusLabel.Text = d["empty"];
                return;
            }

            // Usar Java File para Android - lee mejor que Directory
            string[] dirs = Array.Empty<string>();
            string[] files = Array.Empty<string>();
            try { dirs = Directory.GetDirectories(path); } catch {}
            try { files = Directory.GetFiles(path); } catch {}

            // Si Directory falla, intentar con Java
            if (dirs.Length == 0 && files.Length == 0)
            {
#if ANDROID
                try
                {
                    var jFile = new Java.IO.File(path);
                    var list = jFile.ListFiles();
                    if (list != null)
                    {
                        foreach (var f in list)
                        {
                            if (f.IsDirectory) dirs = dirs.Append(f.AbsolutePath).ToArray();
                            else files = files.Append(f.AbsolutePath).ToArray();
                        }
                    }
                }
                catch {}
#endif
            }

            foreach (var dir in dirs)
            {
                try
                {
                    var name = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(name) || name.StartsWith(".")) continue;
                    int count = 0;
                    try { count = Directory.GetFileSystemEntries(dir).Length; } catch {
#if ANDROID
                        try { count = new Java.IO.File(dir).ListFiles()?.Length ?? 0; } catch {}
#endif
                    }
                    allItems.Add(new FileItem { Name = name, FullPath = dir, Icon = "📁", Info = $"{d["folder"]} • {count} {d["items"]}", IsFolder = true });
                }
                catch {}
            }

            foreach (var file in files)
            {
                try
                {
                    var name = Path.GetFileName(file);
                    if (string.IsNullOrEmpty(name)) continue;
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
                    allItems.Add(new FileItem { Name = name, FullPath = file, Icon = icon, Info = $"{d["file"]} • {size}", IsFolder = false });
                }
                catch {}
            }

            RefreshList("");
            StatusLabel.Text = $"{allItems.Count} {d["items"]}";
            CheckPermissionButton();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Error: " + ex.Message;
            PermissionBtn.IsVisible = true;
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
        {
            var d = T.ContainsKey(lang) ? T[lang] : T["es"];
            StatusLabel.Text = $"{visibleItems.Count} {d["results"]} '{search}'";
        }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RefreshList(e.NewTextValue ?? "");

    private async void OnFileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not FileItem item) return;
        ((CollectionView)sender).SelectedItem = null;
        if (item.IsFolder)
        {
            SearchEntry.Text = "";
            await LoadFolder(item.FullPath);
        }
        else
        {
            try { await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(item.FullPath) }); }
            catch (Exception ex) { await DisplayAlert("No se puede abrir", ex.Message, "OK"); }
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        var parent = Directory.GetParent(currentPath);
        if (parent != null) await LoadFolder(parent.FullName);
    }
    private async void OnHomeClicked(object sender, EventArgs e) => await LoadFolder("/storage/emulated/0");
    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadFolder(currentPath);
}
