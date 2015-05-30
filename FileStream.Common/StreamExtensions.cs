using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Practices.EnterpriseLibrary.Logging;

namespace FileStream.Common
{
    public static partial class StreamExtensions
    {
        public static string SHA256(this Stream stream)
        {
            try
            {
                stream.Position = 0;
            }
            catch (NotSupportedException)
            {
            }


            using (var sh1 = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sh1.ComputeHash(stream);

                return Convert.ToBase64String(hash);
            }
        }

        public static string SHA256(this byte[] body)
        {
            if (body == null)
                return null;

            string result;

            using (var shaM = System.Security.Cryptography.SHA256.Create())
            {
                var sha = shaM.ComputeHash(body);
                result = Convert.ToBase64String(sha);
            }

            return result;
        }

        public static byte[] Serialize(this DataSet dataSet)
        {
            dataSet.RemotingFormat = SerializationFormat.Binary;

            var dss = new DataSetSurrogate(dataSet);
            var memStream = new MemoryStream();
            var format = new BinaryFormatter();
            format.Serialize(memStream, dss);
            return memStream.ToArray();
        }

        public static StreamProxy AsProxy(this Stream stream)
        {
            return new StreamProxy(stream);
        }

        public static System.IO.FileStream Compress(this Stream stream, string path = null)
        {
            path = path ??
                   Path.Combine(Path.GetTempPath(), string.Format(@"{0}{1}", @"compress-", Guid.NewGuid()));
            var result = new System.IO.FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            var startTime = DateTime.Now;

            using (stream)
            using (var compress = new DeflateStream(result, CompressionMode.Compress, true))
            {
                stream.CopyTo(compress);
            }
            result.Position = 0;
            Logger.Write(new LogEntry { Title = @"Compress Stream", Message = string.Format(@"compress duration: {0}ms", (DateTime.Now - startTime).TotalMilliseconds), Severity = TraceEventType.Information });
            return result;
        }

        public static System.IO.FileStream Decompress(this Stream stream, string path = null)
        {
            path = path ??
                   Path.Combine(Path.GetTempPath(), string.Format(@"{0}{1}", @"decompress-", Guid.NewGuid()));
            var result = new System.IO.FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            var startTime = DateTime.Now;

            using (var decompress = new DeflateStream(stream, CompressionMode.Decompress))
            {
                try
                {
                    decompress.CopyTo(result);
                }
                catch (InvalidDataException e)
                {
                    Logger.Write(new LogEntry { Title = @"Decompress Stream", Message = e.ToString(), Severity = TraceEventType.Error });

                    var fs = stream as System.IO.FileStream;
                    
                    if (fs == null) throw;
                    
                    fs.Close();
                    decompress.Close();

                    File.Delete(fs.Name);
                    File.Delete(path);

                    throw;
                }
            }
            result.Position = 0;
            Logger.Write(new LogEntry { Title = @"Decompress Stream", Message = string.Format(@"decompress duration: {0}ms", (DateTime.Now - startTime).TotalMilliseconds), Severity = TraceEventType.Information });

            return result;
        }

        public static System.IO.FileStream StreamDataSet(this DataSet dataSet)
        {
            dataSet.RemotingFormat = SerializationFormat.Binary;
            var startTime = DateTime.Now;
            var dss = new DataSetSurrogate(dataSet);
            var path = Path.Combine(Path.GetTempPath(),
                                    string.Format(@"{0}{1}", @"streamdataset-", Guid.NewGuid()));
            var fileStream = new System.IO.FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite);
            var format = new BinaryFormatter();
            format.Serialize(fileStream, dss);
            fileStream.Position = 0;
            Logger.Write(new LogEntry { Title = @"StreamDataSet DataSet", Message = string.Format(@"dataset {1} streamed length:{0} duration: {2}ms", fileStream.Length, dataSet.DataSetName, (DateTime.Now - startTime).TotalMilliseconds), Severity = TraceEventType.Information });
            return fileStream;
        }

        public static DataSet Deserialize(this Stream obj)
        {
            var startTime = DateTime.Now;
            var format = new BinaryFormatter();

            obj.Position = 0;

            var dss = (DataSetSurrogate)format.Deserialize(obj);

            var result = dss.ConvertToDataSet();

            Logger.Write(new LogEntry { Title = @"Deserialize DataSet", Message = string.Format(@"Deserialize duration: {0}ms", (DateTime.Now - startTime).TotalMilliseconds), Severity = TraceEventType.Information });

            return result;
        }

        public static DataSet Deserialize(this byte[] obj)
        {
            var startTime = DateTime.Now;
            var memStream = new MemoryStream(obj) { Position = 0 };
            var format = new BinaryFormatter();

            var dss = (DataSetSurrogate)format.Deserialize(memStream);
            var result = dss.ConvertToDataSet();

            Logger.Write(new LogEntry { Title = @"Deserialize DataSet", Message = string.Format(@"Deserialize duration: {0}ms", (DateTime.Now - startTime).TotalMilliseconds), Severity = TraceEventType.Information });
            //Logger.Instance.Info();

            return result;
        }

        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> array, Action<T> action)
        {
            if (array == null)
                return array;

            foreach (var item in array)
            {
                action.Invoke(item);
            }

            return array;
        }

        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> array, Action<T, long> action)
        {
            if (array == null)
                return array;
            long i = 0;
            foreach (var item in array)
            {
                action.Invoke(item, i);
                ++i;
            }

            return array;
        }

    }
}
