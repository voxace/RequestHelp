using System.ComponentModel;
using System.Windows;

namespace RequestHelpClient
{
    public partial class App : Application
    {        
		new ClientWindow MainWindow = new ClientWindow();		

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);			
        }        
    }
}
