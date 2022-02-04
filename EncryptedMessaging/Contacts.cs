using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static EncryptedMessaging.ContactConverter;

namespace EncryptedMessaging
{
    public class Contacts
    {
        public Contacts(Context context)
        {
            Context = context;
            var timerRefreshLastMessageTimeDistance = new Timer(RefreshLastMessageTimeDistance, null, 60000, 60000); //If the garbage collector eliminates it, put it as static
        }
        private readonly Context Context;
        public class Observable<T> : System.Collections.ObjectModel.ObservableCollection<T>
        {
            //code based on https://peteohanlon.wordpress.com/2008/10/22/bulk-loading-in-observablecollection/
            private bool _suppressNotification = false;
            protected override void OnCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
            {
                if (!_suppressNotification)
                    base.OnCollectionChanged(e);
            }
            public void Update(IEnumerable<T> list)
            {
                _suppressNotification = true;
                Clear();
                foreach (T item in list)
                    Add(item);
                _suppressNotification = false;
                OnCollectionChanged(new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
            }
        }

        public delegate void LastMessagChanged(Message message);
        public event Action<List<Contact>> ContactsListChanged;
        private void RefreshLastMessageTimeDistance(object n)
        {
            ForEachContact((contact) => contact.UpdateLastMessageTimeDistance());
        }
        private int _refreshSuspendCount;
        internal bool RefreshSuspend
        {
            get => _refreshSuspendCount != 0; set
            {
                if (value == true) _refreshSuspendCount++; else _refreshSuspendCount--; if (_refreshSuspendCount == 0)
                    SortContacts(false);
            }
        }

        public void RestoreContactFromCloud()
        {
            // the response are processed in class ProcessResponsesFromCloud
            Cloud.SendCloudCommands.GetObject(Context, "String", "MyName");
            Cloud.SendCloudCommands.GetAllObject(Context, "Contact");
        }

        //public void ReadPosts() => ForEachContact(contact =>
        //{
        //    contact.ReadPosts();
        //});

        /// <summary>
        /// This feature sorts contacts by sharing what has more recent messages in chat
        /// Remember to save the contact list after ordering it to keep the changes with the application restart
        /// </summary>
        internal void SortContacts(bool forceUiRefresh)
        {
            if (!RefreshSuspend && ContactsList != null)
            {
                List<Contact> sorted = SortContacts(ContactsList);
                bool checker = false;
                try
                {
                    checker = forceUiRefresh || !sorted.SequenceEqual(ContactsList);
                }
                catch (Exception)
                {
                    // system out of bound exception
                }
                if (checker)
                {
                    lock (ContactsList)
                    {
                        ContactsList.Clear();
                        ContactsList.AddRange(sorted);
                    }
                    //new Thread(() => ContactsListChanged?.Invoke(ContactsVisibled)).Start();
                    ContactsListChanged?.Invoke(ContactsVisibled);
                }
            }
        }

        private List<Contact> SortContacts(List<Contact> contacts) => contacts.OrderByDescending(o => o.LastMessageTime).ToList();

        internal void LoadContacts(bool onlyServer = false)
        {
            void addContact(Contact contact)
            {
                if (ContactsList.ToList().Find((x) => x.UserId == contact.UserId) == null)
                    ContactsList.Add(contact);
            }
            if (!Context.My.IsServer && !onlyServer)
            {
                RefreshSuspend = true;
                //Context.SecureStorage.ObjectStorage.DeleteAllObject(typeof(Contact)); // RESET ALL CONTACTS!
                var objects = Context.SecureStorage.ObjectStorage.GetAllObjects(typeof(Contact));
                foreach (var obj in objects)
                {
                    var contact = (Contact)obj;
                    contact.Initialize(Context);
                    ContactsList.Add(contact);
                }
                ForEachContact(contact =>
                {
                    contact.ReadPosts();
                    foreach (var readed in contact.RemoteReadedList)
                        Context.InvokeOnMainThread(() => Context.OnLastReadedTimeChangeInvoke(contact, readed.IdParticipant, readed.DateTime));
                });
                RefreshSuspend = false;

#if DEBUG || DEBUG_A || DEBUG_B
                if (ContactsList.ToList().Find((x) => x.Name == "Random User") == null)
                    ContactsList.Add(new Contact(Context, participants: new List<byte[]>() { new CryptoServiceProvider().ExportCspBlob(false) }, "Random User"));
                if (Context.ContactConverter.PublicKeysToParticipants("An7tQNorwxKrg7H9wseMShCTl79hSH5g8wy+njNvpSrP", out List<byte[]> testUser))
                {
                    var contact = new Contact(Context, testUser, "Test User");
                    addContact(contact);
                }
#endif
#if DEBUG_ALI
                for (int i = 0; i < 12; i++)
                {
                    var name =  System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.MonthNames[i] + " User";
                    if (ContactsList.ToList().Find((x) => x.Name == name) == null)
                        ContactsList.Add(new Contact(Context, participants: new List<byte[]>() { new CryptoServiceProvider().ExportCspBlob(false) }, name));
                }
#endif
#if DEBUG_A
				if (Context.ContactConverter.PublicKeysToParticipants(Context.PubK_B, out List<byte[]> pubK_B))
				{
					var contact = new Contact(Context, pubK_B, "User B");
                    addContact(contact);
                }
#endif
#if DEBUG_B
				if (Context.ContactConverter.PublicKeysToParticipants(Context.PubK_A, out List<byte[]> pubK_A))
				{
					var contact = new Contact(Context, pubK_A, "User A");
                    addContact(contact);
                }
#endif
            }
            addContact(GetCloudContact());
            SortContacts(true);
            ForEachContact(contact => contact.RefreshReadedInfoInUI());
        }

        private DateTime _lastLoad = DateTime.MinValue;
        internal DateTime LastLoad
        {
            get
            {
                if (_lastLoad == DateTime.MinValue)
                {
                    var lastLoad = Context.SecureStorage.ObjectStorage.LoadObject(typeof(DateTime), "LastLoad");
                    if (lastLoad != null)
                        _lastLoad = (DateTime)lastLoad;
                }
                return _lastLoad;
            }
            set { Context.SecureStorage.ObjectStorage.SaveObject(value, "LastLoad"); }
        }

        internal List<Contact> ContactsList = new List<Contact>();
        internal List<Contact> ContactsVisibled
        {
            get
            {
                var list = new List<Contact>();
                ContactsList.ToList().ForEach(contact => { if (contact.IsVisible) list.Add(contact); });
                return list;
            }
        }

        private readonly Observable<Contact> _contactsObservable = new Observable<Contact>();
        /// <summary>
        /// Get a observable contact list
        /// </summary>
        /// <returns></returns>
        public Observable<Contact> GetContacts()
        {
            ContactsList = ContactsList.OrderByDescending(o => o.LastMessageTime).ToList();
            if (ContactsListChanged == null)
                ContactsListChanged += contacts => Context.InvokeOnMainThread(() => _contactsObservable.Update(contacts));
            _contactsObservable.Clear();
            _contactsObservable.Update(ContactsVisibled);
            return _contactsObservable;
        }

        public void ForEachContact(Action<Contact> action)
        {
            foreach (Contact contact in ContactsVisibled) //Create a new list, so that is not necessary lock when interact
                action.Invoke(contact);
        }

        /// <summary>
        /// Delegate for the event that is triggered when a contact is added
        /// </summary>
        /// <param name="contact">The new contact added</param>
        public delegate void OnContactAddedHandler(Contact contact);

        /// <summary>
        /// Event that is triggered when new contacts are added
        /// </summary>
        public event OnContactAddedHandler OnContactAdded;


        /// <summary>
        /// Add a new contact and send it if necessary
        /// </summary>
        /// <param name="contact">Contact to add</param>
        /// <param name="sendMyContact">Option to send my contact to the contact I add</param>
        /// <param name="sendToGroup">If the contact is a group, you can decide to send the contact added to the group (true), or to individual participants (false)</param>
        /// <param name="setSubContact">If this value is set, the values of the sub contacts will be taken from this object</param>
        /// <returns></returns>
        private bool AddContact(Contact contact, SendMyContact sendMyContact = SendMyContact.Send, bool sendToGroup = true, ContactMessage setSubContact = null)
        {
            List<byte[]> participants = contact.Participants;
            if (participants == null || !ValidateKeys(participants)) return false;
            lock (ContactsList)
            {
                if (ContactsList.Contains(contact))
                    return true;
                Contact duplicate = ContactAlreadyExists(participants, contact.Name);
                if (duplicate != null)
                {
                    duplicate.Name = contact.Name;
                    return true;
                }
                ContactsList.Add(contact);
                OnContactAdded?.Invoke(contact);
                var backUpMyContact = !contact.IsServer && contact.IsVisible && ContactsList.Count == My.AntispamCloud; // When using the application is reasonable, back up my data
                if (backUpMyContact)
                    Context.My.BackupToCloud();
            }
            SortContacts(true);
            contact.Save(true);
            contact.ReadPosts();
            var purposeIsUpdateOnly = sendMyContact == SendMyContact.SendNamelessForUpdate;
            if (!contact.IsGroup)
            {
                if (sendMyContact != SendMyContact.None)
                    Context.Messaging.SendContact(GetMyContact(sendMyContact == SendMyContact.SendNamelessForUpdate), contact, purposeIsUpdateOnly: purposeIsUpdateOnly);
            }
            else
            {
                if (sendToGroup)
                    Context.Messaging.SendContact(contact, contact); // Send this group to all group members
                                                                     // Add single sub contact if not exists
                var myKey = Context.My.Csp.ExportCspBlob(false);
                foreach (var participant in participants)
                    if (!participant.SequenceEqual(myKey)) // exclude myself
                    {
                        Contact subContact = ContactAlreadyExists(new List<byte[]>() { participant }, null);
                        if (subContact == null) // exclude already exists
                        {
                            string name = null;
                            string language = null;
                            if (setSubContact != null)
                            {
                                ContactMessage.Properties values = setSubContact.Participants.Find(x => x.Key.SequenceEqual(participant));
                                name = values.Name;
                                language = values.Language;
                            }
                            subContact = AddContact(new List<byte[]>() { participant }, name, false, language);
                        }
                        if (sendToGroup)
                            Context.Messaging.SendContact(contact, subContact); // All group members receive this contact
                    }
            }
            return true;
        }

        /// <summary>
        /// Enumerator that specifies whether when a contact is added to him I should send mine, and for what purpose
        /// </summary>
        public enum SendMyContact
        {
            /// <summary>
            /// Do not send my contact
            /// </summary>
            None,
            /// <summary>
            /// Send my contact to another contact, the contact contains my real name information
            /// </summary>
            Send,
            /// <summary>
            /// It is used to update the firebase tokens and the device id without changing the contact name. This is required when a user reinstalls the application.
            /// </summary>
            SendNamelessForUpdate,
        }

        /// <summary>
        /// Turn a message into a contact. The ContactMessage is a type of data that is used to send contacts over the network
        /// </summary>
        /// <param name="contactMessage">A contact that has been received by the network</param>
        /// <param name="sendMyContact">Define whether the contact added here should receive our contact via the network, whether with the name or without. If we send our contact without a name, the recipient will only update the Firebase tokens and the device id, if instead the contact has the name, the recipient will add our contact including our real name.</param>
        /// <returns></returns>
        public Contact AddContact(ContactMessage contactMessage, SendMyContact sendMyContact = SendMyContact.Send)
        {
            List<byte[]> participants = contactMessage.GetParticipantsKeys(Context);
            contactMessage.GetProperties(Context, out var language, out Contact.RuntimePlatform os, out var firebaseToken, out var deviceToken);

            var name = contactMessage.Name;
            if (string.IsNullOrEmpty(name) && contactMessage.Participants.Count <= 2)
                foreach (var participant in contactMessage.Participants)
                    if (!participant.Key.SequenceEqual(Context.My.GetPublicKeyBinary()))
                        name = participant.Name;
            Contact duplicate = ContactAlreadyExists(participants, name);
            if (contactMessage.Participants.Count == 1 && contactMessage.Participants[0].Key.SequenceEqual(Context.My.GetPublicKeyBinary())) // Prevent adding your contact
                return null;
            if (contactMessage.IsUpdate && duplicate == null)
                return null;
            if (duplicate != null)
            {
                var save = false;
                if (!duplicate.IsGroup)
                {
                    if (duplicate.Language != language)
                    {
                        duplicate.Language = language;
                        save = true;
                    }
                    if (duplicate.Os != os)
                    {
                        duplicate.Os = os;
                        save = true;
                    }
                    if (duplicate._firebaseToken != firebaseToken)
                    {
                        duplicate._firebaseToken = firebaseToken;
                        save = true;
                    }
                    if (duplicate._deviceToken != deviceToken)
                    {
                        duplicate._deviceToken = deviceToken;
                        save = true;
                    }
                }
                //if (duplicate.Name != name && !string.IsNullOrEmpty(name))
                //{
                //	duplicate.Name = name; // also save
                //	save = false;
                //}
                if (save)
                    duplicate.Save(true);
                return duplicate;
            }

            var contact = new Contact(Context, participants, name, language, os, firebaseToken, deviceToken);
            AddContact(contact, sendMyContact, setSubContact: contactMessage, sendToGroup: sendMyContact == SendMyContact.Send);
            if (!string.IsNullOrEmpty(contactMessage.OldName) && contactMessage.OldName != contactMessage.Name)
                Context.Messaging.NotifyContactNameChange(contact); // I send the name with which the contract is registered in my address book, so I can have notifications with the name used locally in my contacts
            return contact;
        }

        public Contact AddContact(string qrCode, SendMyContact sendMyContact = SendMyContact.Send)
        {
            var contactMessage = new ContactMessage(Convert.FromBase64String(qrCode));
            return AddContact(contactMessage, sendMyContact);
        }

        /// <summary>
        /// Add a contact to the address book, if there is already a contact with the same key then the existing contact will be renamed
        /// </summary>
        /// <param name="contacts">The list of source contacts to create a group</param>
        /// <param name="name">The name of the group/contact</param>
        /// <param name="isServer">Is a server (It will not be visible in the contacts directory)</param>
        /// <param name="sendMyContact">Option to send my contact to the contact I add</param>
        /// <returns></returns>
        public Contact AddContact(List<Contact> contacts, string name = null, bool isServer = false, SendMyContact sendMyContact = SendMyContact.Send)
        {
            var publicKeys = "";
            foreach (Contact contact in contacts)
                publicKeys += contact.PublicKeys;
            return AddContactByKeys(publicKeys, name, isServer, sendMyContact);
        }
        /// <summary>
        /// Add a contact to the address book, if there is already a contact with the same key then the existing contact will be renamed
        /// </summary>
        /// <param name="publicKeys">The group keys or the contact key</param>
        /// <param name="name">The name of the group/contact</param>
        /// <param name="isServer">Is a server (It will not be visible in the contacts directory)</param>
        /// <param name="sendMyContact">Option to send my contact to the contact I add</param>
        /// <returns></returns>
        public Contact AddContact(string publicKeys, string name = null, bool isServer = false, SendMyContact sendMyContact = SendMyContact.Send)
        {
            if (!Context.ContactConverter.PublicKeysToParticipants(publicKeys, out List<byte[]> participants))
                return null;
            Contact duplicate = ContactAlreadyExists(participants, name);
            if (duplicate != null)
            {
                if (duplicate.Name != name || duplicate.IsServer != isServer)
                {
                    duplicate.IsServer = isServer;
                    duplicate.Name = name;
                }
                return duplicate; // Return a value but the contact already exists (is renamed if is necessary)
            }
            var newContact = new Contact(Context, participants, name, isServer: isServer);
            AddContact(newContact, sendMyContact);
            return newContact;
        }

        /// <summary>
        /// Add a contact to the address book, if there is already a contact with the same key then the existing contact will be renamed
        /// </summary>
        /// <param name="publicKeys">The group keys or the contact key</param>
        /// <param name="name">The name of the group/contact</param>
        /// <param name="isServer">Is a server (It will not be visible in the contacts directory)</param>
        /// <param name="sendMyContact">Option to send my contact to the contact I add</param>
        /// <returns></returns>
        public Contact AddContactByKeys(string publicKeys, string name = null, bool isServer = false, SendMyContact sendMyContact = SendMyContact.Send)
        {
            if (!Context.ContactConverter.PublicKeysToParticipants(publicKeys, out List<byte[]> participants) || !Context.ContactConverter.ValidateKeys(publicKeys))
                return null;
            Contact duplicate = ContactAlreadyExists(participants, name);
            if (duplicate != null)
            {
                bool save = false;
                if (duplicate.IsServer != isServer)
                {
                    save = true;
                    duplicate.IsServer = isServer;
                }
                if (duplicate.Name != name)
                {
                    save = false;
                    duplicate.Name = name; // also save
                }
                if (save)
                    duplicate.Save(true);
                return duplicate; // Return true but the contact already exists (is renamed if is necessary)
            }
            var contact = new Contact(Context, participants, name, isServer: isServer);
            return !AddContact(contact, sendMyContact) ? null : contact;
        }

        public Contact AddContact(List<byte[]> participants, string name = null, bool isServer = false, string language = null, SendMyContact sendMyContact = SendMyContact.Send)
        {
            if (participants == null)
                return null;
            Contact duplicate = ContactAlreadyExists(participants, name);
            if (duplicate != null)
            {
                if (duplicate.Name != name)
                {
                    duplicate.Name = name;
                }
                return duplicate;
            }
            var contact = new Contact(Context, participants, name, language, isServer: isServer);
            AddContact(contact, sendMyContact);
            return contact;
        }

        public void RemoveContact(Contact contact)
        {
            contact.Delete();
            ContactsListChanged?.Invoke(ContactsVisibled);
        }

        public void RemoveContact(string key)
        {
            Contact contact = ContactsList.ToList().Find(x => x.PublicKeys == key);
            if (contact != null)
                RemoveContact(contact);
        }

        public void ClearContact(string key)
        {
            Contact contact = ContactsList.ToList().Find(x => x.PublicKeys == key);
            if (contact != null)
            {
                Context.Repository.ClearPosts(contact);
                contact.LastMessagePreview = "";
                contact.LastMessageTimeDistance = "";
                contact.LastMessageTime = new DateTime();
                contact.MessageContainerUI = null;
                contact.Save();
            }
        }

        public List<byte[]> GetParticipants(ulong chatId)
        {
            lock (ContactsList)
            {
                Contact contact = GetContact(chatId);
                return contact?.Participants;
            }
        }

        public Contact GetContact(ulong chatId)
        {
            lock (ContactsList)
                return ContactsList.ToList().Find((x) => x.ChatId == chatId);
        }

        public Contact GetContactByUserID(ulong userId)
        {
            lock (ContactsList)
                return ContactsList.ToList().Find((x) => x.UserId == userId);
        }

        /// <summary>
        /// Look for a contact based on his public key, if not present in the book, a null value will be returned;
        /// Each contact is a group of at least 2 people: The user and the recipients; Contacts with more than one recipient are groups and will not be taken into consideration for research.
        /// </summary>
        /// <param name="key">Public key</param>
        /// <returns></returns>
        public Contact GetParticipant(byte[] key)
        {
            bool find(Contact c)
            {
                List<byte[]> keys = c.Participants;
                return keys.Count == 2 && (keys[0].SequenceEqual(key) || keys[1].SequenceEqual(key));
            }
            lock (ContactsList)
            {
                return ContactsList.ToList().Find((x) => find(x)); ;
            }
        }

        /// <summary>
        /// If the participant is present in the address book, he returns his name, otherwise he invents a name
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetParticipantName(byte[] key)
        {
            Contact participant = GetParticipant(key);
            return (participant != null) ? participant.Name : Pseudonym(new List<byte[]>() { key });
        }

        public List<Contact> GetGroupParicipantContacts(Contact contact)
        {
            var contacts = new List<Contact>();
            foreach (var participant in contact.Participants)
            {
                contacts.Add(GetParticipant(participant));
            }
            return contacts;
        }

        /// <summary>
        /// Check if a contact already exists. The check is done using public keys
        /// </summary>
        /// <param name="participants">Chat participants</param>
        /// <param name="name">For groups you must also pass the name because it is allowed to have groups with the same members but different names</param>
        /// <returns>Returns the duplicate contact if it already exists, otherwise null</returns>
        public Contact ContactAlreadyExists(List<byte[]> participants, string name)
        {
            var participantsClone = participants.ToList(); // We use a clone to prevent errors on other threads interacting with the collection at the same time
            Context.ContactConverter.NormalizeParticipants(ref participantsClone);
            Contact duplicate = new List<Contact>(ContactsList).Find(x =>
            {
                List<byte[]> p = x.Participants;
                if (p.Count != participantsClone.Count)
                    return false;
                for (var i = 0; i < p.Count; i++)
                {
                    System.Collections.IStructuralEquatable c = participantsClone[i];
                    if (c.Equals(p[i], System.Collections.StructuralComparisons.StructuralEqualityComparer) == false)
                        return false;
                }
                return !x.IsGroup || x.Name == name;
            });
            return duplicate;
        }

        /// <summary>
        /// Check if a contact already exists. The check is done using public keys
        /// </summary>
        /// <param name="publicKeys">PublicKeys of contact</param>
        /// <returns>Returns the duplicate contact if it already exists, otherwise null</returns>
        public Contact ContactAlreadyExists(string publicKeys, string name) => Context.ContactConverter.PublicKeysToParticipants(publicKeys, out List<byte[]> participants) ? ContactAlreadyExists(participants, name) : null;

        public string Pseudonym(string publicKeys) => Context.ContactConverter.PublicKeysToParticipants(publicKeys, out List<byte[]> participants) ? Pseudonym(participants) : null;

        public string Pseudonym(List<byte[]> participants) => Pseudonym(ParticipantsToUserIds(participants, Context));

        public string Pseudonym(ulong userId) => Pseudonym(new List<ulong>(new ulong[] { userId }));

        private string Pseudonym(List<ulong> userIds)
        {
            var rnd = ParticipantsToRandom(userIds);
            var result = "";
            string[] syllable = { "do", "re", "mi", "fa", "so", "la", "si", "an", "dre", "rio", "ca", "co", "rdo", "sco", "dro", "ti", "le", "ga", "bri", "e", "ri", "ca", "do" };
            var n = rnd.Next(2, 3);
            for (var i = 0; i < n; i++)
                result += syllable[rnd.Next(0, syllable.Length)];
            return Functions.FirstUpper(result);
        }

        public void Colors(ulong userId, out System.Drawing.Color light, out System.Drawing.Color dark) => Colors(new List<ulong>(new ulong[] { userId }), out light, out dark);

        public void Colors(List<byte[]> participants, out System.Drawing.Color light, out System.Drawing.Color dark) => Colors(ParticipantsToUserIds(participants, Context), out light, out dark);

        internal void Colors(List<ulong> userIds, out System.Drawing.Color light, out System.Drawing.Color dark)
        {
            var level = 0.4;
            var limit = (int)(255 * level);
            var rnd = ParticipantsToRandom(userIds);
            bool darkOk = false;
            bool lightOk = false;
            do
            {
                if (!darkOk)
                    dark = System.Drawing.Color.FromArgb(rnd.Next(limit), rnd.Next(limit), rnd.Next(limit));
                if (!lightOk)
                    light = System.Drawing.Color.FromArgb((255 - limit) + rnd.Next(limit), (255 - limit) + rnd.Next(limit), (255 - limit) + rnd.Next(limit));
                if (!darkOk && dark.GetBrightness() <= level)
                    darkOk = true;
                if (!lightOk && light.GetBrightness() >= (1 - level))
                    lightOk = true;
            } while (!darkOk || !lightOk);
        }

        private Random ParticipantsToRandom(List<ulong> idParticipants)
        {
            var myId = Context.My.GetId();
            ulong seed = 0;
            foreach (var idParticipant in idParticipants)
            {
                if (idParticipants.Count != 2 || !(idParticipant == myId))
                    seed ^= idParticipant;
            }
            return new Random(BitConverter.ToInt32(BitConverter.GetBytes(seed), 0));
        }

        public Contact GetMyContact(bool nameless = false)
        {
            return nameless ? Context.My.CreateMyContact(nameless) : Context.My.Contact;
        }

        private Contact _cloud;
        //#if DEBUG
        //static public string CloudPubKey = @"A4f7EZyD/lVQd5P4r0H3haPCdQJNOU/6sm7LsZoIT+XH";
        //#else
        public static string CloudPubKey = @"ApkrRQUe7qbaKY05Lbs5z+o001UNzXlfHgm+9KEN41vE";

        public static ulong CloudUserId;
        //#endif
        public Contact GetCloudContact()
        {
            if (_cloud == null)
            {
                Context.ContactConverter.PublicKeysToParticipants(CloudPubKey, out List<byte[]> serverUser);
                _cloud = new Contact(Context, serverUser, "Server", "en", isServer: true);
            }
            return _cloud;
        }
    }
}