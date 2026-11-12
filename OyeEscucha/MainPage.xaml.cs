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

    private Dictionary<string, Dictionary<string,string>> T = new()
    {
        ["es"] = new() { ["title"]="Mis Archivos", ["search"]="Buscar archivos...", ["back"]="⬅ Atrás", ["home"]="🏠 Inicio", ["refresh"]="🔄 Recargar", ["lang"]="Cambiar idioma", ["empty"]="Carpeta vacía o sin permiso.", ["loading"]="Cargando...", ["folder"]="Carpeta", ["file"]="Archivo", ["items"]="elementos", ["results"]="resultados para", ["choose"]="Elige tu idioma / Choose your language", ["perm"]="⚠️ Dar permiso total para ver dentro de carpetas" },
        ["en"] = new() { ["title"]="My Files", ["search"]="Search files...", ["back"]="⬅ Back", ["home"]="🏠 Home", ["refresh"]="🔄 Reload", ["lang"]="Change language", ["empty"]="Empty folder or no permission.", ["loading"]="Loading...", ["folder"]="Folder", ["file"]="File", ["items"]="items", ["results"]="results for", ["choose"]="Choose your language", ["perm"]="⚠️ Give full permission to see inside folders" },
        ["fr"] = new() { ["title"]="Mes Fichiers", ["search"]="Rechercher...", ["back"]="⬅ Retour", ["home"]="🏠 Accueil", ["refresh"]="🔄 Recharger", ["lang"]="Changer de langue", ["empty"]="Dossier vide ou sans permission.", ["loading"]="Chargement...", ["folder"]="Dossier", ["file"]="Fichier", ["items"]="éléments", ["results"]="résultats pour", ["choose"]="Choisis ta langue", ["perm"]="⚠️ Donner permission totale" },
        ["ht"] = new() { ["title"]="Fichye M yo", ["search"]="Chèche fichye...", ["back"]="⬅ Tounen", ["home"]="🏠 Akèy", ["refresh"]="🔄 Rechaje", ["lang"]="Chanje lang", ["empty"]="Dosye vid oswa san pèmisyon.", ["loading"]="Ap chaje...", ["folder"]="Dosye", ["file"]="Fichye", ["items"]="eleman", ["results"]="rezilta pou", ["choose"]="Chwazi lang ou", ["perm"]="⚠️ Bay pèmisyon total" },
    };

    public MainPage()
    {
        InitializeComponent();
        FilesList.ItemsSource = visibleItems;
        try { lang = Preferences.Get("idioma", "es"); } catch { lang = "es"; }
        if (string.IsNullOrEmpty(lang)) lang = "es";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            if (string.IsNullOrEmpty(Preferences.Get("idioma", "")) && !langAsked)
            {
                langAsked = true;
                await AskLanguage();
            }
            ApplyLanguage();
            CheckPermissionButton();
            await LoadFolder(currentPath);
        }
        catch { }
    }

    private async Task AskLanguage()
    {
        try
        {
            string res = await DisplayActionSheet("Elige tu idioma / Choose your language", "OK", null, "🇩🇴 Español", "🇺🇸 English", "🇫🇷 Français", "🇭🇹 Kreyòl");
            if (res != null && res.Contains("English")) lang = "en";
            else if (res != null && res.Contains("Français")) lang = "fr";
            else if (res != null && res.Contains("Kreyòl")) lang = "ht";
            else lang = "es";
            Preferences.Set("idioma", lang);
        }
        catch { lang = "es"; }
    }

    private void ApplyLanguage()
    {
        try
        {
            var d = T.ContainsKey(lang) ? T[lang] : T["es"];
            TitleLabel.Text = d["title"];
            SearchEntry.Placeholder = d["search"];
            BackBtn.Text = d["back"];
            HomeBtn.Text = d["home"];
            RefreshBtn.Text = d["refresh"];
            LangBtn.Text = d["lang"];
            PermissionBtn.Text = d["perm"];
        }
        catch { }
    }

    private void CheckPermissionButton()
    {
        try
        {
#if ANDROID
            bool hasAll = false;
            try { hasAll = Android.OS.Environment.IsExternalStorageManager; } catch { hasAll = false; }
            PermissionBtn.IsVisible = !hasAll;
#else
            PermissionBtn.IsVisible = false;
#endif
        }
        catch { PermissionBtn.IsVisible = false; }
    }

    // ESTE ES EL ARREGLADO - ANTES DABA CRASH AQUÍ
    private async void OnPermissionClicked(object sender, EventArgs e)
    {
        try
        {
#if ANDROID
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    var activity = Platform.CurrentActivity;
                    if (activity != null)
                    {
                        var intent = new Android.Content.Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                        intent.SetData(Android.Net.Uri.Parse("package:" + activity.PackageName));
                        activity.StartActivity(intent);
                    }
                    else
                    {
                        // Si no hay activity, abre ajustes normal, no da crash
                        AppInfo.ShowSettingsUI();
                    }
                }
                catch
                {
                    // Si falla el intent especial, abre ajustes normal
                    try { AppInfo.ShowSettingsUI(); } catch { }
                }
            });
            await DisplayAlert("Permiso", "Activa 'Permitir acceso a todos los archivos' y vuelve atrás a la app.", "OK");
#endif
        }
        catch { }
        await Task.Delay(500);
        CheckPermissionButton();
        await LoadFolder(currentPath);
    }

    private async void OnChangeLangClicked(object sender, EventArgs e)
    {
        langAsked = false;
        await AskLanguage();
        ApplyLanguage();
        await LoadFolder(currentPath);
    }

    private async Task LoadFolder(string path)
    {
        try
        {
            try { await Permissions.RequestAsync<Permissions.StorageRead>(); } catch { }
            try { await Permissions.RequestAsync<Permissions.Media>(); } catch { }

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

            string[] dirs = Array.Empty<string>();
            string[] files = Array.Empty<string>();
            try { dirs = Directory.GetDirectories(path); } catch { }
            try { files = Directory.GetFiles(path); } catch { }

            if (dirs.Length == 0 && files.Length == 0)
            {
#if ANDROID
                try
                {
                    var jFile = new Java.IO.File(path);
                    var list = jFile.ListFiles();
                    if (list != null)
                    {
                        var dList = new List<string>();
                        var fList = new List<string>();
                        foreach (var f in list)
                        {
                            try { if (f.IsDirectory) dList.Add(f.AbsolutePath); else fList.Add(f.AbsolutePath); } catch { }
                        }
                        dirs = dList.ToArray();
                        files = fList.ToArray();
                    }
                }
                catch { }
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
                        try { count = new Java.IO.File(dir).ListFiles()?.Length ?? 0; } catch { }
#endif
                    }
                    allItems.Add(new FileItem { Name = name, FullPath = dir, Icon = "📁", Info = $"{d["folder"]} • {count} {d["items"]}", IsFolder = true });
                }
                catch { }
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
                catch { }
            }

            RefreshList("");
            StatusLabel.Text = $"{allItems.Count} {d["items"]}";
            CheckPermissionButton();
        }
        catch { }
    }

    private void RefreshList(string search)
    {
        try
        {
            visibleItems.Clear();
            foreach (var item in allItems)
            {
                if (string.IsNullOrWhiteSpace(search) || item.Name.ToLower().Contains(search.ToLower()))
                    visibleItems.Add(item);
            }
        }
        catch { }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RefreshList(e.NewTextValue ?? "");

    private async void OnFileSelected(object sender, SelectionChangedEventArgs e)
    {
        try
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
                catch { }
            }
        }
        catch { }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        try
        {
            var parent = Directory.GetParent(currentPath);
            if (parent != null) await LoadFolder(parent.FullName);
        }
        catch { }
    }
    private async void OnHomeClicked(object sender, EventArgs e) => await LoadFolder("/storage/emulated/0");
    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadFolder(currentPath);
}
