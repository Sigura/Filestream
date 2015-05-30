namespace FileStream.Server.Host
{
    partial class ServiceInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.FileStreamService1 = new FileStream.Server.Host.FileStreamService();
            this.serviceInstaller1 = new System.ServiceProcess.ServiceInstaller();
            // 
            // FileStreamService1
            // 
            this.FileStreamService1.ExitCode = 0;
            this.FileStreamService1.ServiceName = "FileStream.Service.Host";
            // 
            // serviceInstaller1
            // 
            this.serviceInstaller1.Description = "FileStream.Service.Host";
            this.serviceInstaller1.DisplayName = "FileStream.Service.Host";
            this.serviceInstaller1.ServiceName = "FileStream.Service.Host";
            // 
            // ServiceInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.serviceInstaller1});

        }

        #endregion

        private FileStreamService FileStreamService1;
        private System.ServiceProcess.ServiceInstaller serviceInstaller1;
    }
}