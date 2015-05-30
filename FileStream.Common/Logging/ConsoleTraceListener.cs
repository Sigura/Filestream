using System;
using System.Diagnostics;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using Microsoft.Practices.EnterpriseLibrary.Logging;
using Microsoft.Practices.EnterpriseLibrary.Logging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.Logging.TraceListeners;

namespace FileStream.Common.Logging
{
    [ConfigurationElementType(typeof(CustomTraceListenerData))]
    public class ConsoleTraceListener : CustomTraceListener
    {
        public ConsoleTraceListener()
        {
            try
            {
                Console.BufferHeight = 9999;
            }
            catch { }
        }

        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, object data)
        {
            var color = Console.ForegroundColor;
            var log = data as LogEntry;

            try
            {
                SetConsoleColor(eventType);

                WriteLine(log != null && Formatter != null ? Formatter.Format(log) : data.ToString());

                Console.ForegroundColor = color;
            }
            catch
            {
            }
        }

        public override void Write(string message)
        {
            try {
                Console.Write(message);
            }
            catch
            {
            }
        }

        public override void WriteLine(string message)
        {
            try {
                Console.WriteLine(message);
            }
            catch
            {
            }
        }

        private static void SetConsoleColor(TraceEventType severity)
        {
            switch (severity)
            {
                case TraceEventType.Information:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case TraceEventType.Stop:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
                case TraceEventType.Start:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    break;
                case TraceEventType.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case TraceEventType.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case TraceEventType.Critical:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }
        }
    }
}
