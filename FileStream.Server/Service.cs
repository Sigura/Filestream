using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.ServiceModel;
using System.Threading;
using Microsoft.Practices.EnterpriseLibrary.Logging;

namespace FileStream.Server
{
    using Common;
    using Contracts;

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public class Service : IStreamServer, IDisposable
    {
        private static readonly Dictionary<Guid, StreamItem> Dictionary = new Dictionary<Guid, StreamItem>();
        private static readonly Dictionary<Guid, StreamItem> ShredderDictionary = new Dictionary<Guid, StreamItem>();
        private static readonly object SyncObject = new object();

        private readonly OperationContext _operationContext;

        public Service(/*IOperationContext operationContext*/)
        {
            _operationContext = OperationContext.Current;
        }


        #region IStreamServer Members

        public Stream GetStream(Guid streamID)
        {
            var query = new StreamQuery { Key = streamID };
            var result = DownloadStream(query);

            _operationContext.OperationCompleted += (sender, args) =>
            {
                if (result.Stream != null) result.Stream.Dispose();
            };

            return result.Stream;
        }

        private static Stream Stream(Guid streamID)
        {
            StreamItem streamItem;
            lock (SyncObject)
            {
                streamItem = Dictionary.ContainsKey(streamID) ? Dictionary[streamID] : null;
            }
            if (streamItem == null)
            {
                Logger.Write(new LogEntry { ActivityId = streamID, Title = @"StreamNotReceivedException", Message = string.Format(@"StreamNotReceivedException {0}", streamID), Severity = TraceEventType.Warning });

                return null;
            }

            var stream = new System.IO.FileStream(streamItem.FileName, FileMode.Open, FileAccess.Read, FileShare.Read) { Position = 0 };

            //AddToShredder(streamID, streamItem);

            return stream;
        }

        private static void AddToShredder(Guid streamID, StreamItem streamItem)
        {
            lock (SyncObject)
            {
                streamItem.Getted = DateTime.UtcNow;

                if (ShredderDictionary.ContainsKey(streamID))
                    return;

                //Logger.Instance.Debug(@"file added to ShredderDictionary {0}", streamItem.FileName);
                ShredderDictionary.Add(streamID, streamItem);
            }
        }

        private const int CopyBufferSize = 80 * 1024;

        public StreamMessage DownloadStream(StreamQuery streamQuery)
        {
            var stream = Stream(streamQuery.Key);

            if (stream == null)
                return new StreamMessage(streamQuery, null);


            var contentEncoding = string.Empty;

            if (streamQuery.AcceptEncoding == @"gzip, deflate")
            {
                //var path = GetPath(streamQuery.Key) + @".gz";
                var key = Guid.NewGuid();
                var path = string.Format(@"{0}{1}{2}", GetPath(key), @"-downloadstream", @".gz");
                var output = stream.Compress(path);

                AddToShredder(key, new StreamItem(new StreamMessage(output), path));

                stream = output;
                contentEncoding = streamQuery.AcceptEncoding;
            }

            _operationContext.OperationCompleted += (sender, args) => stream.Dispose();
            stream.Position = streamQuery.FromByte;

            return new StreamMessage
            {
                Key = streamQuery.Key,
                ContentEncoding = contentEncoding,
                Stream = stream,
                Hash = streamQuery.Hash,
                Position = stream.Length,
                Length = stream.Length
            };
        }

        public StreamInfo HasStream(StreamQuery message)
        {
            var predicate = (Func<KeyValuePair<Guid, StreamItem>, bool>)(d => d.Key == message.Key || (!string.IsNullOrWhiteSpace(message.Hash) && ((d.Value.Hash == message.Hash) || (d.Value.DeflateHash == message.Hash))));

            bool hasStream;
            lock (SyncObject)
            {
                hasStream = Dictionary.Any(predicate);
            }
            if (!hasStream)
                return new StreamInfo
                {
                    Key = message.Key,
                };

            KeyValuePair<Guid, StreamItem> stream;
            lock (SyncObject)
            {
                stream = Dictionary.FirstOrDefault(predicate);
            }
            using (var file = new System.IO.FileStream(stream.Value.FileName, FileMode.Open, FileAccess.Read))
                return new StreamInfo
                {
                    ContentEncoding = stream.Value.ContentEncoding,
                    Position = file.Length,
                    Hash = stream.Value.Hash,
                    Key = stream.Key,
                    Length = stream.Value.Length
                };
        }

        public void PrepareStream(StreamMessage message)
        {
            var predicate = (Func<KeyValuePair<Guid, StreamItem>, bool>)(d => d.Key == message.Key || (!string.IsNullOrWhiteSpace(message.Hash) && ((d.Value.Hash == message.Hash) || (d.Value.DeflateHash == message.Hash))));

            StreamItem streamItem;
            lock (SyncObject)
            {
                var hasStream = Dictionary.Any(predicate);
                streamItem = hasStream
                                 ? Dictionary.FirstOrDefault(predicate).Value
                                 : new StreamItem(message);
            }
            if (!streamItem.Downloaded)
            {
                CopyStream(message, streamItem);

                long len;

                using (var input = new System.IO.FileStream(streamItem.FileName, FileMode.Open, FileAccess.Read))
                    len = input.Length;

                if ((message.ContentEncoding == @"gzip, deflate" || message.ContentEncoding == @"gzip") &&
                    message.Length == len)
                {
                    var newName = streamItem.FileName + @".gz";
                    File.Move(streamItem.FileName, newName);

                    using (var input = new System.IO.FileStream(newName, FileMode.Open, FileAccess.Read))
                        streamItem.DeflateHash = input.SHA256();

                    using (var decompressed = new System.IO.FileStream(newName, FileMode.Open, FileAccess.Read)
                        .Decompress(streamItem.FileName))
                    {
                        streamItem.Length = decompressed.Length;
                        streamItem.ContentEncoding = null;
                        streamItem.Hash = decompressed.SHA256();
                    }

                    File.Delete(newName);
                }


                Logger.Write(new LogEntry { ActivityId = message.Key, Title = @"PrepareStream", Message = string.Format(@"added stream: {0}, key: {1}", streamItem.Length, message.Key), Severity = TraceEventType.Information });
            }

            AddToDictionary(message, streamItem);
        }

        private static void AddToDictionary(StreamInfo message, StreamItem streamItem)
        {
            lock (SyncObject)
            {
                if (!Dictionary.ContainsKey(message.Key))
                    Dictionary.Add(message.Key, streamItem);
            }
        }

        public void Stop()
        {
            Clear();
        }

        #endregion

        private static void CopyStream(StreamMessage message, StreamItem streamItem)
        {
            Logger.Write(new LogEntry { ActivityId = message.Key, Title = @"CopyStream", Message = string.Format(@"start copy stream key: {0}", message.Key), Severity = TraceEventType.Information, Categories = new []{@"CopyStream"}});

            using (var output = new System.IO.FileStream(streamItem.FileName, FileMode.OpenOrCreate, FileAccess.Write))
            {
                Logger.Write(new LogEntry { ActivityId = message.Key, Title = @"CopyStream", Message = string.Format(@"Has bytes {0}/{1}", output.Length, message.Length), Severity = TraceEventType.Information, Categories = new[] { @"CopyStream" } });
                try
                {
                    output.Position = message.Position;

                    message.Stream.CopyTo(output, CopyBufferSize);

                    streamItem.Downloaded = true;// streamItem.Length == output.Length;

                    Logger.Write(new LogEntry { ActivityId = message.Key, Title = @"CopyStream", Message = string.Format(@"copyed stream size: {0}, key: {1}", output.Length, message.Key), Severity = TraceEventType.Warning, Categories = new[] { @"CopyStream" } });
                }
                catch (InvalidDataException e)
                {
#if DEBUG
                    Logger.Write(new LogEntry { ActivityId = message.Key, Title = @"CopyStream", Message = string.Format(@"CopyStream failed with InvalidDataException {0}", e), Severity = TraceEventType.Warning, Categories = new[] { @"CopyStream" } });
#endif
                }
                catch (TimeoutException e)
                {
#if DEBUG
                    Logger.Write(new LogEntry { ActivityId = message.Key, Title = @"CopyStream", Message = string.Format(@"CopyStream failed with TimeoutException {0}", e), Severity = TraceEventType.Warning, Categories = new[] { @"CopyStream" } });
#endif
                }
                catch (CommunicationException e)
                {
#if DEBUG
                    Logger.Write(new LogEntry { ActivityId = message.Key, Title = @"CopyStream", Message = string.Format(@"CopyStream failed with CommunicationException {0}", e), Severity = TraceEventType.Warning, Categories = new[] { @"CopyStream" } });
#endif
                }
                catch (SocketException e)
                {
#if DEBUG
                    Logger.Write(new LogEntry { ActivityId = message.Key, Title = @"CopyStream", Message = string.Format(@"CopyStream failed with SocketException {0}", e), Severity = TraceEventType.Warning, Categories = new[] { @"CopyStream" } });
#endif
                }
                catch (IOException e)
                {
#if DEBUG
                    Logger.Write(new LogEntry { ActivityId = message.Key, Title = @"CopyStream", Message = string.Format(@"CopyStream failed with IOException {0}", e), Severity = TraceEventType.Warning, Categories = new[] { @"CopyStream" } });
#endif
                }
                Logger.Write(new LogEntry { ActivityId = message.Key, Title = @"CopyStream", Message = string.Format(@"now {0}/{1}", output.Length, message.Length), Severity = TraceEventType.Information, Categories = new[] { @"CopyStream" } });
            }
        }

        private static string GetPath(Guid key)
        {
            return Path.Combine(Path.GetTempPath(), string.Format(@"stream-server-{0}", key));
        }

        #region Eraser

        private static void TryWrap(Action a, string action, int counter = 5, string error = @"")
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                error = string.Format("{0} ({1}): {2}\n\n{3}", action, counter, e, error);

                if (counter > 0)
                    new Thread(() =>
                    {
                        Thread.Sleep(5000);

                        TryWrap(a, action, --counter, error);
                    }).Start();
                else
                    Logger.Write(new LogEntry { Title = @"CopyStream", Message = error, Severity = TraceEventType.Error });
            }
        }

        private static bool DisposeStream(Guid streamID, Dictionary<Guid, StreamItem> dictionary)
        {
            if (!dictionary.ContainsKey(streamID))
                return false;
            var streamItem = dictionary[streamID];

            TryWrap(() => File.Delete(streamItem.FileName), string.Format(@"File.Delete {0}", streamItem.FileName));
            return true;
        }

        ~Service()
        {
            CleanShreader();
        }

        private static void CleanShreader()
        {
            lock (SyncObject)
            {
                ShredderDictionary
                    .ForEach(v => DisposeStream(v.Key, ShredderDictionary));
                ShredderDictionary.Clear();
            }
        }

        public void Dispose()
        {
            CleanShreader();
        }

        private static void RemoveAll()
        {
            lock (SyncObject)
            {
                var keys = Dictionary.Select(i => i.Key).ToArray();

                keys
                    .ForEach(k => DisposeStream(k, Dictionary));
            }
        }

        private static void Clear()
        {
            RemoveAll();

            if (Dictionary != null)
                Dictionary.Clear();
        }

        #endregion

        #region Nested type: StreamItem

        internal class StreamItem
        {
            public string FileName;
            public string Hash;
            public string DeflateHash;
            public DateTime Created;
            public DateTime Getted;
            public long Length;
            public bool Downloaded;
            public string ContentEncoding;

            internal StreamItem(StreamMessage message, string fileName)
            {
                FileName = fileName;
                Created = DateTime.UtcNow;
                Hash = message.Hash;
                Length = message.Length;
                ContentEncoding = message.ContentEncoding;
            }

            public StreamItem(StreamMessage message)
                : this(message, GetPath(message.Key) + @"-empty")
            {

            }
        }

        #endregion
    }
}
