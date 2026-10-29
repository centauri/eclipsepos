using System.Windows;
namespace EclipsePOS.WPF.SystemManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            EclipsePOS.Platform.StartupSupport.Run(this, e, () => new Shell(), () => new Bootstrapper().Run());
        }
    }
}
