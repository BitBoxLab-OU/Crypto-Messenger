using System;
using System.Collections.Generic;

namespace CommunicationChannel
{
	/// <summary>
	/// This class converts the data from one format to another as per the task requirement.
	/// </summary>
	public static class Converter
	{
		/// <summary>
		/// convert DateTime to unix timestamp
		/// </summary>
		/// <param name="dateTime">DateTime</param>
		/// <returns>unix timestamp</returns>
		public static int ToUnixTimestamp(DateTime dateTime) => (int)dateTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
		/// <summary>
		/// convert unix timestamp to DateTime
		/// </summary>
		/// <param name="timestamp">unix timestamp</param>
		/// <returns>DateTime</returns>
		public static DateTime FromUnixTimestamp(int timestamp) => new DateTime(1970, 1, 1).AddSeconds(timestamp);
		///<inheritdoc cref="FromUnixTimestamp(int)"/>
		public static DateTime FromUnixTimestamp(byte[] timestamp4Bytes) => new DateTime(1970, 1, 1).AddSeconds(BitConverter.ToInt32(timestamp4Bytes, 0));
		/// <summary>
		/// convert IP address into unsigned int.
		/// </summary>
		/// <param name="ip">IP address</param>
		/// <returns>IP as unsigned int</returns>
		public static uint IpToUint(string ip)
		{
			var address = System.Net.IPAddress.Parse(ip);
			var bytes = address.GetAddressBytes();
			return BytesToUint(bytes);
		}
		/// <summary>
		/// converts back unsigned int into IP address.
		/// </summary>
		/// <param name="ip">Converted IP address</param>
		/// <returns>IP address</returns>
		public static string UintToIp(uint ip)
		{
			var bytes = GetBytes(ip);
			return new System.Net.IPAddress(bytes).ToString();
		}
		private const string _base36CharList = "0123456789abcdefghijklmnopqrstuvwxyz";
		/// <summary>
		/// get Base36CharList
		/// </summary>
		public static string Base36CharList => _base36CharList;

		/// <summary>
		/// Encode the given number into a Base36 string
		/// </summary>
		/// <param name="input">number to convert</param>
		/// <returns> Base36 Encoded string </returns>
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
		/// <param name="input">Base36 Encoded string</param>
		/// <returns>number</returns>
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
		/// <summary>
		/// Convert the given string into a Base64 string
		/// </summary>
		/// <param name="text">text</param>
		/// <returns> Base64 string</returns>
		public static string StringToBase64(string text)
		{
			// This function is a quick way to crypt a text string
			var bytes = StringToByteArray(text);
			return Convert.ToBase64String(bytes);
		}

		/// <summary>
		/// Convert the given Base64 string into a string
		/// </summary>
		/// <param name="text">Base64 string</param>
		/// <returns>string</returns>
		public static string Base64ToString(string text)
		{
			// Now easy to decrypt a data
			var bytes = Convert.FromBase64String(text);
			return ByteArrayToString(bytes);
		}

		/// <summary>
		/// Convert string to Byte array
		/// </summary>
		/// <param name="text">string</param>
		/// <returns>byte array</returns>
		public static byte[] StringToByteArray(string text) => !string.IsNullOrEmpty(text) ? System.Text.Encoding.GetEncoding("utf-16LE").GetBytes(text) : null;
		/// <summary>
		/// convert byte array to string
		/// </summary>
		/// <param name="bytes"> byte array </param>
		/// <returns>string</returns>
		public static string ByteArrayToString(byte[] bytes) => System.Text.Encoding.GetEncoding("utf-16LE").GetString(bytes);// Unicode encoding
		/// <summary>
		/// Convert XML to Object
		/// </summary>
		/// <param name="xml"> xml string</param>
		/// <param name="type"> type of xml serializer</param>
		/// <param name="obj">converted xml </param>
		/// <returns>True or False</returns>
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
		/// <summary>
		/// Convert Object to XML
		/// </summary>
		/// <param name="obj"> converted xml as object</param>
		/// <returns>xml string</returns>
		public static string ObjectToXml(object obj)
		{
			var str = new System.IO.StringWriter();
			var xml = new System.Xml.Serialization.XmlSerializer(obj.GetType());
			var xmlns = new System.Xml.Serialization.XmlSerializerNamespaces();
			xmlns.Add(string.Empty, string.Empty);
			xml.Serialize(str, obj, xmlns);
			return str.ToString();
		}
		/// <summary>
		/// Convert int to byte array
		/// </summary>
		/// <param name="n">int</param>
		/// <returns> byte array </returns>
		public static byte[] GetBytes(int n) => BitConverter.IsLittleEndian ? BitConverter.GetBytes(n) : BitConverter.GetBytes(n).Reverse(); // flip big-endian(network order) to little-endian
		///<inheritdoc cref="GetBytes(int)"/>
		public static byte[] GetBytes(uint n) => BitConverter.IsLittleEndian ? BitConverter.GetBytes(n) : BitConverter.GetBytes(n).Reverse(); // flip big-endian(network order) to little-endian
		///<inheritdoc cref="GetBytes(int)"/>
		public static byte[] GetBytes(short n) => BitConverter.IsLittleEndian ? BitConverter.GetBytes(n) : BitConverter.GetBytes(n).Reverse(); // flip big-endian(network order) to little-endian
		///<inheritdoc cref="GetBytes(int)"/>
		public static byte[] GetBytes(ushort n) => BitConverter.IsLittleEndian ? BitConverter.GetBytes(n) : BitConverter.GetBytes(n).Reverse(); // flip big-endian(network order) to little-endian
		///<inheritdoc cref="GetBytes(int)"/>
		public static byte[] GetBytes(long n) => BitConverter.IsLittleEndian ? BitConverter.GetBytes(n) : BitConverter.GetBytes(n).Reverse(); // flip big-endian(network order) to little-endian
		///<inheritdoc cref="GetBytes(int)"/>
		public static byte[] GetBytes(ulong n) => BitConverter.IsLittleEndian ? BitConverter.GetBytes(n) : BitConverter.GetBytes(n).Reverse(); // flip big-endian(network order) to little-endian
		/// <summary>
		/// Convert byte array to unsigned int
		/// </summary>
		/// <param name="bytes">byte array</param>
		/// <returns>unsigned int</returns>
		public static uint BytesToUint(byte[] bytes) => BitConverter.ToUInt32(BitConverter.IsLittleEndian ? bytes : bytes.Reverse(), 0); // flip big-endian(network order) to little-endian
		/// <summary>
		/// Convert byte array to int
		/// </summary>
		/// <param name="bytes"></param>
		/// <returns> int </returns>
		public static int BytesToInt(byte[] bytes) => BitConverter.ToInt32(BitConverter.IsLittleEndian ? bytes : bytes.Reverse(), 0); // flip big-endian(network order) to little-endian
		/// <summary>
		/// Convert byte array to 16-bit unsigned int
		/// </summary>
		/// <param name="bytes">byte array</param>
		/// <returns>16-bit unsigned int</returns>
		public static uint BytesToUshort(byte[] bytes) => BitConverter.ToUInt16(BitConverter.IsLittleEndian ? bytes : bytes.Reverse(), 0); // flip big-endian(network order) to little-endian
		/// <summary>
		/// Convert byte array to 16-bit signed int
		/// </summary>
		/// <param name="bytes">byte array</param>
		/// <returns>16-bit signed int</returns>
		public static int BytesToShort(byte[] bytes) => BitConverter.ToInt16(BitConverter.IsLittleEndian ? bytes : bytes.Reverse(), 0); // flip big-endian(network order) to little-endian
		/// <summary>
		/// Convert byte array to 64-bit unsigned int
		/// </summary>
		/// <param name="bytes">byte array</param>
		/// <returns>64-bit unsigned int</returns>
		public static ulong BytesToUlong(byte[] bytes) => BitConverter.ToUInt64(BitConverter.IsLittleEndian ? bytes : bytes.Reverse(), 0); // flip big-endian(network order) to little-endian
		/// <summary>
		/// Convert byte array to 64-bit signed int
		/// </summary>
		/// <param name="bytes">byte array</param>
		/// <returns>64-bit signed int</returns>
		public static long BytesToLong(byte[] bytes) => BitConverter.ToInt64(BitConverter.IsLittleEndian ? bytes : bytes.Reverse(), 0); // flip big-endian(network order) to little-endian
	}
}