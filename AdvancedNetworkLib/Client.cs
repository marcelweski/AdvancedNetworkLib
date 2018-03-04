using System;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace AdvancedNetworkLib
{
	public class Client : Base
    {
		// Variables
		private byte[] bufferReceived;

		private enum ReadMode
		{
			ObjectSize,
			Object,
		}
		private ReadMode readMode;
		private MemoryStream objectSizeBytes;
		private MemoryStream objectBytes;
		private long finalObjectSize;

		// Properties
		public IPEndPoint RemoteEndPoint { get => (IPEndPoint)this.socket.RemoteEndPoint; }
		public string Host { get => this.RemoteEndPoint.Address.ToString(); }
		public ushort Port { get => Convert.ToUInt16(this.RemoteEndPoint.Port); }
		public bool Connected { get => (this.socket != null ? this.socket.Connected : false); }
		public object UserData { get; set; }

		// Events
		public event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;
		public event EventHandler<ObjectReceivedEventArgs> ObjectReceived;
		public event EventHandler<ConnectionChangedEventArgs> ConnectionChanged;
		public event EventHandler SendSuccessfull;

		public Client(Control control = null) : base(control)
		{
			this.readMode = ReadMode.ObjectSize;
			this.objectSizeBytes = new MemoryStream();
			this.objectBytes = new MemoryStream();
			this.finalObjectSize = 0;
		}
		public Client(Socket socket, Control control = null) : this(control)
		{
			this.socket = socket;
			if (this.socket != null)
			{
				if (this.bufferReceived == null)
				{
					this.bufferReceived = new byte[this.socket.ReceiveBufferSize];
				}
				this.socket.BeginReceive(this.bufferReceived, 0, this.bufferReceived.Length, SocketFlags.None, this.receive, null);
			}
		}
		~Client()
		{
			try
			{
				this.disconnect();
			}
			catch { }
		}

		// Public Methods
		public void connect(string host, ushort port)
		{
			if (!this.Connected)
			{
				this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				if (this.bufferReceived == null)
				{
					this.bufferReceived = new byte[this.socket.ReceiveBufferSize];
				}

				try
				{
					this.socket.BeginConnect(host, port, this.connectInternal, null);
				}
				catch (SocketException exc)
				{
					base.callEvent(delegate { this.ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs { Exception = exc }); });
				}
			}
		}
		public void disconnect()
		{
			if (this.Connected)
			{
				this.socket.Close();
			}
		}
		public void send(object obj)
		{
			if (this.Connected)
			{
				byte[] bytes = this.serializeObject(obj);

				this.socket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, (ar) =>
				{
					try
					{
						int sendByteCount = this.socket.EndSend(ar);
						this.callEvent(delegate { this.SendSuccessfull?.Invoke(this, EventArgs.Empty); });
					}
					catch (Exception exc)
					{
						base.callEvent(delegate { this.ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs { Exception = exc }); });
					}

				}, null);
			}
		}
		public void sendSync(object obj)
		{
			if (this.Connected)
			{
				byte[] bytes = this.serializeObject(obj);

				try
				{
					int sendByteCount = this.socket.Send(bytes, SocketFlags.None);
					this.callEvent(delegate { this.SendSuccessfull?.Invoke(this, EventArgs.Empty); });
				}
				catch (Exception exc)
				{
					base.callEvent(delegate { this.ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs { Exception = exc }); });
				}
			}
		}

		// Private Methods
		private byte[] serializeObject(object obj)
		{
			BinaryFormatter bf = new BinaryFormatter();
			using (MemoryStream ms = new MemoryStream())
			{
				bf.Serialize(ms, obj);

				byte[] sizeBytes = BitConverter.GetBytes(ms.Length);
				byte[] bytes = new byte[sizeof(long) + ms.Length];
				Array.Copy(sizeBytes, 0, bytes, 0, sizeBytes.Length);
				Array.Copy(ms.ToArray(), 0, bytes, sizeBytes.Length, ms.Length);

				return bytes;
			}
		}
		private void processReceivedBytes(int byteCount, int offset = 0)
		{
			// Fancy algorithm to read multiple packets from stream ( ͡° ͜ʖ ͡°)
			if (this.readMode == ReadMode.ObjectSize)
			{
				int objectSizeBytesLeft = (int)(sizeof(long) - this.objectSizeBytes.Length);

				if (byteCount >= objectSizeBytesLeft)
				{
					this.objectSizeBytes.Write(this.bufferReceived, offset, objectSizeBytesLeft);

					this.finalObjectSize = BitConverter.ToInt64(this.objectSizeBytes.ToArray(), 0);
					this.objectSizeBytes.Seek(0, SeekOrigin.Begin);
					this.objectSizeBytes.SetLength(0);

					offset += objectSizeBytesLeft;
					byteCount -= objectSizeBytesLeft;

					this.readMode = ReadMode.Object;

					if (byteCount > 0)
					{
						this.processReceivedBytes(byteCount, offset);
					}
				}
				else
				{
					this.objectSizeBytes.Write(this.bufferReceived, offset, byteCount);
				}
			}
			else if (this.readMode == ReadMode.Object)
			{
				int objectBytesLeft = (int)(this.finalObjectSize - this.objectSizeBytes.Length);

				if (byteCount >= objectBytesLeft)
				{
					this.objectBytes.Write(this.bufferReceived, offset, byteCount);

					BinaryFormatter bf = new BinaryFormatter();
					this.objectBytes.Seek(0, SeekOrigin.Begin);
					object obj = bf.Deserialize(this.objectBytes);

					base.callEvent(delegate { this.ObjectReceived?.Invoke(this, new ObjectReceivedEventArgs { Object = obj }); });

					this.finalObjectSize = 0;
					this.readMode = ReadMode.ObjectSize;
					this.objectBytes.SetLength(0);

					offset += objectBytesLeft;
					byteCount -= objectBytesLeft;

					if (byteCount > 0)
					{
						this.processReceivedBytes(byteCount, offset);
					}
				}
				else
				{
					this.objectBytes.Write(this.bufferReceived, offset, byteCount);
				}
			}
		}
		private void connectInternal(IAsyncResult ar)
		{
			try
			{
				this.socket.EndConnect(ar);

				base.callEvent(delegate { this.ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs { Connected = true, Lost = false }); });

				this.socket.BeginReceive(this.bufferReceived, 0, this.bufferReceived.Length, SocketFlags.None, this.receive, null);
			}
			catch (Exception exc)
			{
				base.callEvent(delegate { this.ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs { Exception = exc }); });
			}
		}
		private void receive(IAsyncResult ar)
		{
			try
			{
				int receivedByteCount = this.socket.EndReceive(ar);
				if (receivedByteCount > 0)
				{
					this.processReceivedBytes(receivedByteCount);
					this.socket.BeginReceive(this.bufferReceived, 0, this.bufferReceived.Length, SocketFlags.None, this.receive, null);
				}
				else
				{
					this.socket.Close();
					base.callEvent(delegate { this.ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs { Connected = false, Lost = true }); });
				}
			}
			catch (SocketException)
			{
				base.callEvent(delegate { this.ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs { Connected = false, Lost = true }); });
			}
			catch (ObjectDisposedException)
			{
				base.callEvent(delegate { this.ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs { Connected = false, Lost = false }); });
			}
			catch (Exception exc)
			{
				base.callEvent(delegate { this.ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs { Exception = exc }); });
			}
		}
    }
}