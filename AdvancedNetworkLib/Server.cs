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
using System.Text.RegularExpressions;

namespace AdvancedNetworkLib
{
	public class Server : Base
	{
		// Variables
		private List<Client> clients;

		// Properties
		public ushort Port { get => this.socket != null ? Convert.ToUInt16(((IPEndPoint)this.socket.LocalEndPoint).Port) : (ushort)0; }
		public bool Listening { get; private set; }
		public IEnumerable<Client> Clients
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
				List<string> ips = new List<string>();
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
		public event EventHandler<ClientsChangedEventArgs> ClientsChanged;
		public event EventHandler<StateChangedEventArgs> StateChanged;
		public event EventHandler<PublicIPEventArgs> PublicIPAddressLoaded;

		public Server(Control control = null) : base(control)
		{
			this.Listening = false;
			this.clients = new List<Client>();
		}
		~Server()
		{
			try
			{
				this.stop();
			}
			catch { }
		}

		// Public Methods
		public void start(ushort port, int backlog = 100)
		{
			if (!this.Listening)
			{
				try
				{
					this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					this.socket.Bind(new IPEndPoint(IPAddress.Any, port));
					this.socket.Listen(backlog);
					this.socket.BeginAccept(this.accept, null);

					this.Listening = true;
					base.callEvent(delegate { this.StateChanged?.Invoke(this, new StateChangedEventArgs { Listening = this.Listening }); });
				}
				catch (Exception exc)
				{
					base.callEvent(delegate { this.ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs { Exception = exc }); });
				}
			}
		}
		public void stop()
		{
			if (this.Listening)
			{
				foreach (var c in this.clients)
				{
					c.disconnect();
				}
				this.socket.Close();
			}
		}
		public void sendToAll(object obj)
		{
			foreach (Client client in this.clients)
				client.send(obj);
		}
		public void loadPublicIPAddress()
		{
			WebClient client = new WebClient();
			client.DownloadStringCompleted += (s, e) =>
			{
				try
				{
					var r = new Regex("title=\"copy ip address\">(.*?)</a>", RegexOptions.Multiline | RegexOptions.Compiled);
					this.callEvent(delegate { this.PublicIPAddressLoaded?.Invoke(this, new PublicIPEventArgs { IP = r.Match(e.Result).Groups[1].Value }); });
				}
				catch (Exception exc)
				{
					this.callEvent(delegate { this.ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs { Exception = exc }); });
				}
			};
			client.DownloadStringAsync(new Uri("http://myip.is"));
		}

		// Private Methods
		private void accept(IAsyncResult ar)
		{
			try
			{
				Socket socketClient = this.socket.EndAccept(ar);

				Client client = new Client(socketClient, base.control);
				client.ConnectionChanged += this.removeClient;
				client.ErrorOccurred += (s, e) => base.callEvent(delegate { this.ErrorOccurred?.Invoke(s, e); });
				client.ObjectReceived += (s, e) => base.callEvent(delegate { this.ObjectReceived?.Invoke(s, e); });

				lock (this.clients)
				{
					this.clients.Add(client);
				}

				base.callEvent(delegate { this.ClientsChanged?.Invoke(this, new ClientsChangedEventArgs { Clients = this.Clients }); });

				this.socket.BeginAccept(this.accept, null);
			}
			catch (ObjectDisposedException)
			{
				this.Listening = false;
				base.callEvent(delegate { this.StateChanged?.Invoke(this, new StateChangedEventArgs { Listening = this.Listening }); });
			}
			catch (Exception exc)
			{
				base.callEvent(delegate { this.ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs { Exception = exc }); });
			}
		}
		private void removeClient(object sender, ConnectionChangedEventArgs e)
		{
			Client client = sender as Client;

			lock (this.clients)
			{
				this.clients.Remove(client);
			}

			base.callEvent(delegate { this.ClientsChanged?.Invoke(this, new ClientsChangedEventArgs { Clients = this.Clients }); });
		}
	}
}