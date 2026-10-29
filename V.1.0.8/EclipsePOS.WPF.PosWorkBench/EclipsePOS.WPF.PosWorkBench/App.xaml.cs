using System.Linq;
using System.Windows;
namespace EclipsePOS.WPF.PosWorkBench
{
    public partial class App : Application
    {
        internal static bool PreviewMode;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            PreviewMode = e.Args.Contains("--preview") || e.Args.Contains("--smoke");
            EclipsePOS.Platform.StartupSupport.Run(this, e, () => new PosWorkBenchShell(), () => new PosWorkBenchBootstrapper().Run());
        }
    }
}
