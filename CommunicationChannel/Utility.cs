using System;
using System.Net;
using System.Net.Sockets;

namespace CommunicationChannel
{
	/// <summary>
	///  This is an utility class which provides method to converting data, hashing data or get neccesity information.
	/// </summary>
	public static class Utility
	{
		/// <summary>
		/// Converts the Byte array to Uintegers.
		/// </summary>
		/// <param name="data"> Byte Array </param>
		/// <returns>Integer</returns>
		public static uint DataId(byte[] data) => BitConverter.ToUInt32(DataIdBinary(data), 0);
		/// <summary>
		/// Return Byte Array from DataId.
		/// </summary>
		/// <param name="data">Byte Array</param>
		/// <returns>Byte Array</returns>
		public static byte[] DataIdBinary(byte[] data)
		{
			return data?.Length < 4
				? data.Combine(new byte[4]).Take(4)
				: data.Skip(data.Length - 4);
		}
		/// <summary>
		/// To Hash the data.
		/// </summary>
		/// <param name="data">Byte Array</param>
		/// <returns></returns>
		public static byte[] FastHash(byte[] data)
		{
			void xor(byte[] a, byte[] b)
			{
				for (int i = 0; i < a.Length; i++)
					a[i] ^= b[i];
			}
			var result = BitConverter.GetBytes(data.Length).Combine(new byte[28]);
			if (data.Length < 32)
				data = data.Combine(new byte[32]).Take(32);
			var start = data.Take(32);
			var end = data.Skip(data.Length - 32).Take(32);
			end = end.Reverse();
			xor(result, start);
			xor(result, end);
			return result;
		}
		/// <summary>
		/// Resolves an IP address to an IPHostEntry instance.
		/// </summary>
		/// <returns>IP</returns>
		public static string GetLocalIPAddress()
		{
			var host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (var ip in host.AddressList)
			{
				if (ip.AddressFamily == AddressFamily.InterNetwork)
				{
					return ip.ToString();
				}
			}
			throw new Exception("No network adapters with an IPv4 address in the system!");
		}
	}
}
