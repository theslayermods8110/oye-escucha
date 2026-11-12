using System.Collections.ObjectModel;
using System.IO.Compression;
using MauiShapes = Microsoft.Maui.Controls.Shapes;

namespace OyeEscucha;

public class FileItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string BadgeText { get; set; } = "FILE";
    public string BadgeColor { get; set; } = "#607D8B";
    public MauiShapes.Geometry? IconGeometry { get; set; }
    public string Info { get; set; } = "";
    public bool IsFolder { get; set; }
    public ImageSource? ApkIcon { get; set; }
    public bool HasApkIcon { get; set; } = false;
    public bool ShowDefaultIcon => !HasApkIcon;

    public static MauiShapes.Geometry GetIcon(string key)
    {
        string path = key switch
        {
            "DIR" => "M2 5 C2 4 3 3 4 3 L10 3 L12 5 L20 5 C21 5 22 6 22 7 L22 18 C22 19 21 20 20 20 L4 20 C3 20 2 19 2 18 Z",
            "IMG" => "M2 4 L22 4 L22 20 L2 20 Z M7 9 A2 2 0 1 1 7 9.01 Z M2 18 L8 11 L13 16 L16 13 L22 19 L22 20 L2 20 Z",
            "VID" => "M2 5 L22 5 L22 19 L2 19 Z M9 8 L17 12 L9 16 Z",
            "MP3" => "M9 18 L9 6 L18 4 L18 16 M9 18 A2 2 0 1 1 9 18.01 M18 16 A2 2 0 1 1 18 16.01",
            "PDF" => "M6 2 L14 2 L20 8 L20 22 L6 22 Z M6 2 L14 2 L14 8 L20 8 M9 13 L15 13 M9 16 L15 16",
            "DOC" => "M6 2 L14 2 L20 8 L20 22 L6 22 Z M9 12 L17 12 M9 15 L17 15 M9 18 L14 18",
            "XLS" => "M4 2 L20 2 L20 22 L4 22 Z M4 8 L20 8 M4 15 L20 15 M12 2 L12 22 M8 11 L10 13 L8 15 M16 11 L14 13 L16 15",
            "APK" => "M12 2 L20 7 L20 17 L12 22 L4 17 L4 7 Z M12 2 L12 12 M12 12 L20 7 M12 12 L4 7",
            "ZIP" => "M4 3 L20 3 L20 21 L4 21 Z M12 7 L12 17 M9 10 L12 7 L15 10",
            "TXT" => "M6 2 L18 2 L18 22 L6 22 Z M9 7 L15 7 M9 11 L15 11 M9 15 L13 15 M9 19 L13 19",
            _ => "M6 2 L14 2 L20 8 L20 22 L6 22 Z M6 2 L14 2 L14 8 L20 8",
        };
        try { return (MauiShapes.Geometry)new MauiShapes.PathGeometryConverter().ConvertFromInvariantString(path)!; }
        catch { return (MauiShapes.Geometry)new MauiShapes.PathGeometryConverter().ConvertFromInvariantString("M6 2 L14 2 L20 8 L20 22 L6 22 Z")!; }
    }
}

public partial class MainPage : ContentPage
{
    private string currentPath = "/storage/emulated/0";
    private List<FileItem> allItems = new();
    private ObservableCollection<FileItem> visibleItems = new();
    private string lang = "es";
    private bool langAsked = false;
    private bool isNavigating = false;

    private Dictionary<string, Dictionary<string,string>> T = new()
    {
        ["es"] = new() { ["title"]="Mis Archivos", ["search"]="Buscar archivos...", ["back"]="Atras", ["home"]="Inicio", ["refresh"]="Recargar", ["lang"]="Idioma", ["empty"]="Carpeta vacia o sin permiso.", ["loading"]="Cargando...", ["folder"]="Carpeta", ["file"]="Archivo", ["items"]="elementos", ["perm"]="Dar permiso total para ver dentro de carpetas" },
        ["en"] = new() { ["title"]="My Files", ["search"]="Search files...", ["back"]="Back", ["home"]="Home", ["refresh"]="Reload", ["lang"]="Language", ["empty"]="Empty folder or no permission.", ["loading"]="Loading...", ["folder"]="Folder", ["file"]="File", ["items"]="items", ["perm"]="Give full permission to see inside folders" },
        ["fr"] = new() { ["title"]="Mes Fichiers", ["search"]="Rechercher...", ["back"]="Retour", ["home"]="Accueil", ["refresh"]="Recharger", ["lang"]="Langue", ["empty"]="Dossier vide ou sans permission.", ["loading"]="Chargement...", ["folder"]="Dossier", ["file"]="Fichier", ["items"]="elements", ["perm"]="Donner permission totale" },
        ["ht"] = new() { ["title"]="Fichye M yo", ["search"]="Cheche fichye...", ["back"]="Tounen", ["home"]="Akey", ["refresh"]="Rechaje", ["lang"]="Lang", ["empty"]="Dosye vid oswa san pemisyon.", ["loading"]="Ap chaje...", ["folder"]="Dosye", ["file"]="Fichye", ["items"]="eleman", ["perm"]="Bay pemisyon total" },
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

    private async void OnPathTapped(object sender, EventArgs e)
    {
        try
        {
            await Clipboard.SetTextAsync(currentPath);
            await DisplayAlertAsync("Copiado", currentPath, "OK");
        }
        catch { }
    }

    private async void OnCreateFolderClicked(object sender, EventArgs e)
    {
        try
        {
            string? name = await DisplayPromptAsync("Crear Carpeta", "Nombre de la carpeta:", "Crear", "Cancelar", "Mi Carpeta");
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            string newPath = Path.Combine(currentPath, name);
            if (Directory.Exists(newPath))
            {
                await DisplayAlertAsync("Aviso", "Esa carpeta ya existe.", "OK");
                return;
            }
            Directory.CreateDirectory(newPath);
            await LoadFolder(currentPath);
        }
        catch (Exception ex) { await DisplayAlertAsync("Error", "No se pudo crear: " + ex.Message, "OK"); }
    }

    private async void OnCreateFileClicked(object sender, EventArgs e)
    {
        try
        {
            string? name = await DisplayPromptAsync("Crear Archivo", "Nombre del archivo (ej: nota.txt):", "Crear", "Cancelar", "nuevo.txt");
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            if (string.IsNullOrEmpty(Path.GetExtension(name))) name += ".txt";
            string newPath = Path.Combine(currentPath, name);
            if (File.Exists(newPath))
            {
                await DisplayAlertAsync("Aviso", "Ese archivo ya existe.", "OK");
                return;
            }
            File.WriteAllText(newPath, "");
            string ext = Path.GetExtension(newPath).ToLower();
            if (ext is ".txt" or ".json" or ".kt" or ".cs" or ".xml" or ".md" or ".log" or ".html" or ".css" or ".js" or ".py" or ".java")
            {
                await Navigation.PushAsync(new EditorPage(newPath));
            }
            await LoadFolder(currentPath);
        }
        catch (Exception ex) { await DisplayAlertAsync("Error", "No se pudo crear: " + ex.Message, "OK"); }
    }

    private async Task AskLanguage()
    {
        try
        {
            var page = new LanguagePage();
            await Navigation.PushModalAsync(page);
            string res = await page.ResultTask;
            if (res == "en") lang = "en";
            else if (res == "fr") lang = "fr";
            else if (res == "ht") lang = "ht";
            else lang = "es";
            Preferences.Set("idioma", lang);
            ApplyLanguage();
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
            try { if (OperatingSystem.IsAndroidVersionAtLeast(30)) hasAll = Android.OS.Environment.IsExternalStorageManager; } catch { hasAll = false; }
            PermissionBtn.IsVisible = !hasAll;
#else
            PermissionBtn.IsVisible = false;
#endif
        }
        catch { PermissionBtn.IsVisible = false; }
    }

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
                    else { AppInfo.ShowSettingsUI(); }
                }
                catch { try { AppInfo.ShowSettingsUI(); } catch { } }
            });
            await DisplayAlertAsync("Permiso", "Activa 'Permitir acceso a todos los archivos' y vuelve atras.", "OK");
#endif
        }
        catch { }
        await Task.Delay(500);
        CheckPermissionButton();
        await LoadFolder(currentPath);
    }

    private async void OnChangeLangClicked(object sender, EventArgs e)
    {
        await AskLanguage();
        await LoadFolder(currentPath);
    }

    private (ImageSource? icon, bool has) GetApkIcon(string apkPath)
    {
        try
        {
#if ANDROID
            var activity = Platform.CurrentActivity;
            var pm = activity!.PackageManager;
            var info = pm!.GetPackageArchiveInfo(apkPath, 0);
            if (info?.ApplicationInfo == null) return (null, false);
            info.ApplicationInfo.SourceDir = apkPath;
            info.ApplicationInfo.PublicSourceDir = apkPath;
            var drawable = info.ApplicationInfo.LoadIcon(pm);
            if (drawable is Android.Graphics.Drawables.BitmapDrawable bd && bd.Bitmap != null)
            {
                var stream = new MemoryStream();
                bd.Bitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Png, 100, stream);
                stream.Position = 0;
                byte[] bytes = stream.ToArray();
                return (ImageSource.FromStream(() => new MemoryStream(bytes)), true);
            }
#endif
        }
        catch { }
        return (null, false);
    }

    private async Task LoadFolder(string path)
    {
        if (isNavigating) return;
        isNavigating = true;
        try
        {
            try { await Permissions.RequestAsync<Permissions.StorageRead>(); } catch { }
            currentPath = path;
            PathLabel.Text = path;
            var d = T.ContainsKey(lang) ? T[lang] : T["es"];
            StatusLabel.Text = d["loading"];
            allItems.Clear();
            visibleItems.Clear();
            FilesList.SelectedItem = null;
            if (!Directory.Exists(path)) { StatusLabel.Text = d["empty"]; return; }
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
                        var dList = new List<string>(); var fList = new List<string>();
                        foreach (var f in list) { try { if (f.IsDirectory) dList.Add(f.AbsolutePath); else fList.Add(f.AbsolutePath); } catch { } }
                        dirs = dList.ToArray(); files = fList.ToArray();
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
                    allItems.Add(new FileItem { Name = name, FullPath = dir, BadgeText = "DIR", BadgeColor = "#FFB300", IconGeometry = FileItem.GetIcon("DIR"), Info = $"{d["folder"]} - {count} {d["items"]}", IsFolder = true });
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
                    string badge = "FILE"; string color = "#607D8B";
                    string ext = Path.GetExtension(name).ToLower();
                    ImageSource? apkIcon = null; bool hasApk = false;
                    if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif") { badge = "IMG"; color = "#E91E63"; }
                    else if (ext is ".mp4" or ".mkv" or ".mov" or ".avi") { badge = "VID"; color = "#9C27B0"; }
                    else if (ext is ".mp3" or ".wav" or ".m4a" or ".ogg") { badge = "MP3"; color = "#00BCD4"; }
                    else if (ext is ".pdf") { badge = "PDF"; color = "#F44336"; }
                    else if (ext is ".doc" or ".docx") { badge = "DOC"; color = "#2196F3"; }
                    else if (ext is ".xlsx" or ".xls") { badge = "XLS"; color = "#4CAF50"; }
                    else if (ext is ".apk") { badge = "APK"; color = "#FF9800"; var r = GetApkIcon(file); apkIcon = r.icon; hasApk = r.has; if (hasApk) color = "Transparent"; }
                    else if (ext is ".zip" or ".rar" or ".7z") { badge = "ZIP"; color = "#795548"; }
                    else if (ext is ".txt" or ".json" or ".kt" or ".cs" or ".xml" or ".md" or ".log" or ".html" or ".css" or ".js" or ".py" or ".java") { badge = "TXT"; color = "#00ACC1"; }
                    string size = info.Length < 1024 ? $"{info.Length} B" : info.Length < 1024*1024 ? $"{info.Length/1024.0:F1} KB" : $"{info.Length/1024.0/1024.0:F1} MB";
                    allItems.Add(new FileItem { Name = name, FullPath = file, BadgeText = badge, BadgeColor = color, IconGeometry = FileItem.GetIcon(badge), Info = $"{d["file"]} - {size}", IsFolder = false, ApkIcon = apkIcon, HasApkIcon = hasApk });
                }
                catch { }
            }
            RefreshList("");
            StatusLabel.Text = $"{allItems.Count} {d["items"]}";
            CheckPermissionButton();
            await MainThread.InvokeOnMainThreadAsync(() => FilesList.ScrollTo(0, position: ScrollToPosition.Start, animate: false));
        }
        catch { }
        finally { isNavigating = false; }
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
            ((CollectionView)sender!).SelectedItem = null;
            if (isNavigating) return;
            if (item.IsFolder) { SearchEntry.Text = ""; await LoadFolder(item.FullPath); return; }
            string ext = Path.GetExtension(item.FullPath).ToLower();
            if (ext is ".txt" or ".json" or ".kt" or ".cs" or ".xml" or ".md" or ".log" or ".html" or ".css" or ".js" or ".py" or ".java" or ".kts" or ".gradle" or ".yml" or ".yaml" or ".ini" or ".csv")
            {
                await Navigation.PushAsync(new EditorPage(item.FullPath));
                return;
            }
            if (ext is ".zip") { await OpenZip(item.FullPath); return; }
            if (ext == ".apk") { await InstallApk(item.FullPath); return; }
            try { await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(item.FullPath) }); }
            catch { await DisplayAlertAsync("Error", "No se pudo abrir este archivo.", "OK"); }
        }
        catch { }
    }

    private async Task OpenZip(string zipPath)
    {
        try
        {
            string folderName = Path.GetFileNameWithoutExtension(zipPath) + "_descomprimido";
            string dest = Path.Combine(Path.GetDirectoryName(zipPath) ?? currentPath, folderName);
            if (!Directory.Exists(dest))
            {
                StatusLabel.Text = "Descomprimiendo...";
                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, dest, true));
            }
            SearchEntry.Text = "";
            await LoadFolder(dest);
        }
        catch (Exception ex) { await DisplayAlertAsync("ZIP", "No se pudo abrir: " + ex.Message, "OK"); }
    }

    private async Task InstallApk(string apkPath)
    {
        try
        {
#if ANDROID
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    var activity = Platform.CurrentActivity;
                    var file = new Java.IO.File(apkPath);
                    var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(activity!, activity!.PackageName + ".provider", file);
                    var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
                    intent.SetDataAndType(uri, "application/vnd.android.package-archive");
                    intent.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission);
                    intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                    activity.StartActivity(intent);
                }
                catch { }
            });
#else
            await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(apkPath) });
#endif
        }
        catch { await DisplayAlertAsync("APK", "No se pudo instalar. Dale permiso de 'Instalar apps desconocidas' a esta app.", "OK"); }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        try { var parent = Directory.GetParent(currentPath); if (parent != null) await LoadFolder(parent.FullName); } catch { }
    }
    private async void OnHomeClicked(object sender, EventArgs e) => await LoadFolder("/storage/emulated/0");
    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadFolder(currentPath);
}
