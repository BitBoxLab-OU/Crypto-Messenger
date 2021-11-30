using System;

namespace EncryptedMessaging
{
	public class Storage
	{
		public Storage(Context context) => _context = context;
		private readonly Context _context;
		public  string SaveObject(object obj, string key) => _context.SecureStorage.ObjectStorage.SaveObject(obj, key);
		public object LoadObject(Type type, string key) => _context.SecureStorage.ObjectStorage.LoadObject(type, key);
	}
}


