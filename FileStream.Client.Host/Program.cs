using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.ServiceModel;
using Microsoft.Practices.EnterpriseLibrary.Logging;

namespace FileStream.Client.Host
{
    using Common;
    using Contracts;
    using DAL;

    /// <summary>
    /// http://www.ceservices.com/adding-filestream-existing-table-database-sql-2008-r2
    /// 
    /// alter database [Filestream] add filegroup fsGroup contains filestream;
    /// go
    /// 
    /// alter database [Filestream]
    /// add file
    ///   ( NAME = 'fsFilestream', FILENAME = 'C:\Program Files\Microsoft SQL Server\MSSQL11.MSSQL\MSSQL\DATA\Filestream'
    ///    )
    /// to filegroup fsGroup;
    /// go
    /// 
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            var inputFile = Path.Combine(Environment.CurrentDirectory, @"img.jpg");
            var outputFile = Path.Combine(Environment.CurrentDirectory, @"output.jpg");
            var connectionString = ConfigurationManager.ConnectionStrings[@"Filestream"].ConnectionString;
            var id = Guid.NewGuid();
            string fileHash;
            StreamQuery streamQuery = null;

            using (new Tracer(@"save file to DB"))
            using (var stream = new System.IO.FileStream(inputFile, FileMode.Open))
            using (var conn = new SqlConnection(connectionString).OpenIt())
            using (
                var cmd = conn.CreateCommand(
                    @"if not exists(select top 1 1 from dbo.Files where ID = @id) insert into dbo.Files (ID, Name) values (@id, 'img.jpg');")
                    .AddParam(@"id", SqlDbType.UniqueIdentifier, 16, id))
            {
                var streamInfo = new StreamInfo(stream);
                streamQuery = new StreamQuery(streamInfo);
                fileHash = stream.SHA256();
                stream.Position = 0;

                cmd.ExecuteNonQuery();

                Files.SetBody(connectionString, stream, id);

                Logger.Write(new LogEntry { Severity = TraceEventType.Information, Message = string.Format(@"original Key = {0}, Hash = {1}, len = {2}", streamInfo.Key, streamInfo.Hash, streamInfo.Length) });
            }

            using (new Tracer(@"check file (not exists)"))
            using (var streamProxyFactory = new ChannelFactory<IStreamServer>(@"streamService"))
            {
                var channal = streamProxyFactory.CreateChannel();

                var hasStream = channal.HasStream(streamQuery);

                Logger.Write(fileHash != hasStream.Hash
                    ? new LogEntry {Severity = TraceEventType.Information, Message = @"Ok"}
                    : new LogEntry {Severity = TraceEventType.Warning, Message = string.Format(@"file exists {0} {1}", id, fileHash)});
            }

            using (new Tracer(@"upload file"))
            using (var streamProxyFactory = new ChannelFactory<IStreamServer>(@"streamService"))
            {
                var channal = streamProxyFactory.CreateChannel();

                Files.GetBody(connectionString, id, s => channal.PrepareStream(new StreamMessage(streamQuery, s)));

                var hasStream = channal.HasStream(streamQuery);

                Logger.Write(fileHash == hasStream.Hash
                    ? new LogEntry { Severity = TraceEventType.Information, Message = @"Ok" }
                    : new LogEntry { Severity = TraceEventType.Warning, Message = string.Format(@"file not exists {0} {1}", id, hasStream.Hash) });
            }

            using (new Tracer(@"download file"))
            using (var streamProxyFactory = new ChannelFactory<IStreamServer>(@"streamService"))
            {
                var channal = streamProxyFactory.CreateChannel();

                using (var serverStream = channal.GetStream(streamQuery.Key))
                using (var serverStreamProxy = serverStream.AsProxy())
                using (var outputStream = new System.IO.FileStream(outputFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    var read = 0;
                    serverStreamProxy.BytesRead += (o, a) => Console.WriteLine(@"{0}/{1}", (read += a.BytesMoved), streamQuery.Length);
                    serverStreamProxy.CopyTo(outputStream);

                    Logger.Write(new LogEntry { Severity = TraceEventType.Information, Message = string.Format(@"streamFromServer Hash = {0}, len = {1}", outputStream.SHA256(), outputStream.Length)});
                    var downloadStream = channal.DownloadStream(streamQuery);

                    Logger.Write(new LogEntry { Severity = TraceEventType.Information, Message = string.Format(@"downloadStream Key = {0}, Hash = {1}, len = {2}", downloadStream.Key, downloadStream.Hash, downloadStream.Length)});

                    Files.SetBody(connectionString, downloadStream.Stream, id);

                    Logger.Write(new LogEntry { Severity = TraceEventType.Information, Message = @"Ok" });
                }
            }

            using (new Tracer(@"check file in db"))
            using (var outputStream = new System.IO.FileStream(outputFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                Files.GetBody(connectionString, id, s => s.CopyTo(outputStream));

                Logger.Write(outputStream.SHA256() == fileHash
                    ? new LogEntry {Severity = TraceEventType.Information, Message = @"Ok"}
                    : new LogEntry {Severity = TraceEventType.Error, Message = @"hash does not matched"});
            }

            using (new Tracer(@"clear DB"))
            using (var conn = new SqlConnection(connectionString).OpenIt())
            using (
                var cmd = conn.CreateCommand(
                    @"delete from dbo.Files where id = @id")
                    .AddParam(@"id", SqlDbType.UniqueIdentifier, 16, id))
            {
                cmd.ExecuteNonQuery();
                File.Delete(outputFile);
            }
        }
    }
}
