
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace EncryptedMessaging
{
	/// <summary>
	/// 
	/// </summary>
	public class ContactMessage
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="data">Byte array</param>
		public ContactMessage(byte[] data) => _initialize(data);

		/// <summary>
		/// Convert  the QR code to base 64 string.
		/// </summary>
		/// <param name="qrCode">QR code</param>
		public ContactMessage(string qrCode) => _initialize(Convert.FromBase64String(qrCode));

		/// <summary>
		/// Returns the contact represented by the input. If the input is invalid it will return null
		/// </summary>
		/// <param name="data">input byte array</param>
		/// <returns></returns>
		public static ContactMessage GetContactMessage(byte[] data)
		{
			try
			{
				return new ContactMessage(data);
			}
			catch (Exception ex)
			{
				Debug.Write(ex.Message);
				return null;
			}
		}

		/// <summary>
		/// Returns the contact represented by the input. If the input is invalid it will return null
		/// </summary>
		/// <param name="qrCode">input base 64</param>
		/// <returns></returns>
		public static ContactMessage GetContactMessage(string qrCode)
		{
			try
			{
				return new ContactMessage(qrCode);
			}
			catch (Exception ex)
			{
				Debug.Write(ex.Message);
				return null;
			}
		}

		private void _initialize(byte[] data)
		{
			// [D] = data length
			// [[D]contact name ] + [[D]name1,language1,key1] + [[D]name2,language2,key2] + [[D]name3,language3,key3]...
			var parts = Functions.SplitData(data);
			var n = 0;
			foreach (var part in parts)
			{
				if (n == 0)
				{
					if (part.Length == 1 && part[0] == 0)
						IsUpdate = true;
					else
						Name = Encoding.Unicode.GetString(part);
				}
				else
				{
					var properties = Functions.SplitData(part);
					var name = Encoding.Unicode.GetString(properties[0]);
					var os = Contact.RuntimePlatform.Undefined;
					var language = Encoding.ASCII.GetString(properties[1]);
					if (language.Length == 2)
					{
						var c1 = language[0];
						var c2 = language[1];
						if (char.IsUpper(c1) && char.IsLower(c2))
							os = Contact.RuntimePlatform.iOS;
						else if (char.IsLower(c1) && char.IsUpper(c2))
							os = Contact.RuntimePlatform.Android;
						else
							os = Contact.RuntimePlatform.Undefined;
					}
					var key = properties[2];
					string firebaseToken = null;
					if (properties.Count > 3 && properties[3].Length != 0)
						firebaseToken = Functions.BitesToFirebaseToken(properties[3]);
					string deviceToken = null;
					if (properties.Count > 4 && properties[4].Length != 0)
					{
						deviceToken = properties[4].ToHex();
						for (var i = 8; i <= deviceToken.Length; i += 8)
						{
							deviceToken = deviceToken.Insert(i, " ");
							i++;
						}
					}
					Participants.Add(new Properties() { Name = name, Language = language.ToLower(), Key = key, Os = os, FirebaseToken = firebaseToken, DeviceToken = deviceToken });
				}
				n++;
			}
			if (string.IsNullOrEmpty(Name) && Participants.Count() == 1)
				Name = Participants[0].Name;
		}

		/// <summary>
		/// Return the OS and language for a non-group contact
		/// </summary>
		/// <param name="context">Context</param>
		/// <param name="language">Language</param>
		/// <param name="os">Os</param>
		/// <param name="firebaseToken">Token Firebase</param>
		/// <param name="deviceToken">Device token for IOS</param>
		public void GetProperties(Context context, out string language, out Contact.RuntimePlatform os, out string firebaseToken, out string deviceToken)
		{
			if (Participants.Count <= 2)
			{
				var myKey = context.My.GetPublicKeyBinary();
				var participant = Participants.Find(x => !x.Key.SequenceEqual(myKey)); // Look for the contact other than me
				if (participant != null)
				{
					language = participant.Language;
					os = participant.Os;
					firebaseToken = participant.FirebaseToken;
					deviceToken = participant.DeviceToken;
					return;
				}
			}
			language = null;
			os = Contact.RuntimePlatform.Undefined;
			firebaseToken = null;
			deviceToken = null;
		}
		internal string OldName;
		private string _name;
		/// <summary>
		/// Update the name of the group.
		/// </summary>
		public string Name { get => _name; set { OldName = _name; _name = value; } }
		/// <summary>
		/// Boolean for update, default is set false.
		/// </summary>
		public bool IsUpdate = false;

		/// <summary>
		/// List for participants.
		/// </summary>
		public List<Properties> Participants = new List<Properties>();
		
		/// <summary>
		/// Strings used in this class.
		/// </summary>
		public class Properties
		{	
			/// <summary>
			/// NAme of the group.
			/// </summary>
			public string Name;
			
			/// <summary>
			/// Language.
			/// </summary>
			public string Language;
			
			/// <summary>
			/// Byte array for key.
			/// </summary>
			public byte[] Key;
			
			/// <summary>
			/// Operating system.
			/// </summary>
			public Contact.RuntimePlatform Os;
			
			/// <summary>
			/// Firebase Token.
			/// </summary>
			public string FirebaseToken;
			
			/// <summary>
			/// Device token for IOS.
			/// </summary>
			public string DeviceToken;
		}

		/// <summary>
		/// Get the keys of the particpant by converting the byte array input.
		/// </summary>
		/// <param name="context">Context</param>
		/// <returns>Keys</returns>
		public List<byte[]> GetParticipantsKeys(Context context)
		{
			var result = new List<byte[]>();
			Participants.ForEach(x => result.Add(x.Key));
			context.ContactConverter.NormalizeParticipants(ref result);
			return result;
		}

		/// <summary>
		/// Assign public keys to the partipants and set the firebase and device token for the users.
		/// </summary>
		/// <param name="contact">User contact</param>
		/// <param name="context">Context</param>
		/// <param name="addFirebaseTokken">Token Firebase</param>
		/// <param name="addDeviceTokken">Device Token of iOS</param>
		/// <param name="fullDataParticipant"></param>
		/// <param name="purposeIsUpdateOnly">Update contact</param>
		/// <returns></returns>
		public static byte[] GetDataMessageContact(Contact contact, Context context, bool addFirebaseTokken = true, bool addDeviceTokken = true, bool fullDataParticipant = true, bool purposeIsUpdateOnly = false)
		{
			// [D] = data length
			// [[D]contact name ] + [[D]name1,language1,key1] + [[D]name2,language2,key2] + [[D]name3,language3,key3]...

			var contactName = Array.Empty<byte>();
			if (purposeIsUpdateOnly)
			{
				contactName = new byte[] { 0 }; // Byte 0 in place of the name indicates that you intend to transmit an update of the properties of a contact, and not the contact to be saved in the address book
			}
			else
			{
				if (contact.Participants.Count > 2) // not (is I (my contact) OR I send a single contact (no group))
					contactName = Encoding.Unicode.GetBytes(contact.GetRealName() ?? "");
			}
			var data = Bytes.Combine(new byte[] { (byte)contactName.Length }, contactName);
			var myKey = context.My.GetPublicKeyBinary();

			contact.Participants.ForEach((participantKey) =>
			{
				var isMyContact = participantKey.SequenceEqual(myKey);
				if (participantKey.Count() != 2 || !isMyContact)
				{
					var name = Array.Empty<byte>();
					var language = Array.Empty<byte>();
					if (fullDataParticipant)
					{
						var subContact = contact.Participants.Count() == 1 ? contact : context.Contacts.ContactAlreadyExists(new List<byte[]>() { participantKey }, null);
						if (isMyContact && subContact == null)
							subContact = context.Contacts.GetMyContact();
						name = subContact != null ? Encoding.Unicode.GetBytes(subContact.GetRealName() ?? "") : Array.Empty<byte>();

						var lng = subContact != null ? subContact.Language : null;
						if (lng?.Length == 2)
						{
							var c1 = lng.Substring(0, 1);
							var c2 = lng.Substring(1, 1);
							if (subContact.Os == Contact.RuntimePlatform.iOS)
								c1 = c1.ToUpper();
							else if (subContact.Os == Contact.RuntimePlatform.Android)
								c2 = c2.ToUpper();
							lng = c1 + c2;
						}
						language = subContact != null ? (lng == null ? Array.Empty<byte>() : Encoding.ASCII.GetBytes(lng)) : Array.Empty<byte>();
					}
					var dataParticipant = Bytes.Combine(new byte[] { (byte)name.Length }, name);
					dataParticipant = dataParticipant.Combine(new byte[] { (byte)language.Length }, language);
					dataParticipant = dataParticipant.Combine(new byte[] { (byte)participantKey.Length }, participantKey);
					if (addFirebaseTokken && !string.IsNullOrEmpty(contact.FirebaseToken))
					{
						var firebaseTokken = Functions.FirebaseTokenToBytes(contact.FirebaseToken);
						dataParticipant = dataParticipant.Combine(new byte[] { (byte)firebaseTokken.Length }, firebaseTokken);
					}
					else
						dataParticipant = dataParticipant.Combine(new byte[] { 0 });
					if (addDeviceTokken && !string.IsNullOrEmpty(contact.DeviceToken))
					{
						var deviceTokken = contact.DeviceToken.Replace(" ", "").HexToBytes();
						dataParticipant = dataParticipant.Combine(new byte[] { (byte)deviceTokken.Length }, deviceTokken);
					}
					else
						dataParticipant = dataParticipant.Combine(new byte[] { 0 });
					data = data.Combine(new byte[] { (byte)dataParticipant.Length }, dataParticipant);
				}
			});
#if DEBUG
			// testing
			var contactMessage = new ContactMessage(data);
#endif
			return data;
		}

		/// <summary>
		/// Get the contact info of the other user from the base 64 string.
		/// </summary>
		/// <param name="contact">User contact</param>
		/// <param name="context">context</param>
		/// <returns></returns>
		public static string GetQrCode(Contact contact, Context context) => Convert.ToBase64String(GetDataMessageContact(contact, context));

		/// <summary>
		/// Get the contact onfo of the person using the app from base 64 string.
		/// </summary>
		/// <param name="context">context</param>
		/// <returns></returns>
		public static string GetMyQrCode(Context context) => Convert.ToBase64String(GetDataMessageContact(context.Contacts.GetMyContact(), context));

		/// <summary>
		/// Create new contact by using the QR code
		/// </summary>
		/// <param name="qrCode">QR code</param>
		/// <param name="context">Context</param>
		/// <param name="sendMyContact">Option to send my contact to the contact I add</param>
		public static void AddContact(string qrCode, Context context, Contacts.SendMyContact sendMyContact = Contacts.SendMyContact.Send)
		{
			var contactMessage = new ContactMessage(qrCode);
			context.Contacts.AddContact(contactMessage, sendMyContact);
		}

	}
}
