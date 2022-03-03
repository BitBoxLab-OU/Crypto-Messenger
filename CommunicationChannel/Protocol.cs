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
		/// 
		/// </summary>
		public enum Command : byte
		{
			/// <summary>
			/// 
			/// </summary>
			ConnectionEstablished = 0,
			/// <summary>
			/// 
			/// </summary>
			DataReceivedConfirmation = 1,
			/// <summary>
			/// 
			/// </summary>
			Ping = 2,
			/// <summary>
			/// 
			/// </summary>
			SetNewpost = 3,
			/// <summary>
			/// 
			/// </summary>
			Messages = 4,
		}
	}
}
