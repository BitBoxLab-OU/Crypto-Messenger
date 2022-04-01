using NBitcoin;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using EncryptedMessaging.Resources;
using System.Security.Cryptography;
using System.IO;

namespace EncryptedMessaging
{
	/// <summary>
	/// 
	/// </summary>
	public static class Functions
	{

		/// <summary>
		/// encrypt the user input for the password.
		/// </summary>
		/// <param name="input">user input</param>
		/// <param name="password">Byte array</param>
		/// <returns></returns>
		public static byte[] Encrypt(byte[] input, byte[] password)
		{
			var pdb = new PasswordDeriveBytes(password, new byte[] { 0x43, 0x87, 0x23, 0x72 });
			var ms = new MemoryStream();
			var aes = new AesManaged();
			aes.Key = pdb.GetBytes(aes.KeySize / 8);
			aes.IV = pdb.GetBytes(aes.BlockSize / 8);
			var cs = new CryptoStream(ms,
				aes.CreateEncryptor(), CryptoStreamMode.Write);
			cs.Write(input, 0, input.Length);
			cs.Close();
			return ms.ToArray();
		}

		/// <summary>
		/// Decrypt the password
		/// </summary>
		/// <param name="input">User input</param>
		/// <param name="password">Byte array</param>
		/// <returns></returns>
		public static byte[] Decrypt(byte[] input, byte[] password)
		{
			var pdb = new PasswordDeriveBytes(password, new byte[] { 0x43, 0x87, 0x23, 0x72 });
			var ms = new MemoryStream();
			var aes = new AesManaged();
			aes.Key = pdb.GetBytes(aes.KeySize / 8);
			aes.IV = pdb.GetBytes(aes.BlockSize / 8);
			var cs = new CryptoStream(ms,
				aes.CreateDecryptor(), CryptoStreamMode.Write);
			cs.Write(input, 0, input.Length);
			cs.Close();
			return ms.ToArray();
		}

		/// <summary>
		/// Compare the two byte list and return the result.
		/// </summary>
		public class ByteListComparer : IComparer<IList<byte>>
		{
			/// <summary>
			/// 
			/// </summary>
			/// <param name="x"></param>
			/// <param name="y"></param>
			/// <returns></returns>
			public int Compare(IList<byte> x, IList<byte> y)
			{
				for (var index = 0; index < Math.Min(x.Count, y.Count); index++)
				{
					var result = x[index].CompareTo(y[index]);
					if (result != 0) return result;
				}
				return x.Count.CompareTo(y.Count);
			}
		}


		static public int BytesCompare(IList<byte> x, IList<byte> y)
		{
			for (var index = 0; index < Math.Min(x.Count, y.Count); index++)
			{
				var result = x[index].CompareTo(y[index]);
				if (result != 0) return result;
			}
			return x.Count.CompareTo(y.Count);
		}

		/// <summary>
		/// Set the date to relative  if same return null.
		/// </summary>
		/// <param name="date">Instant in time</param>
		/// <returns></returns>
		public static string DateToRelative(DateTime date)
		{
			if (date == DateTime.MinValue)
				return null;
			var distance = Time.CurrentTimeGMT - date;
			return distance.TotalDays >= 2 ? ((int)distance.TotalDays).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Dictionary.Days + " " + Dictionary.Ago
					 : distance.TotalDays >= 1 ? ((int)distance.TotalDays).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Dictionary.Day + " " + Dictionary.Ago
					 : distance.TotalHours >= 2 ? ((int)distance.TotalHours).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Dictionary.Hours + " " + Dictionary.Ago
					 : distance.TotalHours >= 1 ? ((int)distance.TotalHours).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Dictionary.Hour + " " + Dictionary.Ago
					 : distance.TotalMinutes >= 5 ? ((int)distance.TotalMinutes).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Dictionary.Minutes + " " + Dictionary.Ago
					 : distance.TotalMinutes >= 0 ? Dictionary.JustNow : null;
		}

		/// <summary>
		/// split the data and create new.
		/// </summary>
		/// <param name="data">Combined packages</param>
		/// <param name="offset">The offset where to start</param>
		/// <param name="pointer"></param>
		/// <returns></returns>
		public static List<byte[]> SplitDataWithZeroEnd(byte[] data, int offset, out int pointer)
		{
			var datas = new List<byte[]>();
			int len = data[offset];
			do
			{
				offset += 1;
				var part = new byte[len];
				Buffer.BlockCopy(data, offset, part, 0, len);
				datas.Add(part);
				offset += len;
				if (offset < data.Length)
				{
					len = data[offset];
					if (len == 0)
						offset++;
				}
				else
				{
					len = 0;
				}
			} while (len != 0);
			pointer = offset;
			return datas;
		}


		/// <summary>
		/// Divide merged data packets with join function
		/// </summary>
		/// <param name="data">Combined packages</param>
		/// <param name="lenAsByte">Use the same value used with the join function</param>
		/// <param name="offset">The offset where to start</param>
		/// <returns></returns>
		public static List<byte[]> SplitData(byte[] data, bool lenAsByte = true, int offset = 0)
		{
			var datas = new List<byte[]>();
			while (offset < data.Length)
			{
				int len;
				if (lenAsByte)
				{
					len = data[offset];
					offset++;
				}
				else
				{
					len = BitConverter.ToInt32(data, offset);
					offset += 4;
				}
				var part = new byte[len];
				Buffer.BlockCopy(data, offset, part, 0, len);
				datas.Add(part);
				offset += len;
			}
			return datas;
		}


		/// <summary>
		/// Join data packets
		/// </summary>
		/// <param name="lenAsByte">If true, packets must be smaller than 256 bytes</param>
		/// <param name="values">packages to join</param>
		/// <returns></returns>
		public static byte[] JoinData(bool lenAsByte, params byte[][] values)
		{
			var data = Array.Empty<byte>();
			foreach (var value in values)
			{
#if DEBUG
				if (lenAsByte && value.Length >= 256)
					System.Diagnostics.Debugger.Break(); // It is not allowed to send data greater than 255 bytes
#endif
				data = lenAsByte ? data.Combine(new byte[] { (byte)value.Length }, value) : data.Combine(value.Length.GetBytes(), value);
			}
			return data;
		}

		/// <summary>
		/// Vaidate the passphrase, if wrong return false.
		/// </summary>
		/// <param name="passphrase"></param>
		/// <returns></returns>
		public static bool PassphraseValidation(string passphrase)
		{
			try
			{
				passphrase = passphrase.Trim();
				passphrase = passphrase.Replace(",", " ");
				passphrase = System.Text.RegularExpressions.Regex.Replace(passphrase, @"\s+", " ");
				var words = passphrase.Split(' ');
				if (words.Length >= 12)
				{
					passphrase = passphrase.ToLower();
					var mnemo = new Mnemonic(passphrase, Wordlist.English);
					return mnemo.IsValidChecksum;
				}
				else if (words.Length == 1)
				{
					return (Convert.FromBase64String(passphrase).Length == 32);
				}
			}
			catch (Exception) { }
			return false;

		}

		/// <summary>
		/// Converts the value of a specified Unicode character to its uppercase equivalent using specified culture-specific formatting information.
		/// </summary>
		/// <param name="text"></param>
		/// <returns> The uppercase equivalent of c, modified according to culture, or the unchanged value of c if c is already uppercase, has no uppercase equivalent, or is notalphabetic.</returns>
		public static string FirstUpper(string text)
		{
			var value = "";
			if (string.IsNullOrEmpty(text)) return value;
			var last = false;
			foreach (var c in text)
			{
				if (char.IsLetter(c))
				{
					if (!last)
						value += char.ToUpper(c, System.Globalization.CultureInfo.InvariantCulture);
					else
						value += c;
					last = true;
				}
				else
				{
					last = false;
					value += c;
				}
			}
			return value;
		}


		/// <summary>
		/// Split arrays of incoming data
		/// </summary>
		/// <param name="data">Array of data to split</param>
		/// <param name="smallValue">If true, the format supports values no larger than 256 bytes</param>
		/// <returns>Key value collection</returns>
		public static Dictionary<byte, byte[]> SplitIncomingData(byte[] data, bool smallValue)
		{
			var values = SplitData(data, smallValue);
			var keyValue = new Dictionary<byte, byte[]>();
			if (values?.Count > 0)
			{
				//Read key value
				var n = 0;
				do
				{
					var key = values[n][0];
					n++;
					var value = values[n];
					n++;
					keyValue.Add(key, value);
				} while (n < values.Count);
			}
			return keyValue;
		}

		/// <summary>
		/// Convert byte array to its equivalent string representation that is encoded with uppercase hex characters.
		/// </summary>
		/// <param name="ba"></param>
		/// <returns></returns>
		public static string ToHex(this byte[] ba)
		{
			var hex = new StringBuilder(ba.Length * 2);
			foreach (var b in ba)
				hex.AppendFormat("{0:x2}", b);
			return hex.ToString();
		}

		/// <summary>
		/// Convert hex character to its equivalent byte array.
		/// </summary>
		/// <param name="hex"></param>
		/// <returns></returns>
		public static byte[] HexToBytes(this string hex)
		{
			var NumberChars = hex.Length;
			var bytes = new byte[NumberChars / 2];
			for (var i = 0; i < NumberChars; i += 2)
				bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
			return bytes;
		}

		/// <summary>
		/// Convert byte array to base 64 url.
		/// </summary>
		/// <param name="bytes"></param>
		/// <returns></returns>
		public static string ToBase64Url(this byte[] bytes)
		{
			var returnValue = Convert.ToBase64String(bytes).TrimEnd(padding).Replace('+', '-').Replace('/', '_');
			return returnValue;
		}

		/// <summary>
		/// Convert from base 64 url to byte array
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public static byte[] FromBase64Url(this string text)
		{
			var incoming = text.Replace('_', '/').Replace('-', '+');
			switch (text.Length % 4)
			{
				case 2: incoming += "=="; break;
				case 3: incoming += "="; break;
			}
			var bytes = Convert.FromBase64String(incoming);
			//string originalText = Encoding.ASCII.GetString(bytes);
			return bytes;
		}

		private static readonly char[] padding = { '=' };

		/// <summary>
		/// Convert byte to firebase token
		/// </summary>
		/// <param name="bytes"></param>
		/// <returns></returns>
		public static string BitesToFirebaseToken(byte[] bytes)
		{
			var array1 = new byte[bytes.Length - 105];
			var array2 = new byte[105];
			Array.Copy(bytes, 0, array1, 0, bytes.Length - 105);
			Array.Copy(bytes, bytes.Length - 105, array2, 0, 105);
			var part1 = ToBase64Url(array1);
			part1 = part1.Substring(0, part1.Length - 1);
			var part2 = ToBase64Url(array2);
			var result = part1 + ":" + part2;
			return result;
		}

		/// <summary>
		/// Convert firebase token to byte array.
		/// </summary>
		/// <param name="token"></param>
		/// <returns></returns>
		public static byte[] FirebaseTokenToBytes(string token)
		{
			var parts = token.Split(':');
			var part1 = parts[0];
			var part2 = parts[1];
			var array1 = FromBase64Url(part1 + "0");
			var array2 = FromBase64Url(part2);
			var result = new byte[array1.Length + array2.Length];
			array1.CopyTo(result, 0);
			array2.CopyTo(result, array1.Length);
			return result;
		}


		private class MyWebClient : WebClient
		{
			protected override WebRequest GetWebRequest(Uri uri)
			{
				var w = base.GetWebRequest(uri);
				w.Timeout = 5 * 1000;
				return w;
			}
		}

		private static bool PingHost(Uri address)
		{

			try
			{
				using (var client = new MyWebClient())
				using (client.OpenRead(address))
					return true;
			}
			catch
			{
				return false;
			}

			// this version don't work in iOS
			//var pingable = false;
			//Ping pinger = null;
			//try
			//{
			//	pinger = new Ping();
			//	PingReply reply = pinger.Send(nameOrAddress);
			//	pingable = reply.Status == IPStatus.Success;
			//}
			//catch (PingException)
			//{
			//	// Discard PingExceptions and return false;
			//}
			//finally
			//{
			//	if (pinger != null)
			//	{
			//		pinger.Dispose();
			//	}
			//}
			//return pingable;
		}

		private static bool _pingDisallow = false;
		internal static void TrySwitchOnConnectivityByPing(Uri serverUri)
		{
			if (Context.InternetAccess == false && _pingDisallow == false)
			{
				if (PingHost(serverUri))
					Context.OnConnectivityChange(true);
				else
					_pingDisallow = true;
			}
			if (Context.InternetAccess)
				_pingDisallow = false;
		}
	}
}
