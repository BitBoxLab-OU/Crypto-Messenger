using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CommunicationChannel;

namespace EncryptedMessaging
{
	/// <summary>
	/// This class is used for converting contacts to public keys.
	/// </summary>
	public class ContactConverter
	{
		/// <summary>
		/// Set the context to readonly.
		/// </summary>
		/// <param name="context">Context</param>
		public ContactConverter(Context context) => _context = context;
		private readonly Context _context;

		/// <summary>
		/// From the public key he obtains the user ID, a unique number represented by 8 bytes (ulong)
		/// For privacy reasons this algorithm is not reversible: From the public key we can obtain the user ID but it is not possible to trace the public key by having the user ID
		/// </summary>
		/// <param name="publicKey"></param>
		/// <returns></returns>
		public static ulong GetUserId(byte[] publicKey)
		{
			var hashBytes = CryptoServiceProvider.ComputeHash(publicKey);
			var bit64 = hashBytes.Take(8);
			//var r = Converter.BytesToUlong(bit64);
			//return r;
			return Converter.BytesToUlong(bit64);
		}

		/// <summary>
		/// This function obtains the list of participants from a string that represents everyone's public key
		/// </summary>
		/// <param name="publicKeys">string that represents everyone's public key</param>
		/// <param name="participants">participants in the group</param>
		/// <returns>Boolean</returns>



		public bool PublicKeysToParticipants(string publicKeys, out List<byte[]> participants)
		{
			participants = new List<byte[]>();
			try
			{
				var keyLen = 44;
				if (publicKeys.Length == 0 || (publicKeys.Length % keyLen) != 0)
					return false;
				var nParticipants = publicKeys.Length / keyLen;
				var keys = new List<string>();
				for (var n = 0; n < nParticipants; n++)
				{
					keys.Add(publicKeys.Substring(keyLen * n, keyLen));
				}
				foreach (var key in keys)
					participants.Add(Convert.FromBase64String(key));
			}
			catch (Exception)
			{
				return false;
			}
			NormalizeParticipants(ref participants);
			return true;
		}
		
		/// <summary>
		/// Boolean check for validating key.
		/// </summary>
		/// <param name="participants">Partipants</param>
		/// <param name="publicKeys">Public Key</param>
		/// <param name="removeMyKey">Remove Key</param>
		/// <returns></returns>
		public bool ParticipantsToPublicKeys(List<byte[]> participants, out string publicKeys, bool removeMyKey = false)
		{
			var participantsClone = participants.ToList(); // We use a clone to prevent errors on other threads interacting with the collection at the same time
			NormalizeParticipants(ref participantsClone, removeMyKey);
			publicKeys = "";
			foreach (var participant in participantsClone)
			{
				var key = Convert.ToBase64String(participant);
				if (ValidateKey(key))
					publicKeys += key;
				else
					return false;
			}
			return true;
		}

		/// <summary>
		/// Calculate the hash id of the contact. For groups, the name also comes into play in the computation because there can be groups with the same participants but different names
		/// </summary>
		/// <param name="participants">Partipants</param>
		/// <param name="name">The name parameter must only be passed for groups, because there are groups with the same members but different names</param>
		/// <returns>Unisgned Integer</returns>
		public static ulong ParticipantsToChatId(List<byte[]> participants, string name)
		{
			var participantsClone = participants.ToList(); // So there is no error if the list is changed externally during the sort process
			participantsClone?.Sort(new Functions.ByteListComparer());
			var pts = Array.Empty<byte>();
			if (participantsClone.Count > 2)
				pts = name.GetBytes();
			participantsClone.ForEach((x) => pts = pts.Combine(x));
			var hashBytes = CryptoServiceProvider.ComputeHash(pts);
			return Converter.BytesToUlong(hashBytes.Take(8));
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="participants"></param>
		/// <returns></returns>
		public static bool ValidateKeys(List<byte[]> participants)
		{
			if (participants == null)
				return false;
			try
			{
				using (var csp = new CryptoServiceProvider())
				{
					foreach (var key in participants)
					{
						csp.ImportCspBlob(key);
					}
				}
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// Boolean check for partipants public keys.
		/// </summary>
		/// <param name="keys">Key</param>
		/// <returns>Boolean</returns>
		public bool ValidateKeys(string keys) => PublicKeysToParticipants(keys, out var participants) && ValidateKeys(participants);

		/// <summary>
		/// This will check if the basekey is valid , if not key is converted from base 64 key.
		/// </summary>
		/// <param name="base64Key"></param>
		/// <returns>key</returns>
		public static bool ValidateKey(string base64Key)
		{
			if (base64Key == null)
				return false;
			else if (base64Key.Length != 44)
				return false;
			try
			{
				var key = Convert.FromBase64String(base64Key);
				return ValidateKey(key);
			}
			catch (FormatException ex)
			{
				Debug.WriteLine(ex.Message);
				return false;
			}
		}

		/// <summary>
		/// Validates the key provided.
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public static bool ValidateKey(byte[] key)
		{
			if (key == null)
				return false;
			try
			{
				using (var csp = new CryptoServiceProvider())
				{
					csp.ImportCspBlob(key);
					return csp.IsValid();
				}
			}
			catch (Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// Remove they key if it is not null and assign a new Public key Binary for empty values.
		/// </summary>
		/// <param name="participants">Participants</param>
		/// <param name="removeMyKey">Byte array</param>
		public void NormalizeParticipants(ref List<byte[]> participants, bool removeMyKey = false)
		{
			var myKey = _context.My.GetPublicKeyBinary();
			var isMy = participants.Find(x => x.SequenceEqual(myKey));
			if (removeMyKey)
			{
				if (isMy != null)
					participants.Remove(isMy);
			}
			else
			{
				if (isMy == null)
					participants.Add(myKey);
			}
			participants.Sort(new Functions.ByteListComparer());
		}

		/// <summary>
		/// Change the partipants to their specific User Ids.
		/// </summary>
		/// <param name="participants">Partipants</param>
		/// <param name="context">Context</param>
		/// <returns>User Id</returns>
		public static List<ulong> ParticipantsToUserIds(List<byte[]> participants, Context context)
		{
			var participantsClone = participants.ToList(); // We use a clone to prevent errors on other threads interacting with the collection at the same time
			context.ContactConverter.NormalizeParticipants(ref participantsClone);
			var list = new List<ulong>();
			foreach (var participant in participantsClone)
				list.Add(GetUserId(participant));
			return list;
		}
	}
}