using System;
using System.IO;
using System.ServiceModel;

namespace FileStream.Contracts
{
    using Common;

    [MessageContract]
    public class StreamInfo
    {
        [MessageHeader]
        public Guid Key { get; set; }

        /// <summary>
        /// can be: Content-Encoding: gzip, deflate
        /// </summary>
        [MessageHeader(Name = @"Content-Encoding")]
        public string ContentEncoding { get; set; }

        [MessageHeader]
        public string Hash { get; set; }

        [MessageHeader]
        public long Length { get; set; }

        [MessageHeader]
        public long Position { get; set; }

        public StreamInfo()
        {}

        public StreamInfo(Stream stream)
        {
            if (stream == null)
                return;

            var oldPosition = stream.Position;
            Key = Guid.NewGuid();
            Length = stream.Length;
            Hash = stream.SHA256();
            stream.Position = oldPosition;
        }
    }
}