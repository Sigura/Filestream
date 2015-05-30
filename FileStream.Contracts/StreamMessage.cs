using System;
using System.IO;
using System.ServiceModel;

namespace FileStream.Contracts
{
    [MessageContract]
    public class StreamMessage : StreamInfo, IDisposable
    {
        public StreamMessage()
        { }

        public StreamMessage(StreamInfo streamInfo)
        {
            Fill(streamInfo);
        }

        private void Fill(StreamInfo streamInfo)
        {
            Key = streamInfo.Key;
            Hash = streamInfo.Hash;
            Length = streamInfo.Length;
        }

        private void Fill(StreamQuery streamInfo)
        {
            Key = streamInfo.Key;
            Hash = streamInfo.Hash;
            Length = streamInfo.Length;
        }

        public StreamMessage(StreamQuery streamInfo, Stream stream)
            : this(stream)
        {
            Fill(streamInfo);
        }

        public StreamMessage(Stream stream, bool compressed = false)
            : base(stream)
        {
            Stream = stream;

            if (compressed)
                ContentEncoding = @"gzip, deflate";
        }

        [MessageBodyMember/*(Order = 1)*/]
        public Stream Stream { get; set; }

        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed && Stream != null)
                Stream.Dispose();

            _disposed = true;
        }
    }
}