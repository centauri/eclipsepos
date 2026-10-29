using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EclipsePOS.Platform
{
    internal static class StartupSupport
    {
        internal static void Run(Application app, StartupEventArgs args, Func<Window> shell, Action bootstrap)
        {
            bool preview = args.Args.Contains("--preview") || args.Args.Contains("--smoke");
            ModernTheme.Apply(args.Args.Contains("--dark"));
            app.DispatcherUnhandledException += (sender, e) =>
            {
                e.Handled = true;
                Failure(e.Exception, args.Args.Contains("--smoke"));
                app.Shutdown(1);
            };
            try
            {
                if (!preview) { bootstrap(); return; }
                var previewBootstrapper = new PreviewBootstrapper(shell);
                previewBootstrapper.Run();
                var window = previewBootstrapper.Window;
                window.Title += " — layout preview (no database or devices)";
                window.WindowState = WindowState.Normal;
                window.Width = 1440; window.Height = 900;
                window.Show();
                if (args.Args.Contains("--smoke"))
                {
                    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        window.UpdateLayout();
                        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using (var file = File.Create("shell-" + (ModernTheme.Dark ? "dark" : "light") + ".png")) encoder.Save(file);
                        File.WriteAllText("shell-smoke.txt", "PASS: original WPF shell constructed and rendered. Database/device workflows not exercised.");
                        app.Shutdown(0);
                    }));
                }
            }
            catch (Exception ex) { Failure(ex, args.Args.Contains("--smoke")); app.Shutdown(1); }
        }
        private sealed class PreviewBootstrapper : Microsoft.Practices.Composite.UnityExtensions.UnityBootstrapper
        {
            private readonly Func<Window> factory;
            internal Window Window;
            internal PreviewBootstrapper(Func<Window> factory) { this.factory = factory; }
            protected override DependencyObject CreateShell() { Window = factory(); return Window; }
            protected override Microsoft.Practices.Composite.Modularity.IModuleCatalog GetModuleCatalog()
            { return new Microsoft.Practices.Composite.Modularity.ModuleCatalog(); }
        }
        private static void Failure(Exception error, bool silent)
        {
            if (silent) File.WriteAllText("shell-error.txt", error.ToString());
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EclipsePOS", "Logs");
            Directory.CreateDirectory(folder);
            // Do not serialize connection strings or SQL parameter values to logs.
            File.AppendAllText(Path.Combine(folder, "errors.log"), DateTime.UtcNow.ToString("o") + " " + error.GetType().FullName + Environment.NewLine + error.StackTrace + Environment.NewLine);
            if (!silent) MessageBox.Show("EclipsePOS could not finish this operation and will close. Check database settings, schema upgrades and POS device installation before reopening. Do not retry a payment until its status is checked.\n\n" + error.GetType().Name + "\nDiagnostics: " + folder,
                "EclipsePOS — operation stopped", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

