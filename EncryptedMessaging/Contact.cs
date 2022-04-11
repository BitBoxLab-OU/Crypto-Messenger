using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using static EncryptedMessaging.ContactConverter;

namespace EncryptedMessaging
{
    /// <summary>
    /// This class contains all the functionalities related to contact features.
    /// </summary>
    public class Contact : INotifyPropertyChanged
    {
        /// <summary>
        /// Checks if the chat initiated is between a single user or multiple partipants/group. 
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="participants">Users in the contacts</param>
        /// <param name="name">Name of the user</param>
        /// <param name="language">Application language</param>
        /// <param name="os">Operating system</param>
        /// <param name="firebaseToken">Token Firebase</param>
        /// <param name="deviceToken">Device Token of iOS</param>
        /// <param name="isServer">Is a server</param>
        public Contact(Context context, List<byte[]> participants, string name = null, string language = "en", RuntimePlatform os = RuntimePlatform.Undefined, string firebaseToken = null, string deviceToken = null, bool isServer = false)
        {
            _name = Functions.FirstUpper(name);
            Language = language;
            Os = os;
            FirebaseToken = firebaseToken;
            DeviceToken = deviceToken;
            IsServer = isServer;
            IsServer = isServer;
            var participantsClone = participants.ToList(); // We use a clone to prevent errors on other threads interacting with the collection at the same time
            Context = context;
            Context.ContactConverter.NormalizeParticipants(ref participantsClone);
            var removeMyKey = (participantsClone.Count != 1 || !participantsClone[0].SequenceEqual(context.My.GetPublicKeyBinary())) && participantsClone.Count <= 2;
            if (Context.ContactConverter.ParticipantsToPublicKeys(participantsClone, out var publicKeys, removeMyKey))
                PublicKeys = publicKeys; // This call also Initialize
            if (Context.My.IsServer)
                _sessionTimeout = new Timer(SessionExpired, this, (uint)Context.DefaultServerSessionTimeout.TotalMilliseconds, Timeout.Infinite);
        }
        /// <summary>
        /// It can be used by server applications to store data relating to this contact during a session. When the session expires, the Contact object is deleted and the session data will be deleted (the concept is similar to web application session data)
        /// </summary>
        [XmlIgnore]
        public Dictionary<string, object> Session = new Dictionary<string, object>();
        private readonly Timer _sessionTimeout;
        private void SessionExpired(object obj) => Context.Contacts.RemoveContact(this);
        internal void ExtendSessionTimeout()
        {
            _sessionTimeout?.Change((uint)Context.SessionTimeout.TotalMilliseconds, Timeout.Infinite);
        }
        internal void Initialize(Context context = null)
        {
            if (context != null)
                Context = context;
            if (!Context.ContactConverter.PublicKeysToParticipants(_publicKeys, out var participants)) return;
            Participants = participants;
            ChatId = ParticipantsToChatId(participants, Name);
            if (!IsGroup)
            {
                var myKey = Context.My.GetPublicKeyBinary();
                var contactKey = participants.Find(x => !x.SequenceEqual(myKey));
                if (contactKey != null)
                    UserId = GetUserId(contactKey);
                else if (myKey != null)
                    UserId = Context.My.Id;
            }
            Context.Contacts.Colors(Participants, out LightColor, out DarkColor);
        }

        private Contact()
        {
            //Empty constructor for serialization
        }
        internal Context Context;
        /// <summary>
        /// Represents the method that will handle the proporty changed event raised when a property when a property is changed on a component.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        internal string _name;
        /// <summary>
        /// Get the contact's real name (if set), otherwise return null
        /// </summary>
        /// <returns>Contact's real name if set</returns>
        public string GetRealName()
        {
            return string.IsNullOrEmpty(_name) || _name == Pseudonym() ? null : _name;
        }

        /// <summary>
        /// Get the contact's name (if set), otherwise return null.
        /// </summary>
        public string Name
        {
            get => string.IsNullOrEmpty(_name) ? Pseudonym() : _name;
            set
            {
                var newName = Functions.FirstUpper(value);
                if (newName == Name) return;
                if (Context != null && IsGroup)
                    Debugger.Break(); // The group name cannot be changed
                _name = newName;
                if (Context == null) return;
                OnPropertyChanged(nameof(Name));
                Context.Messaging.NotifyContactNameChange(this);// I send the name with which the contract is registered in my address book, so I can have notifications with the name used locally in my contacts
                Save(true);
            }
        }

        private string _myRemoteName;
        /// <summary>
        /// The name of my contact registered on the recipient's device
        /// </summary>
        public string MyRemoteName
        {
            get => _myRemoteName ?? Context.My.Name;
            set
            {
                if (Context != null && IsGroup)
                    Debugger.Break(); // The group name cannot be changed			
                if (_myRemoteName != value)
                {
                    _myRemoteName = value;
                    if (Context != null)
                        Save();
                }
            }
        }

        /// <summary>
        /// Represents an ARGB (alpha, red, green, blue) color.
        /// </summary>
        [XmlIgnore]

        public System.Drawing.Color LightColor;
        /// <summary>
        /// Represents an ARGB (alpha, red, green, blue) color.
        /// </summary>

        [XmlIgnore]
        public System.Drawing.Color DarkColor;
        /// <summary>
        /// Convert color to hex.
        /// </summary>

        [XmlIgnore]
        public string LightColorAsHex => $"#{LightColor.R:X2}{LightColor.G:X2}{LightColor.B:X2}";
        /// <summary>
        /// Convert color to hex.
        /// </summary>

        [XmlIgnore]
        public string DarkColorAsHex => $"#{DarkColor.R:X2}{DarkColor.G:X2}{DarkColor.B:X2}";

        /// <summary>
        /// Language used for this contact.
        /// </summary>
        public string Language; // 2 character to indicate the language used for this contact, for example EN, IT, etc..

        /// <summary>
        /// Operating system in use for runtime application.
        /// </summary>
        public RuntimePlatform Os;

        /// <summary>
        /// Base class for the runtime platform.
        /// </summary>
        public enum RuntimePlatform
        {   /// <summary>
            /// Undefined
            /// </summary>
            Undefined,

            /// <summary>
            /// Andriod
            /// </summary>
            Android,

            /// <summary>
            ///IOS 
            /// </summary>
            iOS,

            /// <summary>
            /// Windows
            /// </summary>
            Windows,

            /// <summary>
            /// Universal windows platform
            /// </summary>
            UWP,

            /// <summary>
            /// Unix
            /// </summary>
            Unix,

            /// <summary>
            /// Macintosh
            /// </summary>
            Mac
        }
        /// <summary>
        /// Set assumed Pseudonyms for the participants in the group.
        /// </summary>
        /// <returns></returns>
        public string Pseudonym() =>  Context?.Contacts.Pseudonym(Participants);
        private string _publicKeys;
        /// <summary>
        /// Check if public key exists
        /// </summary>
        public string PublicKeys
        {
            get => _publicKeys;
            set
            {
#if DEBUG
                if (!string.IsNullOrEmpty(_publicKeys))
                    Debugger.Break(); // It is not possible to change the public key when it is already assigned
                if (string.IsNullOrEmpty(value))
                {
                    Debugger.Break(); // It is not possible to pass a null value to this property, do a check before making the assignment
                    return;
                };

#endif
                _publicKeys = value.Trim();
                if (Context != null)
                {
                    // The object has been deserialized, so it is necessary to call initialize externally, in order to assign the context 
                    Initialize();
                }
            }
        }
        private DateTime LastPostReaded;
        /// <summary>
        /// Read the messages archived for this contact and view them in the chat view. The use of command in sequence will carry out the pagination (load the messages in blocks), to reset the pagination use the appropriate parameter.
        /// </summary>
        /// <param name="resetPaginate">Reset pagination, i.e. messages will be loaded from starting with the most recent message block (the first page of messages).</param>
        public void ReadPosts(bool resetPaginate = false)
        {
            if (resetPaginate)
                LastPostReaded = default;
            MessageContainerUI = null;
            if (Context.Messaging.MultipleChatModes)
            {
                LastPostReaded = Context.Repository.ReadPosts(ChatId, receprionAntecedent: LastPostReaded);
            }
        }
        /// <summary>
        /// Delegated function that is passed by the client application to back up a post
        /// </summary>
        /// <param name="chatId">Chat id</param>
        /// <param name="post">Encrypted post</param>
        /// <param name="receptionDate">Date of receipt of the post</param>
        /// <returns></returns>
        public delegate Action PostBackup(ulong chatId, byte[] post, DateTime receptionDate);

        /// <summary>
        /// Export all posts (encrypted in row format), useful for example for making a backup
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="exportAction">Action that will be performed for each post (If you want to back up put the backup execution code here)</param>
        /// <param name="exclude">List of posts to exclude using the received date as a filter. It is recommended to use this parameter to avoid exporting posts that have already been exported in the past</param>
        /// <param name="receprionAntecedent">If set, consider only posts that are dated before the value indicated. It is useful for paginating messages in the chat view, or for telling the loading of messages in blocks. How to use this parameter: You need to store the date of the oldest message that is displayed in the chat, when you want to load a second block of messages you have to pass this date in order to get the next block</param>
        /// <param name="take">Limit the number of messages to take (set this value to paginate messages in chunks), in case you don't want the whole message list. Pass null to process all posts, without any paging!</param>
        static public void ExportPosts(Context context, PostBackup exportAction, List<DateTime> exclude, DateTime receprionAntecedent = default, int? take = null)
        {
            context.Contacts.ForEachContact((contact) => contact.GetPosts((post, receptionDate) => exportAction(contact.ChatId, post, receptionDate), exclude, receprionAntecedent, take));
        }

        /// <summary>
        /// Read chat posts (encrypted in row format) for this contact or group (you can use this function to backup chat data)
        /// </summary>
        /// <param name="action">Action to be performed for each post, the byte[] is binary data of the encrypted post that is read from the repository, DateTime is the time the post was received which you can use as a unique ID (you can use the ticks property of DateTime as a unique id ) </param>
        /// <param name="exclude">List of posts to exclude using the received date as a filter. It is recommended to use this parameter to avoid exporting posts that have already been exported in the past</param>
        /// <param name="receprionAntecedent">If set, consider only posts that are dated before the value indicated. It is useful for paginating messages in the chat view, or for telling the loading of messages in blocks. How to use this parameter: You need to store the date of the oldest message that is displayed in the chat, when you want to load a second block of messages you have to pass this date in order to get the next block</param>
        /// <param name="take">Limit the number of messages to take (set this value to paginate messages in chunks), in case you don't want the whole message list. Pass null to process all posts, without any paging!</param>
        /// <returns>Returns the date of arrival of the oldest message processed by the function. Use this value to page further requests by passing the "receprionAntecedent" parameter</returns>
        public DateTime GetPosts(Action<byte[], DateTime> action, List<DateTime> exclude = null, DateTime receprionAntecedent = default, int? take = null)
        {
            if (take == null) // If null then get all messages
                take = Context.Setting.KeepPost;
            return Context.Repository.ReadPosts(ChatId, action, receprionAntecedent, take, exclude);
        }

        /// <summary>
        /// Manually set the post (it is recommended to use this function only for restoring data saved with the help of GetPosts)
        /// </summary>
        /// <param name="post">The encrypted post binary data to be saved in the repository</param>
        /// <param name="receptionDate">DateTime is the time the post was received which you can use as a unique ID (you can use the ticks property of DateTime as a unique id )</param>
        public void SetPost(byte[] post, DateTime receptionDate)
        {
            Context.Repository.AddPost(post, ChatId, ref receptionDate);
        }

        /// <summary>
        /// Returns to a specific action, the messages visible in the chat. Useful function for exporting messages.
        /// </summary>
        /// <param name="actionToExecuteForEachMessage">Action that is performed for each message (use this action to export or process messages).</param>
        /// <param name="exclude">List of id messages to exclude (The reception time is used as an identifier)</param>
        /// <param name="receprionAntecedent">Filter messages considering only those prior to a certain date (useful for paging messages in blocks)</param>
        /// <param name="take">Limit the number of messages to take. If not set, the value set in the Context.Setting.MessagePagination settings will be used. Pass the Context.Setting.KeepPost value to process all messages!</param>
        /// <returns>Returns the date of arrival of the oldest message processed by the function. Use this value to page further requests by passing the "receprionAntecedent" parameter.</returns>
        public DateTime GetMessages(Action<Message, bool> actionToExecuteForEachMessage, List<DateTime> exclude = null, DateTime receprionAntecedent = default, int? take = null)
        {
            void onPost(byte[] dataPost, DateTime receptionTime) => Context.Messaging.ShowPost(dataPost, ChatId, receptionTime, actionToExecuteForEachMessage); // He converted the post (encrypted) into a message           
            return Context.Repository.ReadPosts(ChatId, onPost, receprionAntecedent, take, exclude);
        }

        /// <summary>
        /// Date of the most recent message in the chat. Be careful, this value is UTC
        /// </summary>
        public DateTime LastMessageTime
        {
            get; set;
            // After this SaveContact for the change to take effect and update the interface
            // Saving is not entered here to prevent this from happening during deserialization
        }
        internal void SetLastMessageTime(DateTime lastMessageTime, bool isMy, bool ignoreLastMessageTimeCheck)
        {
            if (lastMessageTime < LastMessageTime && !ignoreLastMessageTimeCheck) return;
            if (!isMy)
                LastNotMyMessageTime = lastMessageTime;
            LastMessageTime = lastMessageTime;
            Context.Contacts.SortContacts(false);
            OnPropertyChanged(nameof(LastMessageTime));
            OnPropertyChanged(nameof(LastMessageTimeText));
            OnPropertyChanged(nameof(LastMessageTimeDistance));
        }

        /// <summary>
        /// Date of the last message time of the user.
        /// </summary>
        public DateTime LastNotMyMessageTime;


        public DateTime LastNotMyMessageTimeAtSendLastReading;

        private string _oldLastMessageTimeDistance;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public string LastMessageTimeDistance
        {
            get
            {
                _oldLastMessageTimeDistance = Functions.DateToRelative(LastMessageTime);
                return _oldLastMessageTimeDistance;
            }
            set
            {
                OnPropertyChanged(nameof(LastMessageTimeDistance));
                OnPropertyChanged(nameof(LastMessagePreview));
            }
        }
        internal void UpdateLastMessageTimeDistance()
        {
            if (_oldLastMessageTimeDistance != Functions.DateToRelative(LastMessageTime))
                OnPropertyChanged(nameof(LastMessageTimeDistance));
        }

        /// <summary>
        /// Date of the most recent message in the chat in string format. This value is Local Time
        /// </summary>
        [XmlIgnore]
        public string LastMessageTimeText => LastMessageTime == DateTime.MinValue ? "" : LastMessageTime.ToLocalTime().ToString();
        /// <summary>
        /// The public keys of all members of the group
        /// </summary>
        [XmlIgnore]
        public List<byte[]> Participants { get; private set; }
        private bool _isBlocked;

        /// <summary>
        /// 
        /// </summary>
        public bool IsBlocked
        {
            get { return _isBlocked; }
            set
            {
                if (_isBlocked != value)
                {
                    _isBlocked = value;
                    Context?.Messaging.SendContactStatus(this);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public bool ImBlocked { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsMuted { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool SendConfirmationOfReading = true;

        /// <summary>
        /// Indicates whether messages are required to be translated. If you change this parameter, you need to save the contact
        /// </summary>
        public bool TranslationOfMessages;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public bool IsVisible => !IsServer;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public bool IsServer;
        internal void UpdateLastReaded(ulong idParticipant, DateTime dateTime)
        {
            lock (RemoteReadedList)
            {
                var readed = _remoteReadedList.Find(x => x.IdParticipant == idParticipant);
                if (readed == null)
                {
                    readed = new RemoteReaded() { IdParticipant = idParticipant };
                    _remoteReadedList.Add(readed);
                }
                if (dateTime > readed.DateTime)
                {
                    readed.DateTime = dateTime;
                    Context.OnLastReadedTimeChangeInvoke(this, readed.IdParticipant, readed.DateTime);
                    Save();
                }
            }
        }
        [XmlIgnore]
        internal DateTime ServerLoggedTime;

        /// <summary>
        /// Set message information on last message delivery.
        /// </summary>
        public MessageInfo LastMessageDelivered;

        /// <summary>
        /// Set message information on last message sent.
        /// </summary>
        public MessageInfo LastMessageSent;
        internal void SetLastMessageSent(DateTime creation, uint dataId) => LastMessageSent = new MessageInfo() { Creation = creation.Ticks, DataId = dataId };
        /// <summary>
        /// Set message information on creation and sent.
        /// </summary>
        public class MessageInfo
        {
            /// <summary>
            /// Message information function for creation and dataId.
            /// </summary>
            public MessageInfo() { }

            /// <summary>
            /// Integer data type for when a message is created
            /// </summary>
            public long Creation;

            /// <summary>
            /// unsigned integer data id.
            /// </summary>
            public uint DataId;
        }

        internal bool OnMessageDelivered(uint dataId)
        {
            var lastMessageSent = LastMessageSent;
            if (lastMessageSent != null && lastMessageSent.DataId == dataId)
            {
                Context.OnMessageDeliveredInvoke(this, new DateTime(lastMessageSent.Creation), false);
                LastMessageDelivered = lastMessageSent;
                LastMessageSent = null;
                Save();
                return true;
            }
            return false;
        }

        private List<RemoteReaded> _remoteReadedList = new List<RemoteReaded>();

        /// <summary>
        /// Add new timestamp to to the list.
        /// </summary>
        public RemoteReaded[] RemoteReadedList { get => _remoteReadedList.ToArray(); set => _remoteReadedList = new List<RemoteReaded>(value); }
        /// <summary>
        /// Add a timestamp to the message.
        /// </summary>
        public class RemoteReaded
        {
            /// <summary>
            /// Add a timestamp to the message.
            /// </summary>
            public RemoteReaded()
            {
                // empty constructor for serialization
            }
            /// <summary>
            /// unsigned integer value.
            /// </summary>
            public ulong IdParticipant;

            /// <summary>
            /// integer value for time.
            /// </summary>
            public long TimeStamp;

            /// <summary>
            /// Update the timestamp on the message.
            /// </summary>
            [XmlIgnore]
            public DateTime DateTime { get => new DateTime(TimeStamp); set => TimeStamp = value.Ticks; }
        }

        /// <summary>
        /// Set as group if more than two participants.
        /// </summary>
        [XmlIgnore]
        public bool IsGroup => Participants.Count > 2;
        internal void RaiseEventLastMessageChanged(Message message)
        {
            LastMessageChanged?.Invoke(message);
        }

        /// <summary>
        /// Check if the last message was changed.
        /// </summary>
        public event Contacts.LastMessagChanged LastMessageChanged;

        /// <summary>
        /// Returns null if there are no messages in the chat
        /// </summary>
        /// <returns></returns>
        private Message GetLastMessage() => Context.Repository.GetLastMessageViewable(ChatId, out _);

        /// <summary>
        /// Use this function when deleting a message, to update the preview of the last message as well
        /// </summary>
        internal void UpdateLastMessagePreview() => SetLastMessagePreview(GetLastMessage(), true);

        internal void SetLastMessagePreview(Message lastMessage, bool ignoreLastMessageTimeCheck = false)
        {
            string messagePreview = null;
            var fontAttributes = 2; // italic
            if (lastMessage != null && (lastMessage.ReceptionTime > LastMessageTime || ignoreLastMessageTimeCheck) && MessageFormat.MessageDescription.ContainsKey(lastMessage.Type))
            {
                const int previewLen = 25;
                var isMy = lastMessage?.GetAuthor().SequenceEqual(Context.My.GetPublicKeyBinary());
                SetLastMessageTime(lastMessage.ReceptionTime, (bool)isMy, ignoreLastMessageTimeCheck); //This causes the contact list sorted by most recent message
                if (LastMessageIsMy != isMy)
                {
                    LastMessageIsMy = (bool)isMy;
                    OnPropertyChanged(nameof(LastMessageIsMy));
                }
                if (lastMessage.Type == MessageFormat.MessageType.Text)
                {
                    var text = System.Text.Encoding.Unicode.GetString(lastMessage.Data);
                    messagePreview = text.Length <= previewLen ? text : text.Substring(0, previewLen - 3) + "...";
                }
                else
                {
                    fontAttributes = 0;
                    messagePreview = MessageFormat.MessageDescription[lastMessage.Type];
                }
            }
            else if (lastMessage == null)
                messagePreview = "";
            if (messagePreview != null && LastMessagePreview != messagePreview)
            {
                LastMessagePreview = messagePreview;
                OnPropertyChanged(nameof(LastMessagePreview));
                if (fontAttributes != LastMessageFontAttributes)
                {
                    LastMessageFontAttributes = fontAttributes;
                    OnPropertyChanged(nameof(LastMessageFontAttributes));
                }
            }
        }

        /// <summary>
        /// Get the preview of last message.
        /// </summary>
        public string LastMessagePreview { get; set; } // Do not use this parameter to set. To set use SetLastMessagePreview. This is public only for deserialization purposes

        /// <summary>
        /// Boolean check for last message.
        /// </summary>
        public bool LastMessageIsMy { get; set; } // Don't use 'set', is available only for deserialization purposes
        /// <summary>
        /// This is an empirical value, please do not use it unless necessary. Unfortunately in iOS it is not possible to locally update the red dot with the number of unread messages, this value must be sent with the notification.
        /// </summary>
        [XmlIgnore]
        public uint RemoteUnreaded { get; internal set; }

        /// <summary>
        /// Integer value for last message font attributes.
        /// </summary>
        public int LastMessageFontAttributes { get; set; }

        /// <summary>
        /// Integer value of the Chat Id.
        /// </summary>
        [XmlIgnore]
        public ulong ChatId { get; private set; }
        /// <summary>
        /// Is the user id of your single contact, if is a group this value is null
        /// </summary>
        [XmlIgnore]
        public ulong? UserId { get; private set; } // This value is set only if it is not a group, and is used to identify a single user
        internal string _firebaseToken;
        /// <summary>
        /// Get the fire base token value and save it.
        /// </summary>
        public string FirebaseToken
        {
            get
            {
                return _firebaseToken;
            }
            set
            {
                if (_firebaseToken == value) return;
                _firebaseToken = value;
                if (Context != null)
                    Save(true);
            }
        }
        internal string _deviceToken;

        /// <summary>
        ///Return and save the device token. 
        /// </summary>
        public string DeviceToken
        {
            get
            {
                return _deviceToken;
            }
            set
            {
                if (_deviceToken == value) return;
                _deviceToken = value;
                if (Context != null)
                    Save(true);
            }
        }

        /// <summary>
        /// Set avator for the input byte array.
        /// </summary>
        [XmlIgnore]
        public byte[] Avatar
        {
            get
            {
                if (!IsGroup) return Context.SecureStorage.DataStorage.LoadData("avatar" + ChatId);
                //Debugger.Break(); // Operation not valid for groups
                return null;
            }
            set
            {
                Context.SecureStorage.DataStorage.SaveData(value, "avatar" + ChatId);
                OnPropertyChanged(nameof(Avatar));
            }
        }

        internal void RequestAvatarUpdate()
        {
            if (!Context.My.IsServer)
            {
                // Load avatars from cloud
                if (IsGroup) return;
                var currentAvatar = Avatar;
                var avatarSize = currentAvatar?.Length ?? 0;
                if ((DateTime.UtcNow - LastMessageTime).TotalDays <= 30) // Update the avatars only of those I had a recent conversation, so as not to create traffic on the server
                    Context.CloudManager?.LoadDataFromCloud("Avatar", UserId.ToString(), avatarSize, true); //               Cloud.SendCloudCommands.GetAvatar(Context, (ulong)UserId, avatarSize);
            }
        }

        /// <summary>
        /// Save the data on cloud storage.
        /// </summary>
        /// <param name="cloudBackup">Boolean</param>
        public void Save(bool cloudBackup = false)
        {
            if (Context?.My.IsServer != false || IsServer || !Context.Contacts.ContactsList.Contains(this)) return;
            _ = Context.SecureStorage.ObjectStorage.SaveObject(this, ChatId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!cloudBackup) return;
            var data = ContactMessage.GetDataMessageContact(this, Context, !IsGroup, !IsGroup);
            data = SecureStorage.Cryptography.Encrypt(data, Context.My.Csp.ExportCspBlob(true));
            if (!Context.My.IsServer)
            {
                Context.CloudManager?.SaveDataOnCloud("Contact", ChatId.ToString(), data); // Cloud.SendCloudCommands.PostObject(Context, "Contact", ChatId.ToString(), data);
                RequestAvatarUpdate();
            }
        }

        internal Contact Load(ulong chatId) => Load(chatId);

        internal Contact Load(string id)
        {
            Context.Contacts.RefreshSuspend = true;
            var contact = (Contact)Context.SecureStorage.ObjectStorage.LoadObject(typeof(Contact), id);
            if (contact != null)
            {
                lock (Context.Contacts.ContactsList)
                {
                    Context.Contacts.ContactsList.Add(contact);
                }
            }
            Context.Contacts.RefreshSuspend = false;
            return contact;
        }
        internal void RefreshReadedInfoInUI()
        {
            foreach (var readed in RemoteReadedList)
                Context.OnLastReadedTimeChangeInvoke(this, readed.IdParticipant, readed.DateTime);
        }
        internal void Delete()
        {
            if (_sessionTimeout != null)
                _sessionTimeout.Change(Timeout.Infinite, Timeout.Infinite);
            else
                Context.CloudManager?.DeleteDataOnCloud("Contact", ChatId.ToString()); //   Cloud.SendCloudCommands.DeleteObject(Context, "Contact", ChatId.ToString());
            Context.SecureStorage.ObjectStorage.DeleteObject(typeof(Contact), ChatId.ToString());
            lock (Context.Contacts.ContactsList)
            {
                if (Context.Contacts.ContactsList.Contains(this))
                    Context.Contacts.ContactsList.Remove(this);
            }
        }
        /// <summary>
        /// Create a shallow copy of the current object
        /// </summary>
        /// <returns></returns>
        public object Clone() => MemberwiseClone();

        /// <summary>
        /// The last time the user watched this chat. All messages after this date are to be considered as unseen.
        /// </summary>
        public DateTime LastSeen { get; set; }

        /// <summary>
        /// Integer value get for unread messages.
        /// </summary>
        public int UnreadMessages { get; set; } // Don't use 'set', is available only for deserialization purposes
        internal void SetUnreadMessages(int unreadMessages, bool save = false)
        {
            if (UnreadMessages == unreadMessages) return;
            UnreadMessages = unreadMessages;
            OnPropertyChanged(nameof(UnreadMessages));
            if (save)
                Save();
        }

        /// <summary>
        /// It can be used by the client program to store the user interface of this chat
        /// </summary>
        [XmlIgnore]
        public object MessageContainerUI { get; set; }

        /// <summary>
        /// Get thr QR code string for the context.
        /// </summary>
        /// <returns></returns>
        public string GetQrCode() => ContactMessage.GetQrCode(this, Context);

        /// <summary>
        /// Get the First letter and set it to uppercase.
        /// </summary>
        [XmlIgnore]
        public object NameFirstLetter => Name.Substring(0, 1).ToUpper();

    }
}