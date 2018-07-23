using System;
using System.Net.Sockets;
using System.Windows.Forms;

namespace AdvancedNetworkLib
{
	public class Base
	{
		// Variables
		protected Socket socket;
		protected Control control;

		public Base(Control control = null)
		{
			this.control = control;
		}

		protected void callEvent(MethodInvoker action)
		{
			if (this.control != null)
			{
				try
				{
					this.control.Invoke(action);
				}
				catch (ObjectDisposedException)
				{
					this.control = null;
				}
			}
			else
			{
				try
				{
					action();
				}
				catch (Exception exc)
				{
					Console.WriteLine(exc.StackTrace+"\n"+exc.Message);
				}
			}
		}
	}
}