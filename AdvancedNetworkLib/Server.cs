/*
	MIT License
	
	Copyright (c) 2018 marcelweski
	
	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:
	
	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.
	
	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
	SOFTWARE.
*/

using System;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Collections.Generic;

namespace AdvancedNetworkLib
{
	public class Server : Server<object> { }

	public class Server<T> : Base
	{
		// Fields
		private List<Client<T>> clients;

		// Properties
		public ushort Port { get => this.socket != null ? Convert.ToUInt16(((IPEndPoint)this.socket.LocalEndPoint).Port) : (ushort)0; }
		public bool Listening { get; private set; }
		public IEnumerable<Client<T>> Clients
		{
			get
			{
				foreach (var c in this.clients)
				{
					yield return c;
				}
			}
		}
		public string LocalHostname { get => Dns.GetHostName(); }
		public string[] LocalIPAddresses
		{
			get
			{
				var host = Dns.GetHostEntry(Dns.GetHostName());
				List<string> ips = new List<string>(host.AddressList.Length);
				foreach (var ip in host.AddressList)
				{
					if (ip.AddressFamily == AddressFamily.InterNetwork)
					{
						ips.Add(ip.ToString());
					}
				}
				return ips.ToArray();
			}
		}

		// Events
		public event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;
		public event EventHandler<ObjectReceivedEventArgs> ObjectReceived;
		public event EventHandler<ClientConnectionChangedEventArgs<T>> ClientsChanged;
		public event EventHandler<StateChangedEventArgs> StateChanged;

		public Server(Control control = null) : base(control)
		{
			this.Listening = false;
			this.clients = new List<Client<T>>();
		}
		~Server()
		{
			try
			{
				this.Stop();
			}
			catch { }
		}

		// Public Methods
		public void Start(ushort port, int backlog = 100)
		{
			if (!this.Listening)
			{
				try
				{
					this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					this.socket.Bind(new IPEndPoint(IPAddress.Any, port));
					this.socket.Listen(backlog);
					this.socket.BeginAccept(this.Accept, null);

					this.Listening = true;
					base.CallEvent(delegate { this.StateChanged?.Invoke(this, new StateChangedEventArgs { Listening = this.Listening }); });
				}
				catch (Exception exc)
				{
					base.CallEvent(delegate { this.ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs { Exception = exc }); });
				}
			}
		}
		public void Stop()
		{
			if (this.Listening)
			{
				foreach (var c in this.clients)
				{
					c.Disconnect();
				}
				this.socket.Close();
			}
		}
		public void SendToAll(object obj)
		{
			foreach (var client in this.clients)
				client.Send(obj);
		}

		// Private Methods
		private void Accept(IAsyncResult ar)
		{
			try
			{
				Socket socketClient = this.socket.EndAccept(ar);

				var client = new Client<T>(socketClient, base.control);
				client.ConnectionChanged += this.RemoveClient;
				client.ErrorOccurred += (s, e) => base.CallEvent(delegate { this.ErrorOccurred?.Invoke(s, e); });
				client.ObjectReceived += (s, e) => base.CallEvent(delegate { this.ObjectReceived?.Invoke(s, e); });

				lock (this.clients)
				{
					this.clients.Add(client);
				}

				base.CallEvent(delegate { this.ClientsChanged?.Invoke(this, new ClientConnectionChangedEventArgs<T> { Client = client, Connected = true, Lost = false }); });

				this.socket.BeginAccept(this.Accept, null);
			}
			catch (ObjectDisposedException)
			{
				this.Listening = false;
				base.CallEvent(delegate { this.StateChanged?.Invoke(this, new StateChangedEventArgs { Listening = this.Listening }); });
			}
			catch (Exception exc)
			{
				base.CallEvent(delegate { this.ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs { Exception = exc }); });
			}
		}
		private void RemoveClient(object sender, ConnectionChangedEventArgs e)
		{
			var client = sender as Client<T>;

			lock (this.clients)
			{
				this.clients.Remove(client);
			}

			base.CallEvent(delegate { this.ClientsChanged?.Invoke(this, new ClientConnectionChangedEventArgs<T> { Client = client, Connected = false, Lost = e.Lost }); });
		}
	}
}