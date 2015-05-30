using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using Microsoft.Practices.EnterpriseLibrary.Logging;

namespace FileStream.Server.Host
{
    class Program
    {
        static void Main()
        {
            var args = Environment.GetCommandLineArgs();

            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;

            var server = new FileStreamService();

            if (Environment.UserInteractive)
            {
                if (args.Contains(@"uninstall") || args.Contains(@"remove"))
                {
                    ConsoleServiceHost.Remove();
                }
                else if (args.Contains(@"install"))
                {
                    ConsoleServiceHost.Insatll();
                }
                else
                {

                    server.Start();

                    Console.ReadLine();

                    server.Stop();
                }
            }
            else
            {
                ServiceBase.Run(server);
            }
        }

        private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e != null && e.ExceptionObject != null)
            {
                Logger.Write(new LogEntry{Title = @"Unhandled exception occurred", Message = e.ExceptionObject.ToString(), Severity = TraceEventType.Critical});
            }
            else
            {
                Logger.Write(new LogEntry { Title = @"Unhandled exception occurred", Message = @"Unhandled exception occurred within the application, no exceptions were provided here", Severity = TraceEventType.Critical });
            }
        }
    }
}
