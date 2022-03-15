using System;

namespace EncryptedMessaging
{
    /// <summary>
    /// Configuration functions of setting message saving and deleting based on user input.
    /// </summary>
    public class Setting
    {
        /// <summary>
        /// Load the settigs of the chats.
        /// </summary>
        /// <param name="context">Context</param>
        public Setting(Context context)
        {
            _context = context;
            Load();
        }
        private readonly Context _context;

        private int _postPersistenceDays;

        /// <summary>
        /// Number of days messages must be kept in memory before being automatically deleted
        /// </summary>
        public int PostPersistenceDays
        {
            get => _postPersistenceDays;
            set { _postPersistenceDays = value; _context.SecureStorage.Values.Set("PostPersistenceDays", value); }
        }

        private int _keepPosts;
        /// <summary>
        /// Number of messages to be saved for each chat
        /// </summary>
        public int KeepPost
        {
            get => _keepPosts;
            set { _keepPosts = value; _context.SecureStorage.Values.Set("KeepPosts", value); }
        }

        private int _messagePagination;
        /// <summary>
        /// Number of messages for each chat page: The chat is divided into pages to speed up the loading and not to weigh down the memory
        /// </summary>
        public int MessagePagination
        {
            get => _messagePagination;
            set { _messagePagination = value; _context.SecureStorage.Values.Set("MessagePagination", value); }
        }

        private void Load()
        {
            _postPersistenceDays = _context.SecureStorage.Values.Get("PostPersistenceDays", 365);
            _keepPosts = _context.SecureStorage.Values.Get("KeepPosts", 1000);
            _messagePagination = _context.SecureStorage.Values.Get("MessagePagination", 30);
        }

    }
}
