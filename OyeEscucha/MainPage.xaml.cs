using System.Collections.ObjectModel;
using System.IO.Compression;

namespace OyeEscucha;

public class FileItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string Info { get; set; } = "";
    public bool IsFolder { get; set; }
    // ICONOS POR LINK - Icons8 te presta la imagen, no hay que subir PNG
    public string IconImage { get; set; } = "https://img.icons8.com/3d-fluency/94/file.png";
    public ImageSource? ApkIcon { get; set; }
    public bool HasApkIcon { get; set; } = false;
    public bool ShowDefaultIcon => !HasApkIcon;

    public static string GetIconImage(string ext, bool isFolder)
    {
        if (isFolder) return "https://img.icons8.com/3d-fluency/94/folder-invoices.png";
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" => "https://img.icons8.com/3d-fluency/94/image.png",
            ".mp4" or ".mkv" or ".mov" or ".avi" => "https://img.icons8.com/3d-fluency/94/video.png",
            ".mp3" or ".wav" or ".m4a" or ".ogg" => "https://img.icons8.com/3d-fluency/94/music.png",
            ".pdf" => "https://img.icons8.com/3d-fluency/94/pdf.png",
            ".doc" or ".docx" => "https://img.icons8.com/3d-fluency/94/document.png",
            ".xlsx" or ".xls" => "https://img.icons8.com/3d-fluency/94/spreadsheet.png",
            ".apk" => "https://img.icons8.com/3d-fluency/94/android.png",
            ".zip" or ".rar" or ".7z" => "https://img.icons8.com/3d-fluency/94/zip.png",
            ".txt" or ".json" or ".kt" or ".cs" or ".xml" or ".md" or ".log" or ".html" or ".css" or ".js" or ".py" or ".java" => "https://img.icons8.com/3d-fluency/94/text.png",
            _ => "https://img.icons8.com/3d-fluency/94/file.png",
        };
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

    // PRIVACIDAD + PLUGINS
    private bool mostrarOcultos = false;
    private bool mostrarTamano = true;
    private bool buscarActivo = true;
    private bool pluginsActivo = true;
    private TaskCompletionSource<string>? dialogTcs;

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
        try { mostrarOcultos = Preferences.Get("mostrarOcultos", false); } catch {}
        try { mostrarTamano = Preferences.Get("mostrarTamano", true); } catch {}
        try { buscarActivo = Preferences.Get("buscarActivo", true); } catch {}
        try { pluginsActivo = Preferences.Get("pluginsActivo", true); } catch {}
        ApplyTheme();
    }

    // ===== TEMA SOL / LUNA EN VECTOR - LOS DE ANTES =====
    private void OnThemeClicked(object sender, TappedEventArgs e)
    {
        try
        {
            string actual = Preferences.Get("tema", "dark");
            string nuevo = actual == "light" ? "dark" : "light";
            Preferences.Set("tema", nuevo);
            ApplyTheme();
        }
        catch { }
    }
    private void ApplyTheme()
    {
        try
        {
            string tema = Preferences.Get("tema", "dark");
            bool esLight = tema == "light";
            if (Application.Current != null)
                Application.Current.UserAppTheme = esLight ? AppTheme.Light : AppTheme.Dark;
            if (SunIcon != null) SunIcon.IsVisible = esLight;
            if (MoonIcon != null) MoonIcon.IsVisible = !esLight;
        }
        catch { }
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

    // ===== MENU ••• - SIN EMOJI =====
    private async void OnMenuClicked(object sender, EventArgs e)
    {
        string? res = await DisplayActionSheet("Que quieres hacer?", "Cancelar", null,
            "Buscar", "Crear Carpeta", "Crear Archivo", "Plugins", "Privacidad");
        if (res == "Buscar")
        {
            if (!buscarActivo) { await MostrarAlertaModerna("Buscar desactivado", "Activalo en Privacidad"); return; }
            SearchBox.IsVisible = true; SearchEntry.Focus();
        }
        else if (res == "Crear Carpeta") OnCreateFolderClicked(sender, e);
        else if (res == "Crear Archivo") OnCreateFileClicked(sender, e);
        else if (res == "Plugins") MostrarPlugins();
        else if (res == "Privacidad") MostrarPrivacidad();
    }
    private void OnCloseSearchClicked(object sender, EventArgs e)
    {
        SearchBox.IsVisible = false; SearchEntry.Text = "";
    }

    // ===== ALERTA MODERNA - IGUAL QUE IDIOMA Y GUARDADO =====
    private Task<string> MostrarDialogoModerno(string titulo, string mensaje, string placeholder, bool conInput, bool mostrarOk = true)
    {
        dialogTcs = new TaskCompletionSource<string>();
        DialogTitle.Text = titulo; DialogMessage.Text = mensaje;
        DialogEntry.IsVisible = conInput; DialogEntry.Text = ""; DialogEntry.Placeholder = placeholder;
        DialogOptions.Clear(); DialogOkBtn.IsVisible = mostrarOk;
        ModernDialog.IsVisible = true;
        return dialogTcs.Task;
    }
    private void OnDialogCancel(object sender, EventArgs e)
    {
        ModernDialog.IsVisible = false; dialogTcs?.TrySetResult("");
    }
    private void OnDialogOk(object sender, EventArgs e)
    {
        ModernDialog.IsVisible = false; dialogTcs?.TrySetResult(DialogEntry.Text ?? "");
    }
    private async Task MostrarAlertaModerna(string titulo, string mensaje)
    {
        await MostrarDialogoModerno(titulo, mensaje, "", false);
    }

    // ===== PLUGINS - SIN EMOJI =====
    private async void MostrarPlugins()
    {
        if (!pluginsActivo) { await MostrarAlertaModerna("Plugins desactivados", "Activalos en Privacidad"); return; }
        string? res = await DisplayActionSheet("Plugins", "Cerrar", null,
            "Visor de Imagenes", "Reproductor de Musica", "Editor de Texto", "Comprimir ZIP");
        if (res != null && res != "Cerrar") await MostrarAlertaModerna("Guardado", $"Plugin activado: {res}");
    }

    // ===== PRIVACIDAD - ACTIVAR / DESACTIVAR =====
    private async void MostrarPrivacidad()
    {
        string? res = await DisplayActionSheet("Privacidad - Toca para activar / desactivar", "Cerrar", null,
            $"Mostrar ocultos: {(mostrarOcultos ? "ON" : "OFF")}",
            $"Mostrar tamano: {(mostrarTamano ? "ON" : "OFF")}",
            $"Buscar: {(buscarActivo ? "ON" : "OFF")}",
            $"Plugins: {(pluginsActivo ? "ON" : "OFF")}");
        if (res == null || res == "Cerrar") return;
        if (res.Contains("ocultos")) { mostrarOcultos = !mostrarOcultos; Preferences.Set("mostrarOcultos", mostrarOcultos); }
        if (res.Contains("tamano")) { mostrarTamano = !mostrarTamano; Preferences.Set("mostrarTamano", mostrarTamano); }
        if (res.Contains("Buscar")) { buscarActivo = !buscarActivo; Preferences.Set("buscarActivo", buscarActivo); }
        if (res.Contains("Plugins")) { pluginsActivo = !pluginsActivo; Preferences.Set("pluginsActivo", pluginsActivo); }
        await LoadFolder(currentPath);
        MostrarPrivacidad();
    }

    private async void OnPathTapped(object sender, TappedEventArgs e)
    {
        try { await Clipboard.SetTextAsync(currentPath); await MostrarAlertaModerna("Copiado", currentPath); } catch { }
    }

    private async void OnCreateFolderClicked(object sender, EventArgs e)
    {
        try
        {
            string name = await MostrarDialogoModerno("Crear Carpeta", "Nombre de la carpeta:", "Mi Carpeta", true);
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            string newPath = Path.Combine(currentPath, name);
            if (Directory.Exists(newPath)) { await MostrarAlertaModerna("Aviso", "Esa carpeta ya existe."); return; }
            Directory.CreateDirectory(newPath);
            await LoadFolder(currentPath);
            await MostrarAlertaModerna("Guardado", $"Carpeta \"{name}\" creada con exito");
        }
        catch (Exception ex) { await MostrarAlertaModerna("Error", "No se pudo crear: " + ex.Message); }
    }

    private async void OnCreateFileClicked(object sender, EventArgs e)
    {
        try
        {
            string name = await MostrarDialogoModerno("Crear Archivo", "Nombre del archivo (ej: nota.txt):", "nuevo.txt", true);
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            if (string.IsNullOrEmpty(Path.GetExtension(name))) name += ".txt";
            string newPath = Path.Combine(currentPath, name);
            if (File.Exists(newPath)) { await MostrarAlertaModerna("Aviso", "Ese archivo ya existe."); return; }
            File.WriteAllText(newPath, "");
            string ext = Path.GetExtension(newPath).ToLower();
            if (ext is ".txt" or ".json" or ".kt" or ".cs" or ".xml" or ".md" or ".log" or ".html" or ".css" or ".js" or ".py" or ".java")
                await Navigation.PushAsync(new EditorPage(newPath));
            await LoadFolder(currentPath);
            await MostrarAlertaModerna("Guardado", $"Archivo \"{name}\" creado con exito");
        }
        catch (Exception ex) { await MostrarAlertaModerna("Error", "No se pudo crear: " + ex.Message); }
    }

    private async Task AskLanguage()
    {
        try
        {
            var page = new LanguagePage();
            await Navigation.PushModalAsync(page);
            string res = await page.ResultTask;
            if (res == "en") lang = "en"; else if (res == "fr") lang = "fr"; else if (res == "ht") lang = "ht"; else lang = "es";
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
            await MostrarAlertaModerna("Permiso", "Activa 'Permitir acceso a todos los archivos' y vuelve atras.");
#endif
        }
        catch { }
        await Task.Delay(500);
        CheckPermissionButton();
        await LoadFolder(currentPath);
    }

    private async void OnChangeLangClicked(object sender, EventArgs e) { await AskLanguage(); await LoadFolder(currentPath); }

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
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!mostrarOcultos && name.StartsWith(".")) continue;
                    int count = 0;
                    try { count = Directory.GetFileSystemEntries(dir).Length; } catch {
#if ANDROID
                        try { count = new Java.IO.File(dir).ListFiles()?.Length ?? 0; } catch { }
#endif
                    }
                    allItems.Add(new FileItem { Name = name, FullPath = dir, IconImage = FileItem.GetIconImage("", true), Info = $"{d["folder"]} - {count} {d["items"]}", IsFolder = true });
                }
                catch { }
            }
            foreach (var file in files)
            {
                try
                {
                    var name = Path.GetFileName(file);
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!mostrarOcultos && name.StartsWith(".")) continue;
                    var info = new FileInfo(file);
                    string ext = Path.GetExtension(name).ToLower();
                    ImageSource? apkIcon = null; bool hasApk = false;
                    if (ext == ".apk") { var r = GetApkIcon(file); apkIcon = r.icon; hasApk = r.has; }
                    string size = "";
                    if (mostrarTamano)
                        size = info.Length < 1024 ? $"{info.Length} B" : info.Length < 1024*1024 ? $"{info.Length/1024.0:F1} KB" : $"{info.Length/1024.0/1024.0:F1} MB";
                    string infoText = mostrarTamano ? $"{d["file"]} - {size}" : d["file"];
                    allItems.Add(new FileItem { Name = name, FullPath = file, IconImage = FileItem.GetIconImage(ext, false), Info = infoText, IsFolder = false, ApkIcon = apkIcon, HasApkIcon = hasApk });
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
            { await Navigation.PushAsync(new EditorPage(item.FullPath)); return; }
            if (ext is ".zip") { await OpenZip(item.FullPath); return; }
            if (ext == ".apk") { await InstallApk(item.FullPath); return; }
            try { await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(item.FullPath) }); }
            catch { await MostrarAlertaModerna("Error", "No se pudo abrir este archivo."); }
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
        catch (Exception ex) { await MostrarAlertaModerna("ZIP", "No se pudo abrir: " + ex.Message); }
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
        catch { await MostrarAlertaModerna("APK", "No se pudo instalar. Dale permiso de 'Instalar apps desconocidas' a esta app."); }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    { try { var parent = Directory.GetParent(currentPath); if (parent != null) await LoadFolder(parent.FullName); } catch { } }
    private async void OnHomeClicked(object sender, EventArgs e) => await LoadFolder("/storage/emulated/0");
    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadFolder(currentPath);
}
