using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedNetworkLib
{
	public class ErrorOccurredEventArgs : EventArgs
	{
		public Exception Exception { get; set; }
	}

	public class ObjectReceivedEventArgs : EventArgs
	{
		public object Object { get; set; }
	}

	public class ConnectionChangedEventArgs : EventArgs
	{
		public bool Connected { get; set; }
		public bool Lost { get; set; }
	}

	public class StateChangedEventArgs : EventArgs
	{
		public bool Listening { get; set; }
	}

	public class ClientsChangedEventArgs : EventArgs
	{
		public IEnumerable<Client> Clients { get; set; }
	}

	public class PublicIPEventArgs : EventArgs
	{
		public string IP { get; set; }
	}
}
