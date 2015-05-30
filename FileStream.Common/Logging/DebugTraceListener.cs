#region usings

using System;
using System.Diagnostics;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using Microsoft.Practices.EnterpriseLibrary.Logging;
using Microsoft.Practices.EnterpriseLibrary.Logging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.Logging.TraceListeners;

#endregion

namespace FileStream.Common.Logging
{
    [ConfigurationElementType(typeof (CustomTraceListenerData))]
    public class DebugTraceListener : CustomTraceListener
    {
        public override void Write(string message)
        {
            Debug.Write(message);
            Console.Write(message);
        }

        public override void WriteLine(string message)
        {
            Debug.WriteLine(message);
            Console.WriteLine(message);
        }

        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id,
            object data)
        {
            if (data is LogEntry && Formatter != null)
            {
                WriteLine(Formatter.Format(data as LogEntry));
            }
            else
            {
                WriteLine(data.ToString());
            }
        }
    }
}