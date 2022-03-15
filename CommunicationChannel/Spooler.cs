using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading;

namespace CommunicationChannel
{
	/// <summary>
	/// This class is used for saving, updating the data in the queue list.
	/// </summary>
	internal class Spooler
	{
		internal Spooler(Channel channell)
		{
			_channell = channell;
			_queueListName = "ql" + _channell.MyId.ToString();
			_queueName = "q" + _channell.MyId.ToString() + "-";
			LoadUnsendedData();
		}
		private readonly Channel _channell;
		private string _queueListName;
		private string _queueName;
		private const bool _persistentQuee = true;


		private void LoadUnsendedData()
		{
			var datas = new List<byte[]>();
			lock (_inQuee)
			{
				if (_persistentQuee && Channel.IsoStoreage.FileExists(_queueListName))
				{
					using (var stream = new IsolatedStorageFileStream(_queueListName, FileMode.Open, FileAccess.Read, Channel.IsoStoreage))
					{
						while (stream.Position < stream.Length)
						{
							var dataInt = new byte[4];
							stream.Read(dataInt, 0, 4);
							var progressive = BitConverter.ToInt32(dataInt, 0);
							if (Channel.IsoStoreage.FileExists(_queueName + progressive))
							{
								using (var stream2 = new IsolatedStorageFileStream(_queueName + progressive, FileMode.Open, FileAccess.Read, Channel.IsoStoreage))
								{
									var data = new byte[stream2.Length];
									stream2.Read(data, 0, (int)stream2.Length);
									datas.Add(data);
								}
								Channel.IsoStoreage.DeleteFile(_queueName + progressive);
							}
						}
					}
					Channel.IsoStoreage.DeleteFile(_queueListName);
				}
			}
			foreach (var data in datas)
				AddToQuee(data);
		}

		private int _progressive;
		private readonly List<Tuple<uint, int>> _inQuee = new List<Tuple<uint, int>>();  // Tuple<int, int> = Tuple<idData, progressive>
		/// <summary>
		/// Add the data to the spooler Queue.
		/// </summary>
		/// <param name="data">byte array</param>
		public void AddToQuee(byte[] data)
		{
			//_channell.Tcp.Connect();
			Queue.Add(data);
			if (_persistentQuee)
			{
				lock (_inQuee)
				{
					_inQuee.Add(Tuple.Create(Utility.DataId(data), _progressive));
					using (var stream = new IsolatedStorageFileStream(_queueName + _progressive, FileMode.Create, FileAccess.Write, Channel.IsoStoreage))
						stream.Write(data, 0, data.Length);
					_progressive += 1;
					SaveQueelist();
				}
			}
			if (Queue.Count == 1) //if the Queue is empty, the spooler is stopped, then re-enable the spooler
				SendNext(false);
		}

		/// <summary>
		/// Remove the data that from the spooler Queue.
		/// </summary>
		/// <param name="dataId"> Data id</param>
		public void RemovePersistent(uint dataId)
		{
			if (_persistentQuee)
			{
				lock (_inQuee)
				{
					Tuple<uint, int> toRemove = _inQuee.Find(x => x.Item1 == dataId);
					if (toRemove != null)
					{
						var progressive = toRemove.Item2;
						_inQuee.Remove(toRemove);
						if (Channel.IsoStoreage.FileExists(_queueName + progressive))
							Channel.IsoStoreage.DeleteFile(_queueName + progressive);
						SaveQueelist();
					}
				}
			}
		}

		private void SaveQueelist()
		{
			using (var stream = new IsolatedStorageFileStream(_queueListName, FileMode.Create, FileAccess.Write, Channel.IsoStoreage))
				foreach (Tuple<uint, int> item in _inQuee)
					stream.Write(item.Item2.GetBytes(), 0, 4);
		}
		/// <summary>
		/// On send completed it remove the sent packet and insert in the spooler queue before closing the communication channnel.
		/// </summary>
		/// <param name="data">data</param>
		/// <param name="ex">exception</param>
		/// <param name="connectionIsLost">connection status</param>
		public void OnSendCompleted(byte[] data, Exception ex, bool connectionIsLost)
		{
			if (ex != null)
				_channell.Tcp.InvokeError(connectionIsLost ? Tcp.ErrorType.LostConnection : Tcp.ErrorType.SendDataError, ex.Message);
			if (connectionIsLost)
			{
#if DEBUG
				_sent.Remove(data);
#endif
				Queue.Insert(0, data);
				_channell.Tcp.Disconnect();
			}
		}
		internal List<Tuple<uint, Action>> ExecuteOnConfirmReceipt = new List<Tuple<uint, Action>>();
		/// <summary>
		/// Confirm the receipt status on the sent data before sending the next message
		/// </summary>
		/// <param name="dataId"> data ID</param>
		public void OnConfirmReceipt(uint dataId)
		{
			lock (_channell.Tcp.DataAwaitingConfirmation)
			{
				_channell.Tcp.DataAwaitingConfirmation.Clear();
				_channell.Tcp.SendTimeOut.Change(Timeout.Infinite, Timeout.Infinite);
			}
			Action action = null;
			lock (ExecuteOnConfirmReceipt)
			{
				Tuple<uint, Action> tuple = ExecuteOnConfirmReceipt.Find(x => x.Item1 == dataId);
				if (tuple != null)
				{
					ExecuteOnConfirmReceipt.Remove(tuple);
					action = tuple.Item2;
				}
			}
			action?.Invoke();
			RemovePersistent(dataId);
			_channell.OnDataDeliveryConfirm?.Invoke(dataId);
			SendNext(); //Upon receipt confirmation, sends the next message
		}
#if DEBUG
		private readonly List<byte[]> _sent = new List<byte[]>();
#endif
		internal void SendNext(bool pause = true)
		{
			if (_channell.Tcp.Logged)
			{
				if (_channell.Tcp.IsConnected() && Queue.Count > 0)
				{
					var data = Queue[0];
					Queue.RemoveAt(0);

#if DEBUG
					if (_sent.Contains(data))
						System.Diagnostics.Debugger.Break(); // send duplicate message!!
					_sent.Add(data);
#endif
					if (pause)
						Thread.Sleep(1000);
					_channell.Tcp.ExecuteSendData(data);
				}
			}
		}
		internal int QueeCount => Queue.Count;
		internal readonly List<byte[]> Queue = new List<byte[]>();
	}

}