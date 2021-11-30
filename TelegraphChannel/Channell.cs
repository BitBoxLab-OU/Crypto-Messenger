using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.IsolatedStorage;
using System.Threading;

namespace CommunicationChannel
{
	public class Channell
	{
		/// <summary>
		/// Initialize the library
		/// </summary>
		/// <param name="connectivity"></param>
		/// <param name="internetAccess"></param>
		/// <param name="serverAddress"></param>
		/// <param name="connectionTimeout"></param>
		public Channell(string serverAddress, int domain, Action<ulong, byte[]> onMessageArrives, Action<uint> onDataDeliveryConfirm, ulong myId, int connectionTimeout = Timeout.Infinite)
		{
			MyId = myId;
			Domain = domain;
			ConnectionTimeout = connectionTimeout;
			Tcp = new Tcp(this);
			CommandsForServer = new CommandsForServer(this);
			Spooler = new Spooler(this);
			ServerUri = new UriBuilder(serverAddress).Uri; //new Uri(serverAddress);
			OnMessageArrives = onMessageArrives;
			OnDataDeliveryConfirm = onDataDeliveryConfirm;
		}
		internal static readonly IsolatedStorageFile IsoStoreage = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly | IsolatedStorageScope.Domain, null, null);
		internal AntiDuplicate AntiDuplicate = new AntiDuplicate();
		internal int ConnectionTimeout = Timeout.Infinite;
		internal ulong MyId;
		internal Spooler Spooler;
		internal Tcp Tcp;
		public CommandsForServer CommandsForServer;
		internal Action<uint> OnDataDeliveryConfirm; // uint parameter is dataId

		/// <summary>
		/// Use this command to re-establish the connection if it is disabled by the timer set with the initialization
		/// </summary>
		public static void ReEstablishConnection()
		{
			foreach (Tcp tcp in Tcp.Tcps)
			{
				lock (tcp.LockIsConnected)
				{
					tcp.Connect();
				}
			}
		}
		public bool IsConnected()
		{
			return Tcp.Client != null && Tcp.Client.Connected;
		}
		public readonly Uri ServerUri;
		public readonly int Domain;
		internal void OnDataReceives(byte[] incomingData, out Tuple<Tcp.ErrorType, string> error, bool directlyWithoutSpooler)
		{
			if (incomingData.Length == 0)
			{
				error = Tuple.Create(Tcp.ErrorType.WrondDataLength, null as string);
				return;
			}
			else
			{
				if (!Enum.IsDefined(typeof(Protocol.Command), incomingData[0]))
				{
					error = Tuple.Create(Tcp.ErrorType.CommandNotSupported, "Command id=" + incomingData[0]);
					return;
				}
				var inputType = (Protocol.Command)incomingData[0];
				if (inputType != Protocol.Command.DataReceivedConfirmation && directlyWithoutSpooler == false)
				{
					//Send a confirmation of data received to the server
					CommandsForServer.DataReceivedConfirmation(incomingData);
				}
				if (inputType == Protocol.Command.Messages)
				{
					var chatId = Converter.BytesToUlong(incomingData.Skip(1).Take(8));
					if (!SplitAllPosts(incomingData.Skip(9), out List<byte[]> posts))
					{
						error = Tuple.Create(Tcp.ErrorType.WrondDataLength, null as string);
						return;
					}
					PostCounter++;
					LastPostParts = posts.Count;
					posts.ForEach((post) =>
					{
						if (AntiDuplicate.AlreadyReceived(post))
							DuplicatePost++;
						else
							OnMessageArrives?.Invoke(chatId, post);
					});
				}
				else if (inputType == Protocol.Command.Ping)
				{
					Debug.WriteLine("ping received!");
				}
			}
			error = null;
			return;
		}
		internal void OnTcpError(Tcp.ErrorType errorId, string description)
		{
			//manage TCP error here
			Status = errorId;
			StatusDescription = description;
			//if (errorId != Tcp.ErrorType.Working)
			//	Debugger.Break();
			if (LogError)
			{
				ErrorLog += DateTime.UtcNow.ToString("MM/dd/yyyy HH:mm:ss") + " " + Status.ToString() + ": " + description + "\r\n";
				RefreshLogError?.Invoke(ErrorLog);
			}
		}

		public bool LogError = true; // Set this true if you want a ErrorLog

		//=========================== Data exposed for diagnostic use =====================================
		public event Action<string> RefreshLogError;
		internal string StatusDescription; //is multi line text 
		public bool ClientExists => Tcp.Client != null;
		public bool ClientConnected => Tcp.Client != null && Tcp.Client.Connected;
		public bool Logged => Tcp.Logged;
		public int QueeCount => Spooler.QueeCount;
		public int LastPostParts;
		public int PostCounter;
		public int DuplicatePost;
		public string ErrorLog;
		public ulong KeepAliveFailures { get; internal set; }
		//=================================================================================================


		private static bool _internetAccess;
		public static bool InternetAccess
		{
			get => _internetAccess;
			set
			{
				if (_internetAccess != value)
				{
					_internetAccess = value;
					if (_internetAccess)
						Tcp.Tcps.ForEach(tcp => tcp.Connect());
					else
						Tcp.Tcps.ForEach(tcp => tcp.Disconnect(false));
				}
			}
		}
		internal Tcp.ErrorType Status;
		internal Action<ulong, byte[]> OnMessageArrives;
		private static bool SplitAllPosts(byte[] data, out List<byte[]> posts)
		{
			posts = new List<byte[]>();
			var p = 0;
			if (data.Length > 0)
			{
				do
				{
					var len = Converter.BytesToInt(data.Skip(p).Take(4));
					p += 4;
					if (len + p > data.Length)
					{
						//Unexpected data length
						return false;
					}
					var post = new byte[len];
					Buffer.BlockCopy(data, p, post, 0, len);
					posts.Add(post); // post format: [1] version, [2][3][4][5] UNIX timestamp, [7] data type 
					p += len;
				} while (p < data.Length);
			}
			return p == data.Length;
		}
		//		public static void OnConnectivityChange(bool internetAccess) => InternetAccess = internetAccess;
	}
}