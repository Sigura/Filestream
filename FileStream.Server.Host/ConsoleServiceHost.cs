using System;
using System.Collections;
using System.Configuration.Install;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using Microsoft.Practices.EnterpriseLibrary.Logging;
using FileStream.Common;
using StreamExtensions = FileStream.Common.StreamExtensions;

namespace FileStream.Server.Host
{
    class ConsoleServiceHost
    {
        private static bool IsExists()
        {
            return ServiceController.GetServices().Any(s => s.ServiceName == ServiceInstaller.Name);
        }

        public static void Remove()
        {
            Stop();

            new[]
            {
                new Tuple<string, string>(@"sc",
                    string.Format(@"delete {0}", ServiceInstaller.Name)),
            }.ForEach(Runner);
        }

        private static void Install(bool undo, string[] args = null)
        {
            try
            {
                Logger.Write(undo ? @"uninstalling" : @"installing");

                using (var inst = new AssemblyInstaller(typeof(Program).Assembly, args ?? new string[0]))
                {
                    IDictionary state = new Hashtable();
                    inst.UseNewContext = true;
                    try
                    {
                        if (undo)
                        {
                            Remove();

                            inst.Uninstall(state);
                        }
                        else
                        {
                            inst.Install(state);
                            inst.Commit(state);
                        }
                    }
                    catch
                    {
                        try
                        {
                            inst.Rollback(state);
                        }
                        catch (Exception e)
                        {
                            Logger.Write(string.Format(@"Rollback failed with error: {0}", e));
                        }
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write(string.Format(@"Install failed with error {0}", ex));
            }
        }

        private static void Start()
        {
            using (var serviceController = new ServiceController(ServiceInstaller.Name))
            {
                var status = serviceController.Status;

                if (status == ServiceControllerStatus.Stopped)
                {
                    serviceController.Start();
                    Logger.Write(@"Running.");
                }
                else
                {
                    Logger.Write(string.Format(@"Cannot start. It's {0}", status));
                }
            }
        }

        private static void Stop()
        {
            Logger.Write(@"Stopping before update");

            //_server.StopServer();

            StreamExtensions.ForEach(new[]
            {
                new Tuple<string, string>(@"net",
                    string.Format(
                        @"stop {0}",
                        ServiceInstaller.Name)),
            }, (Action<Tuple<string, string>>)Runner);

            //StopServiceAndWaitForExit(ServiceInstaller.Name);            
        }

        private static void Runner(Tuple<string, string> t)
        {
            Logger.Write(
                string.Format(
                    @"{0} {1}",
                    t.Item1, t.Item2));

            var regeditProcess = Process.Start(t.Item1, t.Item2);

            if (regeditProcess == null)
            {
                Logger.Write(@"command cannot start process");
                return;
            }

            regeditProcess.OutputDataReceived += (sender, args) =>
            {
                var output = args.Data;
                if (!string.IsNullOrEmpty(output))
                    return;

                Logger.Write(output);
            };
            regeditProcess.WaitForExit();
        }

        public static void Insatll()
        {
            if (IsExists())
            {
                Install(true);
            }

            Install(false);

            SetProperties();
        }

        private static void SetProperties()
        {
            new[]
            {
                new Tuple<string, string>(@"sc",
                    string.Format(
                        @"sdset {0} D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWRPWPDTLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)S:(AU;FA;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;WD)",
                        ServiceInstaller.Name)),
                new Tuple<string, string>(@"sc",
                    string.Format(
                        @"failure {0} reset= 20 actions= restart/40000/restart/40000/restart/40000",
                        ServiceInstaller.Name)),
                new Tuple<string, string>(@"sc", string.Format(@"config {0} start= auto", ServiceInstaller.Name))
            }.ForEach(t =>
            {
                Logger.Write(
                    string.Format(
                        @"{0} {1}",
                        t.Item1, t.Item2));

                var regeditProcess = Process.Start(t.Item1, t.Item2);

                if (regeditProcess == null)
                {
                    Logger.Write(@"command cannot start process");
                    return;
                }

                regeditProcess.OutputDataReceived += (sender, args) =>
                {
                    var output = args.Data;
                    if (!string.IsNullOrEmpty(output))
                        return;

                    Logger.Write(output);
                };
                regeditProcess.WaitForExit();
            });
        }
    }
}