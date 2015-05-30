using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.ServiceModel;
using Microsoft.Practices.EnterpriseLibrary.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileStream.Tests
{
    using Common;
    using Contracts;
    using DAL;

    /// <summary>
    /// todo: test for broken connection on upload & download
    /// todo: test for case with same files by hash
    /// </summary>
    [TestClass]
    public class FileStreamTest: BaseTest
    {
        static string InputFile = @"C:\Users\adudnik\Pictures\1016994_745473442139487_8152706858231675358_n.jpg";
        static string OutputFile = @"C:\Logs\test.jpg";
        readonly Guid _id = Guid.NewGuid();
        private static readonly string ConnectionString = ConfigurationManager.ConnectionStrings[@"Filestream"].ConnectionString;
        private string _fileHash;
        private StreamQuery _streamQuery;


        [TestMethod]
        public void CheckFileInDBTest()
        {
            CheckFile();
        }

        private void CheckFile()
        {
            File.Delete(OutputFile);

            using (new Tracer(@"check file in db"))
            using (var outputStream = new System.IO.FileStream(OutputFile, FileMode.CreateNew, FileAccess.ReadWrite))
            {
                // ReSharper disable once AccessToDisposedClosure
                Files.GetBody(ConnectionString, _id, s => s.CopyTo(outputStream));

                Assert.AreEqual(outputStream.SHA256(), _fileHash, @"hash does not matched");
            }
        }

        [TestMethod]
        public void UploadFileTest()
        {
            UploadFile();
        }

        protected void UploadFile()
        {
            using (new Tracer(@"upload file"))
            using (var streamProxyFactory = new ChannelFactory<IStreamServer>(@"streamService"))
            {
                var channal = streamProxyFactory.CreateChannel();

                Files.GetBody(ConnectionString, _id, s => channal.PrepareStream(new StreamMessage(s)
                {
                    Key = _id,
                    Hash = _fileHash
                }));

                var hasStream = channal.HasStream(new StreamQuery
                {
                    Hash = _fileHash
                });

                Assert.AreEqual(hasStream.Hash, _fileHash, string.Format(@"file not exists {0} {1}", _id, hasStream.Hash));
            }
        }

        [TestMethod]
        public void DownloadFileTest()
        {
            UploadFile();

            DownloadFile();

            CheckFile();
        }

        protected void DownloadFile()
        {
            using (new Tracer(@"download file"))
            using (var streamProxyFactory = new ChannelFactory<IStreamServer>(@"streamService"))
            {
                var channal = streamProxyFactory.CreateChannel();

                using (var serverStream = channal.GetStream(_id))
                using (var serverStreamProxy = serverStream.AsProxy())
                using (var outputStream = new System.IO.FileStream(OutputFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    var read = 0;
                    serverStreamProxy.BytesRead +=
                        (o, a) => Console.WriteLine(@"{0}/{1}", (read += a.BytesMoved), _streamQuery.Length);
                    serverStreamProxy.CopyTo(outputStream);

                    Logger.Write(new LogEntry
                    {
                        Severity = TraceEventType.Information,
                        Message =
                            string.Format(@"streamFromServer Hash = {0}, len = {1}", outputStream.SHA256(), outputStream.Length)
                    });
                    var downloadStream = channal.DownloadStream(_streamQuery);

                    Logger.Write(new LogEntry
                    {
                        Severity = TraceEventType.Information,
                        Message =
                            string.Format(@"downloadStream Key = {0}, Hash = {1}, len = {2}", downloadStream.Key,
                                downloadStream.Hash, downloadStream.Length)
                    });

                    Files.SetBody(ConnectionString, downloadStream.Stream, _id);

                    Logger.Write(new LogEntry {Severity = TraceEventType.Information, Message = @"Ok"});
                }
            }
        }

        [TestMethod]
        public void CheckFileExists()
        {
            UploadFile();

            using (new Tracer(@"check file (not exists)"))
            using (var streamProxyFactory = new ChannelFactory<IStreamServer>(@"streamService"))
            {
                var channal = streamProxyFactory.CreateChannel();

                var hasStream = channal.HasStream(new StreamQuery
                {
                    Key = _id
                });

                Assert.AreEqual(_fileHash, hasStream.Hash, string.Format(@"file exists {0} {1}", _id, hasStream.Hash));
            }
        }

        [TestInitialize]
        public void FileStreamTestInit()
        {
            InitFields();
            StartHosts();
            CreateFileRow();
        }

        private static void InitFields()
        {
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            InputFile = Path.Combine(Environment.CurrentDirectory, @"img.jpg");
            OutputFile = Path.Combine(Environment.CurrentDirectory, @"output.jpg");
        }

        protected void CreateFileRow()
        {
            using (new Tracer(@"save file to DB"))
            using (var stream = new System.IO.FileStream(InputFile, FileMode.Open))
            using (var conn = new SqlConnection(ConnectionString).OpenIt())
            using (
                var cmd = conn.CreateCommand(
                    @"if not exists(select top 1 1 from dbo.Files where ID = @id) insert into dbo.Files (ID, Name) values (@id, 'img.jpg');")
                    .AddParam(@"id", SqlDbType.UniqueIdentifier, 16, _id))
            {
                var streamInfo = new StreamInfo(stream);
                _streamQuery = new StreamQuery(streamInfo) {Key = _id};
                _fileHash = stream.SHA256();
                stream.Position = 0;

                cmd.ExecuteNonQuery();

                Files.SetBody(ConnectionString, stream, _id);

                Logger.Write(new LogEntry
                {
                    Severity = TraceEventType.Information,
                    Message =
                        string.Format(@"original Key = {0}, Hash = {1}, len = {2}", streamInfo.Key, streamInfo.Hash,
                            streamInfo.Length)
                });
            }
        }

        [TestCleanup]
        public void FileStreamTestCleanUp()
        {
            CleanFile();
            BaseTestCleanup();
        }

        public void CleanFile()
        {
            using (new Tracer(@"clear DB"))
            using (var conn = new SqlConnection(ConnectionString).OpenIt())
            using (
                var cmd = conn.CreateCommand(
                    @"delete from dbo.Files where id = @id")
                    .AddParam(@"id", SqlDbType.UniqueIdentifier, 16, _id))
            {
                cmd.ExecuteNonQuery();
            }

            using (new Tracer(@"delete file output"))
                File.Delete(OutputFile);
        }

    }
}
