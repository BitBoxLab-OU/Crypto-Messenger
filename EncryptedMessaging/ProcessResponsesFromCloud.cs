using System.Text;

namespace EncryptedMessaging
{
	static class ProcessResponsesFromCloud
	{
		internal static void OnResponse(Context context, string objectName, string key, byte[] data)
		{
#if !DEBUG
			try
			{
#endif
			if (key.Length == 0)
			{
				var dataCollection = Functions.SplitData(data, false);
				foreach (var datax in dataCollection)
					OnData(context, objectName, key, datax);
			}
			else
				OnData(context, objectName, key, data);
#if !DEBUG
			}
			catch (System.Exception ex)
			{
			}
#endif
		}

		private static void OnData(Context context, string objectName, string key, byte[] data)
		{
			data = SecureStorage.Cryptography.Decrypt(data, context.My.Csp.ExportCspBlob(true));
			if (objectName == "Contact")
			{
				var contactMessage = new ContactMessage(data);				
				context.Contacts.AddContact(contactMessage, Contacts.SendMyContact.SendNamelessForUpdate);
			}
			else if (objectName == "String")
			{
				if (key == "MyName")
					context.My.Name = Encoding.Unicode.GetString(data);
			}
		}
	}
}
