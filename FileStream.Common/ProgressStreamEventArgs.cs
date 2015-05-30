using System;

namespace FileStream.Common
{
    public class ProgressStreamEventArgs : EventArgs
    {
        public DateTime Created { get; private set; }

        /// <summary>
        /// The number of bytes that were read/written to/from the stream.
        /// </summary>
        public int BytesMoved { get; private set; }

        /// <summary>
        /// The total length of the stream in bytes.
        /// </summary>
        public long StreamLength { get; private set; }

        /// <summary>
        /// The current position in the stream.
        /// </summary>
        public long StreamPosition { get; private set; }

        /// <summary>
        /// True if the bytes were read from the stream, false if they were written.
        /// </summary>
        public bool WasRead { get; private set; }

        /// <summary>
        /// Default constructor for ProgressStreamEventArgs.
        /// </summary>
        public ProgressStreamEventArgs() { }

        /// <summary>
        /// Creates a new ProgressStreamEventArgs initializing its members.
        /// </summary>
        /// <param name="bytesMoved">The number of bytes that were read/written to/from the stream.</param>
        /// <param name="streamLength">The total length of the stream in bytes.</param>
        /// <param name="streamPosition">The current position in the stream.</param>
        /// <param name="wasRead">True if the bytes were read from the stream, false if they were written.</param>
        /// <param name="created"> </param>
        public ProgressStreamEventArgs(int bytesMoved, long streamLength, long streamPosition, bool wasRead, DateTime created = default(DateTime))
            : this()
        {
            Created = (created == default(DateTime) ? DateTime.UtcNow : created);
            BytesMoved = bytesMoved;
            StreamLength = streamLength;
            StreamPosition = streamPosition;
            WasRead = wasRead;
        }
    }
}