using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace PPeX.External.PP
{
	/// <summary>
	/// If removed from a ppParser, CreateReadStream() is no longer guaranteed to work. The .pp file may have changed,
	/// so you have to transfer the ppSubfile's data when removing.
	/// </summary>
	public class ppSubfile : NeedsSourceStreamForWriting, IReadFile, IWriteFile
	{
		public string ppPath;
		public uint offset;
		public uint size;
		public ppFormat ppFormat;

		public object Metadata { get; set; }
		public Stream SourceStream { get; set; }

		public ppSubfile(string ppPath)
		{
			this.ppPath = ppPath;
		}

		public string Name { get; set; }

		public void WriteTo(Stream stream)
		{
			BinaryReader reader = new BinaryReader(SourceStream);
			BinaryWriter writer = new BinaryWriter(stream);
			for (byte[] buf; (buf = reader.ReadBytes(Utility.BufSize)).Length > 0; )
			{
				writer.Write(buf);
			}
		}

		public Stream CreateReadStream()
		{
			FileStream fs = null;
			try
			{
				fs = File.OpenRead(ppPath);
				fs.Seek(offset, SeekOrigin.Begin);
				return ppFormat.ReadStream(new PartialStream(fs, size));
			}
			catch (Exception e)
			{
				if (fs != null)
				{
					fs.Close();
				}
				throw e;
			}
		}

		public override string ToString()
		{
			return this.Name;
		}
	}

	// Represents a subsection of the stream. This forces a CryptoStream to use TransformFinalBlock() at the end of the subsection.
	public class PartialStream : Stream
	{
		public override bool CanRead
		{
			get { return stream.CanRead; }
		}

		public override bool CanSeek
		{
			get { return stream.CanSeek; }
		}

		public override bool CanWrite
		{
			get { return stream.CanWrite; }
		}

		public override void Flush()
		{
			stream.Flush();
		}

		public override long Length
		{
			get { return this.length; }
		}

        public override long Position
        {
            get
            {
                return stream.Position - offset;
            }
            set
            {
                stream.Position = value + offset;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
		{
			if ((stream.Position + count) > end)
			{
				count = (int)(end - stream.Position);
			}

			if (count < 0)
			{
				return 0;
			}
			else
			{
				return stream.Read(buffer, offset, count);
			}
		}

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = length + offset;
                    break;
            }

            return Position;
        }


        public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		private Stream stream = null;
		private long offset = 0;
		private long length = 0;
		private long end = 0;

		public PartialStream(Stream stream, long length)
		{
			if ((length + stream.Position) > stream.Length)
			{
				throw new ArgumentOutOfRangeException();
			}

			this.stream = stream;
			this.offset = stream.Position;
			this.length = length;
			this.end = this.offset + this.length;
		}

		public override void Close()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected override void Dispose(bool disposing)
		{
			try
			{
				if (disposing)
				{
					this.stream.Close();
				}
			}
			finally
			{
				base.Dispose(disposing);
			}
		}
	}
}
