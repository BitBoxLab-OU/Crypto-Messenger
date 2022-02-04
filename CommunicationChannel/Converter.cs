using System;
using System.Collections.Generic;

namespace CommunicationChannel
{
	public static class Converter
	{
		public static int ToUnixTimestamp(DateTime dateTime) => (int)dateTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
		public static DateTime FromUnixTimestamp(int timesatamp) => new DateTime(1970, 1, 1).AddSeconds(timesatamp);
		public static DateTime FromUnixTimestamp(byte[] timesatamp4Bytes) => new DateTime(1970, 1, 1).AddSeconds(BitConverter.ToInt32(timesatamp4Bytes, 0));

		public static uint IpToUint(string ip)
		{
			var address = System.Net.IPAddress.Parse(ip);
			var bytes = address.GetAddressBytes();
			return BytesToUint(bytes);
		}
		public static string UintToIp(uint ip)
		{
			var bytes = GetBytes(ip);
			return new System.Net.IPAddress(bytes).ToString();
		}
		private const string _base36CharList = "0123456789abcdefghijklmnopqrstuvwxyz";
		public static string Base36CharList => _base36CharList;

		/// <summary>
		/// Encode the given number into a Base36 string
		/// </summary>
		/// <param name="input"></param>
		/// <returns></returns>
		public static string Base36Encode(ulong input)
		{
			var clistarr = Base36CharList.ToCharArray();
			var result = new Stack<char>();
			while (input != 0)
			{
				result.Push(clistarr[input % 36]);
				input /= 36;
			}
			return new string(result.ToArray());
		}
		/// <summary>
		/// Decode the Base36 Encoded string into a number
		/// </summary>
		/// <param name="input"></param>
		/// <returns></returns>
		public static ulong Base36Decode(string input)
		{
			var charArray = input.ToLower().ToCharArray();
			Array.Reverse(charArray);
			IEnumerable<char> reversed = new string(charArray);
			ulong result = 0;
			var pos = 0;
			foreach (var c in reversed)
			{
				result += (ulong)Base36CharList.IndexOf(c) * (ulong)Math.Pow(36, pos);
				pos++;
			}
			return result;
		}
		public static string StringToBase64(string text)
		{
			// This function is a quick way to crypt a text string
			var bytes = StringToByteArray(text);
			return Convert.ToBase64String(bytes);
		}
		public static string Base64ToString(string text)
		{
			// Now easy to decrypt a data
			var bytes = Convert.FromBase64String(text);
			return ByteArrayToString(bytes);
		}
		public static byte[] StringToByteArray(string text) => !string.IsNullOrEmpty(text) ? System.Text.Encoding.GetEncoding("utf-16LE").GetBytes(text) : null;
		public static string ByteArrayToString(byte[] bytes) => System.Text.Encoding.GetEncoding("utf-16LE").GetString(bytes);// Unicode encoding
		public static bool XmlToObject(string xml, Type type, out object obj)
		{
			var xmlSerializer = new System.Xml.Serialization.XmlSerializer(type);
			try
			{
				obj = xmlSerializer.Deserialize(new System.IO.StringReader(xml));
				return true;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.Print(ex.Message);
				System.Diagnostics.Debugger.Break();
			}
			obj = null;
			return false;
		}
		public static string ObjectToXml(object obj)
		{
			var str = new System.IO.StringWriter();
			var xml = new System.Xml.Serialization.XmlSerializer(obj.GetType());
			var xmlns = new System.Xml.Serialization.XmlSerializerNamespaces();
			xmlns.Add(string.Empty, string.Empty);
			xml.Serialize(str, obj, xmlns);
			return str.ToString();
		}

		public static byte[] GetBytes(int n) => BitConverter.IsLittleEndian ? BitConverter.GetBytes(n) : BitConverter.GetBytes(n).Reverse(); // flip big-endian(network order) to little-endian

		public static byte[] GetBytes(uint n) => BitConverter.IsLittleEndian ? BitConverter.GetBytes(n) : BitConverter.GetBytes(n).Reverse(); // flip big-endian(network order) to little-endian

		public static byte[] GetBytes(short n) => BitConverter.IsLittleEndian ? BitConverter.GetBytes(n) : BitConverter.GetBytes(n).Reverse(); // flip big-endian(network order) to little-endian

		public static byte[] GetBytes(ushort n) => BitConverter.IsLittleEndian ? BitConverter.GetBytes(n) : BitConverter.GetBytes(n).Reverse(); // flip big-endian(network order) to little-endian

		public static byte[] GetBytes(long n) => BitConverter.IsLittleEndian ? BitConverter.GetBytes(n) : BitConverter.GetBytes(n).Reverse(); // flip big-endian(network order) to little-endian

		public static byte[] GetBytes(ulong n) => BitConverter.IsLittleEndian ? BitConverter.GetBytes(n) : BitConverter.GetBytes(n).Reverse(); // flip big-endian(network order) to little-endian

		public static uint BytesToUint(byte[] bytes) => BitConverter.ToUInt32(BitConverter.IsLittleEndian ? bytes : bytes.Reverse(), 0); // flip big-endian(network order) to little-endian

		public static int BytesToInt(byte[] bytes) => BitConverter.ToInt32(BitConverter.IsLittleEndian ? bytes : bytes.Reverse(), 0); // flip big-endian(network order) to little-endian

		public static uint BytesToUshort(byte[] bytes) => BitConverter.ToUInt16(BitConverter.IsLittleEndian ? bytes : bytes.Reverse(), 0); // flip big-endian(network order) to little-endian

		public static int BytesToShort(byte[] bytes) => BitConverter.ToInt16(BitConverter.IsLittleEndian ? bytes : bytes.Reverse(), 0); // flip big-endian(network order) to little-endian

		public static ulong BytesToUlong(byte[] bytes) => BitConverter.ToUInt64(BitConverter.IsLittleEndian ? bytes : bytes.Reverse(), 0); // flip big-endian(network order) to little-endian

		public static long BytesToLong(byte[] bytes) => BitConverter.ToInt64(BitConverter.IsLittleEndian ? bytes : bytes.Reverse(), 0); // flip big-endian(network order) to little-endian
	}
}