using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace OyeEscucha;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // QUITA LO MORADO - Pone la barra de arriba y abajo negras
        if (Window != null)
        {
            Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#0F0F0F"));
            Window.SetNavigationBarColor(Android.Graphics.Color.ParseColor("#0F0F0F"));

            // Para que los iconos de hora / bateria se vean blancos sobre negro
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                var decor = Window.DecorView;
                // Quita el modo claro, deja iconos blancos
                decor.SystemUiVisibility &= ~(StatusBarVisibility.LightStatusBar);
            }
        }
    }
}
