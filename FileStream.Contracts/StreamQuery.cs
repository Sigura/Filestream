using System;
using System.IO;
using System.ServiceModel;

namespace FileStream.Contracts
{
    using Common;

    [MessageContract]
    public class StreamQuery
    {
        [MessageHeader]
        public Guid Key { get; set; }

        [MessageHeader]
        public string Hash { get; set; }

        [MessageHeader]
        public long Length { get; set; }

        [MessageHeader]
        public long FromByte { get; set; }

        /// <summary>
        /// gzipped when Accept-Encoding: gzip
        /// </summary>
        [MessageHeader(Name = @"Accept-Encoding")]
        public string AcceptEncoding { get; set; }

        public StreamQuery()
        {
            FromByte = 0;
        }

        public StreamQuery(StreamInfo streamInfo, bool? compressed = null)
        {
            FromByte = 0;
            Key = streamInfo.Key;
            Hash = streamInfo.Hash;
            Length = streamInfo.Length;
            AcceptEncoding = streamInfo.ContentEncoding;
            if (compressed == true)
                AcceptEncoding = @"gzip, deflate";
            if (compressed == false)
                AcceptEncoding = null;
            //FromByte = streamInfo.Position;
        }

        public StreamQuery(Stream stream, bool compressed = false)
        {
            var oldPosition = stream.Position;
            Key = Guid.NewGuid();
            Length = stream.Length;
            Hash = stream.SHA256();
            stream.Position = oldPosition;
            if (compressed)
                AcceptEncoding = @"gzip, deflate";
        }
    }
}
