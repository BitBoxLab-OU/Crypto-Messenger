using System;

namespace EncryptedMessaging
{
	public class Setting
	{
		public Setting(Context context)
		{
			_context = context;
			Load();
		}
		private readonly Context _context;

		private int _postPersistenceDays;
		public int PostPersistenceDays
		{
			get => _postPersistenceDays;
			set { _postPersistenceDays = value; _context.Storage.SaveObject(_postPersistenceDays, "PostPersistenceDays"); }
		}

		private int _keepPosts;
		public int KeepPost
		{
			get => _keepPosts;
			set { _keepPosts = value; _context.Storage.SaveObject(_keepPosts, "KeepPosts"); }
		}

		private bool _usePseudonim;
		public bool UsePseudonim
		{
			get => _usePseudonim;
			set { _usePseudonim = value; _context.Storage.SaveObject(_usePseudonim, "UsePseudonim"); }
		}

		private void Load()
		{
				var persistenceDays = _context.Storage.LoadObject(typeof(int), "PostPersistenceDays");
				_postPersistenceDays = persistenceDays != null ? (int)persistenceDays : 7;
				var keepPosts = _context.Storage.LoadObject(typeof(int), "KeepPosts");
				_keepPosts = keepPosts != null ? (int)keepPosts : 30;
		}

	}
}
