using System.ComponentModel;

namespace FileStream.Server.Host
{
    [RunInstaller(true)]
    public partial class ServiceInstaller : System.Configuration.Install.Installer
    {
        public ServiceInstaller()
        {
            InitializeComponent();
        }

        public static string Name {
            get { return @"FileStream.Service.Host"; }
        }
    }
}
