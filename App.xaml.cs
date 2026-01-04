using InteractiveVision;
using System.Configuration;
using System.Data;
using System.Windows;

namespace TinyVolumeAdjuster
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
           
            HandleBug.Go(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dump"));
        }
    }

}
