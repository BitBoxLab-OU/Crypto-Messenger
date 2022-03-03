using System;
using CommunicationChannel;

namespace EncryptedMessaging
{
	/// <summary>
	/// 
	/// </summary>
	public static class Bytes
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="me"></param>
		/// <param name="element"></param>
		/// <returns></returns>
		public static byte[] Combine(this byte[] me, params byte[][] element)
		{
			var first = (byte[])me.Clone();
			foreach (var item in element)
			{
				first = first.Combine(item);
			}
			return first;
		}

		public static byte[] Combine(this byte[] me, byte[] byteArray)
		{
			var ret = new byte[me.Length + byteArray.Length];
			Buffer.BlockCopy(me, 0, ret, 0, me.Length);
			Buffer.BlockCopy(byteArray, 0, ret, me.Length, byteArray.Length);
			return ret;
		}

		public static byte[] Take(this byte[] source, int length)
		{
			var result = new byte[length];
			Array.Copy(source, result, length);
			return result;
		}

		public static byte[] Skyp(this byte[] source, int offset)
		{
			var result = new byte[source.Length - offset];
			Buffer.BlockCopy(source, offset, result, 0, result.Length);
			return result;
		}

		public static bool SequenceEqual(this byte[] source, byte[] compareTo)
		{
			if (compareTo.Length != source.Length)
				return false;
			for (var i = 0; i < source.Length; i++)
				if (source[i] != compareTo[i])
					return false;
			return true;
		}

		public static byte[] GetBytes(this int value) => Converter.GetBytes(value);

		public static byte[] GetBytes(this uint value) => Converter.GetBytes(value);

		public static byte[] GetBytes(this long value) => Converter.GetBytes(value);

		public static byte[] GetBytes(this ulong value) => Converter.GetBytes(value);

	}
}
