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
    public class Contact : INotifyPropertyChanged
    {
        public Contact(Context context, List<byte[]> participants, string name = null, string language = "en", RuntimePlatform os = RuntimePlatform.Undefined, string firebaseToken = null, string deviceToken = null, bool isServer = false)
        {
            _name = Functions.FirstUpper(name);
            Language = language;
            Os = os;
            FirebaseToken = firebaseToken;
            DeviceToken = deviceToken;
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
                    UserId = Context.My.GetId();
            }
            Context.Contacts.Colors(Participants, out LightColor, out DarkColor);
        }

        public Contact()
        {
            //Empty constructor for serialization
        }
        internal Context Context;
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
        public string Name
        {
            get => string.IsNullOrEmpty(_name) ? Pseudonym() : _name;
            set
            {
                if (Context != null && IsGroup)
                    Debugger.Break(); // The group name cannot be changed
                var newName = Functions.FirstUpper(value);
                if (_name == newName) return;
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

        [XmlIgnore]
        private System.Drawing.Color LightColor;

        [XmlIgnore]
        public System.Drawing.Color DarkColor;

        [XmlIgnore]
        public string LightColorAsHex => $"#{LightColor.R:X2}{LightColor.G:X2}{LightColor.B:X2}";

        [XmlIgnore]
        public string DarkColorAsHex => $"#{DarkColor.R:X2}{DarkColor.G:X2}{DarkColor.B:X2}";

        public string Language; // 2 character to indicate the language used for this contact, for example EN, IT, etc..
        public RuntimePlatform Os;
        public enum RuntimePlatform
        {
            Undefined,
            Android,
            iOS,
            Windows,
            UWP,
            Unix,
            Mac
        }
        public string Pseudonym() => Context.Contacts.Pseudonym(Participants);
        private string _publicKeys;
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

        internal void ReadPosts()
        {
            MessageContainerUI = null;
            if (Context.Messaging.MultipleChatModes)
            {
                if (Context.Contacts.HibernatedChat == false || RestoreHibernatedUI() == false)
                    Context.Repository.ReadPosts(ChatId);
            }
        }

        public delegate Action PostBackup(ulong chatId, byte[] post, DateTime receptionDate);

        /// <summary>
        /// Export all posts, useful for example for making a backup
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="exportAction">Action that will be performed for each post (If you want to back up put the backup execution code here)</param>
        /// <param name="exclude">List of posts to exclude using the received date as a filter. It is recommended to use this parameter to avoid exporting posts that have already been exported in the past</param>
        /// <param name="async">Do this synchronously or asynchronously (in the background)</param>
        /// <param name="take">Limit the number of messages to take, in case you don't want the whole message list</param>
        static public void ExportPosts(Context context, PostBackup exportAction, List<DateTime> exclude, bool async = false, int? take = null)
        {
            context.Contacts.ForEachContact((contact) =>
            {
                contact.GetPosts((post, receptionDate) => { exportAction(contact.ChatId, post, receptionDate); }, exclude, async, take);
            });
        }

        /// <summary>
        /// Read chat posts for this contact or group (you can use this function to backup chat data)
        /// </summary>
        /// <param name="action">Action to be performed for each post, the byte[] is binary data of the encrypted post that is read from the repository, DateTime is the time the post was received which you can use as a unique ID (you can use the ticks property of DateTime as a unique id ) </param>
        /// <param name="exclude">List of posts to exclude using the received date as a filter. It is recommended to use this parameter to avoid exporting posts that have already been exported in the past</param>
        /// <param name="async">Do this synchronously or asynchronously (in the background)</param>
        /// <param name="take">Limit the number of messages to take, in case you don't want the whole message list</param>
        public void GetPosts(Action<byte[], DateTime> action, List<DateTime> exclude = null, bool async = false, int? take = null)
        {
            Context.Repository.ReadPosts(ChatId, action, async, take, exclude);
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

        public void HibernatedChatUI() => Context.SecureStorage.DataStorage.BinarySerialize(MessageContainerUI, "UI" + ChatId);

        internal bool RestoreHibernatedUI()
        {
            MessageContainerUI = Context.SecureStorage.DataStorage.BinaryDeserialize("UI" + ChatId);
            return MessageContainerUI != null;
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
        public DateTime LastNotMyMessageTime;
        public DateTime LastNotMyMessageTimeAtSendLastReading;

        private string _oldLastMessageTimeDistance;
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
        public bool ImBlocked { get; set; }
        public bool IsMuted { get; set; }
        public bool SendConfirmationOfReading = true;

        /// <summary>
        /// Indicates whether messages are required to be translated. If you change this parameter, you need to save the contact
        /// </summary>
        public bool TranslationOfMessages;

        [XmlIgnore]
        public bool IsVisible => !IsServer;
        [XmlIgnore]
        public bool IsServer = false;
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
        public MessageInfo LastMessageDelivered;
        public MessageInfo LastMessageSent;
        internal void SetLastMessageSent(DateTime creation, uint dataId) => LastMessageSent = new MessageInfo() { Creation = creation.Ticks, DataId = dataId };
        public class MessageInfo
        {
            public MessageInfo() { }
            public long Creation;
            public uint DataId;
        }

        internal bool OnMessageDelivered(uint dataId)
        {
            var lastMessageSent = LastMessageSent;
            if (lastMessageSent != null && lastMessageSent.DataId == dataId)
            {
                new System.Threading.Tasks.Task(() =>
                {
                    Context.OnMessageDeliveredInvoke(this, new DateTime(lastMessageSent.Creation), false);
                }).Start();
                LastMessageDelivered = lastMessageSent;
                LastMessageSent = null;
                Save();
                return true;
            }
            return false;
        }

        private List<RemoteReaded> _remoteReadedList = new List<RemoteReaded>();
        public RemoteReaded[] RemoteReadedList { get => _remoteReadedList.ToArray(); set => _remoteReadedList = new List<RemoteReaded>(value); }
        public class RemoteReaded
        {
            public RemoteReaded()
            {
                // empty constructor for serialization
            }
            public ulong IdParticipant;
            public long TimeStamp;
            [XmlIgnore]
            public DateTime DateTime { get => new DateTime(TimeStamp); set => TimeStamp = value.Ticks; }
        }

        [XmlIgnore]
        public bool IsGroup => Participants.Count > 2;
        internal void RaiseEventLastMessageChanged(Message message)
        {
            LastMessageChanged?.Invoke(message);
        }
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

        public string LastMessagePreview { get; set; } // Do not use this parameter to set. To set use SetLastMessagePreview. This is public only for deserialization purposes
        public bool LastMessageIsMy { get; set; } // Don't use 'set', is available only for deserialization purposes
        /// <summary>
        /// This is an empirical value, please do not use it unless necessary. Unfortunately in iOS it is not possible to locally update the red dot with the number of unread messages, this value must be sent with the notification.
        /// </summary>
        [XmlIgnore]
        public uint RemoteUnreaded { get; internal set; }
        public int LastMessageFontAttributes { get; set; }
        [XmlIgnore]
        public ulong ChatId { get; private set; }
        /// <summary>
        /// Is the user id of your single contact, if is a group this value is null
        /// </summary>
        [XmlIgnore]
        public ulong? UserId { get; private set; } // This value is set only if it is not a group, and is used to identify a single user
        internal string _firebaseToken;
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
        [XmlIgnore]
        public byte[] Avatar
        {
            get
            {
                if (!IsGroup) return Context.SecureStorage.DataStorage.LoadData("avatar" + ChatId);
                //Debugger.Break(); // Operation not valid for groups
                return null;
            }
            internal set
            {
                Context.SecureStorage.DataStorage.SaveData(value, "avatar" + ChatId);
                OnPropertyChanged(nameof(Avatar));
            }
        }

        internal void RequestAvatarUpdate()
        {
            // Load avatars from cloud
            if (IsGroup) return;
            var currentAvatar = Avatar;
            var avatarSize = currentAvatar?.Length ?? 0;
            if ((DateTime.UtcNow - LastMessageTime).TotalDays <= 30) // Update the avatars only of those I had a recent conversation, so as not to create traffic on the server
                Cloud.SendCloudCommands.GetAvatar(Context, (ulong)UserId, avatarSize);
        }

        public void Save(bool cloudBackup = false)
        {
            if (Context?.My.IsServer != false || IsServer || !Context.Contacts.ContactsList.Contains(this)) return;
            _ = Context.SecureStorage.ObjectStorage.SaveObject(this, ChatId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!cloudBackup) return;
            var data = ContactMessage.GetDataMessageContact(this, Context, !IsGroup, !IsGroup);
            data = SecureStorage.Cryptography.Encrypt(data, Context.My.Csp.ExportCspBlob(true));
            Cloud.SendCloudCommands.PostObject(Context, "Contact", ChatId.ToString(), data);
            RequestAvatarUpdate();
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
                Cloud.SendCloudCommands.DeleteObject(Context, "Contact", ChatId.ToString());
            Context.SecureStorage.ObjectStorage.DeleteObject(typeof(Contact), ChatId.ToString());
            lock (Context.Contacts.ContactsList)
            {
                if (Context.Contacts.ContactsList.Contains(this))
                    Context.Contacts.ContactsList.Remove(this);
            }
        }
        public object Clone() => MemberwiseClone();

        /// <summary>
        /// The last time the user watched this chat. All messages after this date are to be considered as unseen.
        /// </summary>
        public DateTime LastSeen { get; set; }
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

        public string GetQrCode() => ContactMessage.GetQrCode(this, Context);

        [XmlIgnore]
        public object NameFirstLetter => Name.Substring(0, 1).ToUpper();

    }
}