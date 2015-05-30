using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceProcess;
using Microsoft.Practices.EnterpriseLibrary.Logging;
using FileStream.Contracts;

namespace FileStream.Server.Host
{
    partial class FileStreamService : ServiceBase
    {
        private readonly IList<ServiceHost> _serviceHosts = new List<ServiceHost>();

        public FileStreamService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            var streamHost = new ServiceHost(typeof(Service));
            _serviceHosts.Add(streamHost);

            foreach (var host in _serviceHosts.ToList())
            {
                host.Open();
            }
        }

        private static void StopStreamServer()
        {
            using (var streamProxyFactory = new ChannelFactory<IStreamServer>(@"streamService"))
            {
                var channal = streamProxyFactory.CreateChannel();

                channal.Stop();
            }
        }

        protected override void OnStop()
        {
            StopStreamServer();

            if (_serviceHosts == null) return;

            foreach (var host in _serviceHosts.ToList())
            {
                StopServiceHost(host);
                _serviceHosts.Remove(host);
            }
        }

        private static void StopServiceHost(ServiceHostBase serviceHost)
        {
            var hostName = GetServiceHostName(serviceHost);

            try
            {
                serviceHost.Close();

                Logger.Write(new LogEntry
                {
                    Title = @"Service host is stopped",
                    Message = string.Format(@"Service host {0} is stopped {1}", hostName, serviceHost.GetType().Name),
                    Severity = TraceEventType.Information
                });
            }
            catch (Exception ex)
            {
                Logger.Write(new LogEntry
                {
                    Title = @"Service host is stopped",
                    Message = string.Format(@"Could not stop service host {0}: {1}", hostName, ex),
                    Severity = TraceEventType.Error
                });
            }
        }

        private static string GetServiceHostName(ServiceHostBase serviceHost)
        {
            var hostName = string.Empty;

            if (serviceHost != null && serviceHost.Description != null)
            {
                hostName = serviceHost.Description.Name;
            }

            return hostName;
        }

        public void Start()
        {
            OnStart(null);
        }
    }
}
