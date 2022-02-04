using System;
using System.Collections.Generic;
using System.Text;

namespace CommunicationChannel
{
	public static class Protocol
	{
		public enum Command : byte
		{
			ConnectionEstablished = 0,
			DataReceivedConfirmation = 1,
			Ping = 2,
			SetNewpost = 3,
			Messages = 4,
		}
	}
}
