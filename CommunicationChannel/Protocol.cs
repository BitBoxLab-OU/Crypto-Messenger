using System;
using System.Collections.Generic;
using System.Text;

namespace CommunicationChannel
{
	/// <summary>
	/// 
	/// </summary>
	public static class Protocol
	{
		/// <summary>
		/// Defines the protocols for the communication channel
		/// </summary>
		public enum Command : byte
		{
			/// <summary>
			/// 0 represents that connection is established
			/// </summary>
			ConnectionEstablished = 0,
			/// <summary>
			/// 1 represents that data is recieved by server
			/// </summary>
			DataReceivedConfirmation = 1,
			/// <summary>
			/// 2 represents that server is pinged
			/// </summary>
			Ping = 2,
			/// <summary>
			/// 3 represents that new post is set
			/// </summary>
			SetNewpost = 3,
			/// <summary>
			/// 4 represents messages
			/// </summary>
			Messages = 4,
		}
	}
}
