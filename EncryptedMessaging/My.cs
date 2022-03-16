using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace EncryptedMessaging
{
    /// <summary>
    /// This class allows you to access all the information about the user who is using the application: the contact, if his cryptographic keys, the tokens of his device for notifications, the user id, etc.
    /// </summary>
    public class My
    {
        internal My(Context context, Func<string> getFirebaseToken, Func<string> getAppleDeviceToken)
        {
            Context = context;
            CounterTask = 1 + (getFirebaseToken == null ? 0 : 1) + (getAppleDeviceToken == null ? 0 : 1); // Create a countdown to check when all tasks have finished
            if (getFirebaseToken != null)
                Task.Run(() =>
                {
                    FirebaseToken = getFirebaseToken();
                    CheckUpdateTheNotificationKeyToMyContacts();
                });
            if (getAppleDeviceToken != null)
                Task.Run(() =>
                {
                    DeviceToken = getAppleDeviceToken();
                    CheckUpdateTheNotificationKeyToMyContacts();
                });
        }
        private int CounterTask; // Create a countdown to check when all tasks have finished In order to perform operations that must be performed at the end of all initialization tasks

        internal Context Context;
        private Contact _contact;

        /// <summary>
        /// Gets my contact (the contact of the user using the application)
        /// </summary>
        public Contact Contact
        {
            get
            {
                if (_contact != null)
                    return _contact;
                _contact = CreateMyContact();
                return _contact;
            }
        }
        internal Contact CreateMyContact(bool nameless = false)
        {
            {
                var culture = CultureInfo.CurrentCulture;
                var language = culture.TwoLetterISOLanguageName;
                var participants = new List<byte[]> { Context.My.GetPublicKeyBinary() };
                return new Contact(Context, participants, nameless ? null : Context.My.Name, language, Context.RuntimePlatform, FirebaseToken, DeviceToken, IsServer);
            }
        }

        /// <summary>
        /// Return a CSP of current user
        /// </summary>
        /// <returns></returns>

        public CryptoServiceProvider Csp
        {
            get
            {
                if (_csp != null)
                    return _csp;
                var key = Context.SecureStorage.ObjectStorage.LoadObject(typeof(string), "MyPrivateKey") as string;
                if (!string.IsNullOrEmpty(key))
                    _csp = new CryptoServiceProvider(Convert.FromBase64String(key));
                if (_csp?.IsValid() == false)
                {
                    Debugger.Break(); // An invalid key was imported! We need to investigate!
                    _csp = null;
                }
                if (_csp == null)
                {
                    _csp = new CryptoServiceProvider();
                    Context.SecureStorage.ObjectStorage.SaveObject(GetPrivateKey(), "MyPrivateKey");
                }
                return _csp;
            }
        }

        private CryptoServiceProvider _csp = null;

        /// <summary>
        /// Boolean set for the Server parameter.
        /// </summary>
        public bool IsServer => Context.IsServer;

        /// <summary>
        /// Return the public key of current user in base64 format
        /// </summary>
        /// <returns></returns>
        public string GetPublicKey() => Convert.ToBase64String(Csp.ExportCspBlob(false));

        /// <summary>
        /// Return the public key of current user in binary format (array of byte)
        /// </summary>
        /// <returns></returns>
        public byte[] GetPublicKeyBinary() => Csp.ExportCspBlob(false);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public byte[] GetPrivatKeyBinary() => Csp.ExportCspBlob(true);

        /// <summary>
        /// Get the unisgned long integer User Id from the CSP byte array.
        /// </summary>
        /// <returns></returns>
        public ulong GetId() => ContactConverter.GetUserId(Csp.ExportCspBlob(false));

        private string _name;
        /// <summary>
        /// Set or get the current username, the data is saved on the cloud in an anonymous and encrypted way if an application usage limit is exceeded.
        /// </summary>
        public string Name
        {
            get
            {
                if (_name == null)
                {
                    _name = Context.SecureStorage.Values.Get("MyName", null);

                    //============================ for compatibility with old version, this code can be removed after first update of application
                    if (_name == null)
                    {
                        _name = Context.SecureStorage.ObjectStorage.LoadObject(typeof(string), "MyName") as string;
                        if (_name != null)
                            Context.SecureStorage.Values.Set("MyName", _name);
                    }
                    //============================

                }
                return _name;
            }
            set => SetName(value);
        }

        /// <summary>
        /// Set the current username, if saveToCloud is true, the data is saved on the cloud in an anonymous and encrypted way if an application usage limit is exceeded.
        /// </summary>
        /// <param name="name">Name</param>
        /// <param name="saveToCloud">if saveToCloud is true, the data is saved on the cloud in an anonymous and encrypted way if an application usage limit is exceeded.</param>
        public void SetName(string name, bool saveToCloud = true)
        {
            if (_name != name)
            {
                _name = name;
                Context.SecureStorage.Values.Set("MyName", _name);
                if (saveToCloud)
                    BackupToCloud();
            }
        }

        private string _firebaseToken;
        /// <summary>
        /// It is used by firebase, to send notifications to a specific device. The sender needs this information to make the notification appear to the recipient.
        /// </summary>
        public string FirebaseToken
        {
            get
            {
                if (_firebaseToken == null)
                    _firebaseToken = Context.SecureStorage.Values.Get("FirebaseToken", null);
                return _firebaseToken;
            }
            private set
            {
                var lastValue = FirebaseToken;
                if (lastValue != value)
                {
                    _contact = null;
                    _firebaseToken = value;
                    Context.SecureStorage.Values.Set("FirebaseToken", _firebaseToken);
                    Contact.FirebaseToken = _firebaseToken;
                    if (!string.IsNullOrEmpty(lastValue)) // If the value has changed then we set a flag that will allow us to transmit the new value to all our contacts when the contact list is loaded
                        NeedUpdateTheNotificationKeyToMyContacts = true;
                }
            }
        }

        private string _deviceToken;
        /// <summary>
        /// In ios this is used to generate notifications for the device. Whoever sends the encrypted message needs this data to generate a notification on the device of who will receive the message.
        /// </summary>
        public string DeviceToken
        {
            get
            {
                if (_deviceToken == null)
                    _deviceToken = Context.SecureStorage.Values.Get("DeviceToken", null);
                return _deviceToken;
            }
            private set
            {
                var lastValue = DeviceToken;
                if (lastValue != value)
                {
                    _contact = null;
                    _deviceToken = value;
                    Context.SecureStorage.Values.Set("DeviceToken", _deviceToken);
                    Contact.DeviceToken = _deviceToken;
                    if (!string.IsNullOrEmpty(lastValue)) // If the value has changed then we set a flag that will allow us to transmit the new value to all our contacts when the contact list is loaded
                        NeedUpdateTheNotificationKeyToMyContacts = true;
                }
            }
        }

        /// <summary>
        /// Return the private key stored in the device,if not present, it generates one
        /// </summary>
        /// <returns></returns>
        public string GetPrivateKey() => Convert.ToBase64String(Csp.ExportCspBlob(true));

        /// <summary>
        /// Gets the combination of words that allow you to recover the account using bitcoin technology
        /// </summary>
        /// <returns>Passphrase</returns>
        public string GetPassphrase()
        {
            var passphrase = Context.SecureStorage.Values.Get("MyPassPhrase", null);

            //============================ for compatibility with old version, this code can be removed after first update of application
            if (passphrase == null)
            {
                passphrase = Context.SecureStorage.ObjectStorage.LoadObject(typeof(string), "MyPassPhrase") as string;
                if (passphrase != null)
                    Context.SecureStorage.Values.Set("MyPassPhrase", passphrase);
            }
            //============================

            if (!string.IsNullOrEmpty(passphrase))
                return passphrase;
            passphrase = Csp.GetPassphrase();
            Context.SecureStorage.Values.Set("MyPassPhrase", passphrase);
            return passphrase;
        }

        /// <summary>
        /// Set the private key and save safely or save the passphrase if you passed this as a parameter
        /// </summary>
        internal void SetPrivateKey(string value)
        {
            if (_csp != null)
                Debugger.Break(); // Set the private key only once!
            _csp = new CryptoServiceProvider(value);
            Context.SecureStorage.Values.Set("MyPrivateKey", GetPrivateKey());
            if (value.Contains(" "))
                Context.SecureStorage.Values.Set("MyPassPhrase", value);
        }

        /// <summary>
        /// Gets the avatar (the image of the user photo in the form of a byte array)
        /// </summary>
        /// <returns></returns>
        public byte[] GetAvatar()
        {
            var png = Context.SecureStorage.DataStorage.LoadData("avatar");
            return png;
        }

        /// <summary>
        /// Sets the avatar (the image of the user photo in the form of a byte array)
        /// </summary>
        /// <param name="png"></param>
        public void SetAvatar(byte[] png)
        {
            Context.SecureStorage.DataStorage.SaveData(png, "avatar");
            var encryptedPng = Functions.Encrypt(png, GetPublicKeyBinary()); // The avatar is public but is encrypted using the contact's public key as a password, in this way it can only be decrypted by users who have this contact in the address book
            Context.CloudManager?.SaveDataOnCloud("", "Avatar", encryptedPng); // Cloud.SendCloudCommands.PostAvatar(Context, encryptedPng);             
            Context.Contacts.ForEachContact(contact =>
            {
                if ((DateTime.Now.ToLocalTime() - contact.LastMessageTime).TotalDays < 30) // To avoid creating too much traffic on the network, the information on the avatar update is sent only to those who have sent us messages in the last 30 days
                    Context.Messaging.SendInfo(MessageFormat.InformType.AvatarHasUpdated, contact);
            });
        }

        internal const int AntispamCloud = 3;
        /// <summary>
        /// Backup my name
        /// When the account is recovered with the passphrase, the name is recovered.
        /// Account recovery is done by resetting the passphrase. This is the routine that is performed following the restore when the cloud sends the name used <see cref="ProcessResponsesFromCloud.OnData(Context, string, string, byte[])">OnData</see>
        /// </summary>
        internal void BackupToCloud()
        {
            // to limit the Spam in cloud the contact can be backup when you add the first contact
            if (Context.Contacts.ContactsList.Count >= AntispamCloud)
            {
                if (_name != null) // Saving the name to the cloud will allow me to get it back when I recover the account with the passphrase
                    Context.CloudManager?.SaveDataOnCloud("String", "MyName", SecureStorage.Cryptography.Encrypt(System.Text.Encoding.Unicode.GetBytes(_name), Csp.ExportCspBlob(true)));  //Cloud.SendCloudCommands.PostObject(Context, "String", "MyName", SecureStorage.Cryptography.Encrypt(System.Text.Encoding.Unicode.GetBytes(_name), Csp.ExportCspBlob(true))); 
                //Cloud.SendCloudCommands.PostBackupUser(Context, Csp.ExportCspBlob(false), Name, FirebaseToken, DeviceToken);
            }
        }

        /// <summary>
        /// If the value has changed then we set a flag that will allow us to transmit the new value to all our contacts when the contact list is loaded
        /// </summary>
        internal bool NeedUpdateTheNotificationKeyToMyContacts;
        /// <summary>
        /// Update my data held to my contacts when necessary.
        /// When my device id or firebase id change (for non-application dependent events), an update is sent to my contacts so they can continue to notify me when they send me communications and messages
        /// </summary>
        private void UpdateTheNotificationKeyToMyContacts()
        {
            var myContact = CreateMyContact(true);
            Context.Contacts.ForEachContact((contact) =>
            {
                if (!contact.IsGroup)
                    Context.Messaging.SendContact(myContact, contact, true);
            });
        }

        internal void CheckUpdateTheNotificationKeyToMyContacts()
        {
            CounterTask--;
            if (CounterTask == 0 && NeedUpdateTheNotificationKeyToMyContacts)
                UpdateTheNotificationKeyToMyContacts();
        }
    }
}