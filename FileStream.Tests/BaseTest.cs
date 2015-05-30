using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using Microsoft.Practices.EnterpriseLibrary.Logging;

namespace FileStream.Tests
{
    using Contracts;
    using Server;

    public abstract class BaseTest
    {
        private readonly IList<ServiceHost> _serviceHosts = new List<ServiceHost>();

        protected void StartHosts()
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

        protected void BaseTestCleanup()
        {
            if (_serviceHosts == null || _serviceHosts.Count <= 0) return;

            StopStreamServer();

            foreach (var host in _serviceHosts.ToList())
            {
                StopServiceHost(host);
                _serviceHosts.Remove(host);
            }
        }

        private static void StopServiceHost(ServiceHostBase serviceHost)
        {
            try
            {
                serviceHost.Close();

                Logger.Write(new LogEntry
                {
                    Title = @"Service host is stopped",
                    Message = string.Format(@"Service host {0} is stopped {1}", @"StreamService", serviceHost.GetType().Name),
                    Severity = TraceEventType.Information
                });
            }
            catch (Exception ex)
            {
                Logger.Write(new LogEntry
                {
                    Title = @"Service host is stopped",
                    Message = string.Format(@"Could not stop service host {0}: {1}", @"StreamService", ex),
                    Severity = TraceEventType.Error
                });

                serviceHost.Abort();
            }
        }
    }
}