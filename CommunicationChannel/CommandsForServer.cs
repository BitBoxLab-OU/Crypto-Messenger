
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CommunicationChannel
{
	/// <summary>
	/// This class handle the commands to be executed at the server level.
	/// </summary>
	public class CommandsForServer
	{
		internal CommandsForServer(Channel channell) => _channell = channell;
		private readonly Channel _channell;
		internal void SendCommandToServer(Protocol.Command command, byte[] dataToSend = null, ulong? chatId = null, ulong? myId = null, bool directlyWithoutSpooler = false)
		{
			if (!_channell.Tcp.IsConnected())
				_channell.Tcp.Connect();
			var data = CreateCommand(command, dataToSend, chatId, myId);
			if (directlyWithoutSpooler)
				_channell.Tcp.ExecuteSendData(data, directlyWithoutSpooler: directlyWithoutSpooler);  // Send directly without using the spooler
			else
				_channell.Tcp.SendData(data);                                                         // Send data using the spooler
		}

		internal byte[] CreateCommand(Protocol.Command command, byte[] dataToSend = null, ulong? chatId = null, ulong? myId = null)
		{
			var data = new byte[] { (byte)command }; // 1 byte
			if (myId != null)
			{
				// ConnectionEstablished: command [0], domainId [1][2][3][4], senderId [5][6][7][8][9][10][11][12]
				data = data.Combine(Converter.GetBytes(_channell.Domain)); // 4 byte
				data = data.Combine(Converter.GetBytes((ulong)myId)); // 8 byte
			}
			// command [0] chatId [1][2][3][4][5][6][7][8], data [0..]
			if (chatId != null)
				data = data.Combine(Converter.GetBytes((ulong)chatId)); // 8 byte
			if (dataToSend != null)
				data = data.Combine(dataToSend);
			return data;
		}

		/// <summary>
		/// Send data to the server.
		/// </summary>
		/// <param name="chatId">chat to which data belong to</param>
		/// <param name="dataToSend">data</param>
		/// <param name="directlyWithoutSpooler"> if you want to send directly without spooler make it true else false </param>
		public void SendPostToServer(ulong chatId, byte[] dataToSend, bool directlyWithoutSpooler = false) => SendCommandToServer(Protocol.Command.SetNewpost, dataToSend, chatId, directlyWithoutSpooler: directlyWithoutSpooler);

		//public static void Connect(ulong myId)
		//{
		//	Tcp.Connect(myId, ServerAddress);
		//}

		/// <summary>
		/// Confirmation that data is recieved at the server side.
		/// </summary>
		/// <param name="dataReceived"> data to recieve confirmation </param>
		public void DataReceivedConfirmation(byte[] dataReceived) => SendCommandToServer(Protocol.Command.DataReceivedConfirmation, Utility.DataIdBinary(dataReceived), directlyWithoutSpooler: true);

	}
}