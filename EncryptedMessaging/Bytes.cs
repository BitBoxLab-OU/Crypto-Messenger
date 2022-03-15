using System;
using CommunicationChannel;

namespace EncryptedMessaging
{
	/// <summary>
	/// This class is used for combining and converting Byte array based on the input.
	/// </summary>
	public static class Bytes
	{
		/// <summary>
		/// Combines the byte array with the item in the element 
		/// </summary>
		/// <param name="me">Byte</param>
		/// <param name="element">Array</param>
		/// <returns>First</returns>
		public static byte[] Combine(this byte[] me, params byte[][] element)
		{
			var first = (byte[])me.Clone();
			foreach (var item in element)
			{
				first = first.Combine(item);
			}
			return first;
		}

		/// <summary>
		/// Combines the Byte wuth Byte array.
		/// </summary>
		/// <param name="me">Byte</param>
		/// <param name="byteArray"> Byte array </param>
		/// <returns>Byte</returns>
		public static byte[] Combine(this byte[] me, byte[] byteArray)
		{
			var ret = new byte[me.Length + byteArray.Length];
			Buffer.BlockCopy(me, 0, ret, 0, me.Length);
			Buffer.BlockCopy(byteArray, 0, ret, me.Length, byteArray.Length);
			return ret;
		}

		/// <summary>
		/// Convert and copy byte array to a numeric representation.
		/// </summary>
		/// <param name="source">Byte source</param>
		/// <param name="length">numeric value</param>
		/// <returns>Byte length</returns>
		public static byte[] Take(this byte[] source, int length)
		{
			var result = new byte[length];
			Array.Copy(source, result, length);
			return result;
		}

		/// <summary>
		/// Copies a specified number of bytes from a source array starting at a particular offset to a destination array starting at a particular offset.
		/// </summary>
		/// <param name="source">Source array</param>
		/// <param name="offset">range</param>
		/// <returns>Result</returns>

		public static byte[] Skyp(this byte[] source, int offset)
		{
			var result = new byte[source.Length - offset];
			Buffer.BlockCopy(source, offset, result, 0, result.Length);
			return result;
		}

		/// <summary>
		/// Compare the source byte array with the length.
		/// </summary>
		/// <param name="source">source byte array</param>
		/// <param name="compareTo"> byte array </param>
		/// <returns>Boolean</returns>
		public static bool SequenceEqual(this byte[] source, byte[] compareTo)
		{
			if (compareTo.Length != source.Length)
				return false;
			for (var i = 0; i < source.Length; i++)
				if (source[i] != compareTo[i])
					return false;
			return true;
		}

		/// <summary>
		/// Returns the specified integer value as an array of bytes.		
		/// </summary>
		/// <param name="value">integer</param>
		/// <returns>Byte array</returns>
		public static byte[] GetBytes(this int value) => Converter.GetBytes(value);

		/// <summary>
		/// Returns the specified unsigned integer value as an array of bytes.
		/// </summary>
		/// <param name="value">unsigned integer</param>
		/// <returns>Byte array</returns>
		public static byte[] GetBytes(this uint value) => Converter.GetBytes(value);

		/// <summary>
		/// Returns the specified 64-bit signed integer value as an array of bytes.
		/// </summary>
		/// <param name="value"> 64-bit signed integer</param>
		/// <returns>>Byte array</returns>
		public static byte[] GetBytes(this long value) => Converter.GetBytes(value);

		/// <summary>
		/// Returns the specified 64-bit unsigned integer value as an array of bytes.
		/// </summary>
		/// <param name="value"> 64-bit unsigned integer</param>
		/// <returns>>Byte array</returns>
		public static byte[] GetBytes(this ulong value) => Converter.GetBytes(value);

	}
}
