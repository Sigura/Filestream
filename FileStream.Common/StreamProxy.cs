using System;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using Microsoft.Practices.EnterpriseLibrary.Logging;

namespace FileStream.Common
{
    public class StreamProxy : Stream
    {
        private readonly Stream _stream;

        #region events
        /// <summary>
        /// Raised when bytes are read from the stream.
        /// </summary>
        public event EventHandler<ProgressStreamEventArgs> BytesRead;
        public event EventHandler<ProgressStreamEventArgs> BytesWritten;
        public event EventHandler<ProgressStreamEventArgs> BytesMoved;

        public void OnBytesRead(int bytesMoved)
        {
            var handler = BytesRead;

            if (handler != null) handler(this, new ProgressStreamEventArgs(bytesMoved, TryGetLength, TryGetPosition, true));
        }

        public void OnBytesWritten(int bytesMoved)
        {
            var handler = BytesWritten;
            if (handler != null) handler(this, new ProgressStreamEventArgs(bytesMoved, TryGetLength, TryGetPosition, false));
        }

        public void OnBytesMoved(int bytesMoved, bool isRead)
        {
            var handler = BytesMoved;
            if (handler != null) handler(this, new ProgressStreamEventArgs(bytesMoved, TryGetLength, TryGetPosition, isRead));
        }


        protected long TryGetPosition
        {
            get
            {
                try
                {
                    return _stream.Position;
                }
                catch (NotSupportedException)
                {
                }
                return long.MinValue;
            }
        }

        protected long TryGetLength
        {
            get
            {
                try
                {
                    if (_stream.CanSeek)
                        return _stream.Length;
                }
                catch (NotSupportedException)
                {
                }
                return long.MinValue;
            }
        }
        #endregion

        public StreamProxy(Stream stream)
        {
            //Contract.Requires<ArgumentNullException>(stream != null);

            _stream = stream;
        }

        public IObservable<EventPattern<ProgressStreamEventArgs>> ReadAsObservable()
        {
            return Observable
                .FromEventPattern<EventHandler<ProgressStreamEventArgs>, ProgressStreamEventArgs>
                (
                    ev => BytesRead += ev,
                    ev => BytesRead -= ev
                );
        }

        public IObservable<EventPattern<ProgressStreamEventArgs>> WriteAsObservable()
        {
            return Observable
                .FromEventPattern<EventHandler<ProgressStreamEventArgs>, ProgressStreamEventArgs>
                (
                    ev => BytesWritten += ev,
                    ev => BytesWritten -= ev
                );
        }

        public override void Close()
        {
            try
            {
                _stream.Close();
            }
            catch (ObjectDisposedException e)
            {
#if DEBUG
                Logger.Write(new LogEntry{ Title = @"stream closed with error", Message = e.ToString(), Severity = TraceEventType.Error});
#endif

            }

            base.Close();
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _stream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = _stream.Read(buffer, offset, count);

            OnBytesRead(bytesRead);
            OnBytesMoved(bytesRead, true);

            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);

            OnBytesWritten(count);
            OnBytesMoved(count, false);
        }

        public override bool CanRead
        {
            get { return _stream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _stream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _stream.CanWrite; }
        }

        public override long Length
        {
            get { return _stream.Length; }
        }

        public override long Position
        {
            get { return _stream.Position; }
            set { _stream.Position = value; }
        }

        public new void Dispose()
        {
            _stream.Dispose();

            base.Dispose();
        }
    }
}