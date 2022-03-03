using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;

namespace CommunicationChannel
{
	/// <summary>
	/// 
	/// </summary>
	public class AntiDuplicate
	{
		/// <summary>
		/// 
		/// </summary>
		public AntiDuplicate()
		{
			Load();
		}
		private readonly List<byte[]> HashList = new List<byte[]>();
		private const string HashFile = "posthashs.bin";
		private void Load()
		{
			if (Channel.IsoStoreage.FileExists(HashFile))
				using (var stream = new IsolatedStorageFileStream(HashFile, FileMode.Open, FileAccess.Read, Channel.IsoStoreage))
					for (int i = 0; i < (int)stream.Length; i += 4)
					{
						var data = new byte[stream.Length];
						stream.Read(data, i, 4);
						HashList.Add(data);
					}
		}
		/// <summary>
		///	
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public bool AlreadyReceived(byte[] data)
		{
			var alreadyReceived = false;
			var hash = Utility.FastHash(data);
			lock (HashList)
			{
				foreach (var item in HashList)
				{
					if (Bytes.SequenceEqual(hash, item))
					{
						alreadyReceived = true;
						break;
					}
				}
				if (!alreadyReceived)
				{
					if (HashList.Count >= 20)
						HashList.RemoveAt(0);
					HashList.Add(hash);
				}
			}
			if (!alreadyReceived)
				Save();
			return alreadyReceived;
		}
		private void Save()
		{
			lock (HashList)
				using (var stream = new IsolatedStorageFileStream(HashFile, FileMode.Create, FileAccess.Write, Channel.IsoStoreage))
					foreach (byte[] item in HashList)
						stream.Write(item, 0, 4);
		}
	}
}
