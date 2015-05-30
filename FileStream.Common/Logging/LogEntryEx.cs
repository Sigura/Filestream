using System;
using Microsoft.Practices.EnterpriseLibrary.Logging;
using Newtonsoft.Json;

namespace FileStream.Common.Logging
{
    public class LogEntryEx : LogEntry
    {
        public Guid ProcessID;

        public LogEntryEx() { }
        public LogEntryEx(string title, string message)
        {
            TimeStamp = DateTime.UtcNow;
            Title = title;
            Message = message;
        }

        public string SetObject<T>(T o) where T : class
        {
            return JsonObject = JsonSerialize(o);
        }

        public string JsonObject { get; set; }

        public override string ToString()
        {
            return string.Format(@"[{2}] {4} {3} {1}: {0}, object = {5}", Message, Title, TimeStamp.ToLocalTime().ToString(@"s"), GetType().Name, ProcessID, JsonObject);
        }
        public static string JsonSerialize<T>(T package)
            where T : class
        {
            if (package == null)
                return @"null";

            return JsonConvert.SerializeObject(package, Formatting.Indented,
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    PreserveReferencesHandling = PreserveReferencesHandling.None
                });
        }
    }
}