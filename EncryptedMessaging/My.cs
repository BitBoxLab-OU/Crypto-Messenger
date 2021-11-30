using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace EncryptedMessaging
{
    public class My
    {
        public My(Context context) => Context = context;
        internal Context Context;
        private Contact _contact;
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
        public byte[] GetPrivatKeyBinary() => Csp.ExportCspBlob(true);

        public ulong GetId() => ContactConverter.GetUserId(Csp.ExportCspBlob(false));

        private string _name;
        public string Name
        {
            get
            {
                if (_name == null)
                    _name = Context.SecureStorage.ObjectStorage.LoadObject(typeof(string), "MyName") as string;
                return _name;
            }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    Context.SecureStorage.ObjectStorage.SaveObject(_name, "MyName");
                    Contact.Name = _name;
                    BackupToCloud();
                }
            }
        }

        private string _firebaseToken;
        public string FirebaseToken
        {
            get
            {
                if (_firebaseToken == null)
                    _firebaseToken = Context.SecureStorage.ObjectStorage.LoadObject(typeof(string), "FirebaseToken") as string;
                return _firebaseToken;
            }
            set
            {
                if (_firebaseToken != value)
                {
                    _firebaseToken = value;
                    Context.SecureStorage.ObjectStorage.SaveObject(_firebaseToken, "FirebaseToken");
                    Contact.FirebaseToken = _firebaseToken;
                    BackupToCloud();
                }
            }
        }

        private string _deviceToken;
        public string DeviceToken
        {
            get
            {
                if (_deviceToken == null)
                    _deviceToken = Context.SecureStorage.ObjectStorage.LoadObject(typeof(string), "DeviceToken") as string;
                return _deviceToken;
            }
            set
            {
                if (_deviceToken != value)
                {
                    _contact = null;
                    _deviceToken = value;
                    Context.SecureStorage.ObjectStorage.SaveObject(_deviceToken, "DeviceToken");
                    Contact.DeviceToken = _deviceToken;
                    BackupToCloud();
                }
            }
        }

        /// <summary>
        /// Return the private key stored in the device,if not present, it generates one
        /// </summary>
        /// <returns></returns>
        public string GetPrivateKey() => Convert.ToBase64String(Csp.ExportCspBlob(true));
        public string GetPassphrase()
        {
            var passphrase = Context.SecureStorage.ObjectStorage.LoadObject(typeof(string), "MyPassPhrase") as string;
            if (!string.IsNullOrEmpty(passphrase))
                return passphrase;
            passphrase = Csp.GetPassphrase();
            Context.SecureStorage.ObjectStorage.SaveObject(passphrase, "MyPassPhrase");
            return passphrase;
        }

        /// <summary>
        /// Set the private key and save safely 
        /// </summary>
        internal void SetPrivateKey(string value)
        {
            if (_csp != null)
                Debugger.Break(); // Set the private key only once!
            _csp = new CryptoServiceProvider(value);
            Context.SecureStorage.ObjectStorage.SaveObject(GetPrivateKey(), "MyPrivateKey");
            if (value.Contains(" "))
                Context.SecureStorage.ObjectStorage.SaveObject(value, "MyPassPhrase");
        }

        public byte[] GetAvatar()
        {
            var png = Context.SecureStorage.DataStorage.LoadData("avatar");
            return png;
        }

        public void SetAvatar(byte[] png)
        {
            Context.SecureStorage.DataStorage.SaveData(png, "avatar");
            var encryptedPng = Functions.Encrypt(png, GetPublicKeyBinary()); // The avatar is public but is encrypted using the contact's public key as a password, in this way it can only be decrypted by users who have this contact in the address book
            Cloud.SendCloudCommands.PostAvatar(Context, encryptedPng);
            Context.Contacts.ForEachContact(contact =>
            {
                if ((DateTime.Now.ToLocalTime() - contact.LastMessageTime).TotalDays < 30) // To avoid creating too much traffic on the network, the information on the avatar update is sent only to those who have sent us messages in the last 30 days
                    Context.Messaging.SendInfo(MessageFormat.InformType.AvatarHasUpdated, contact);
            });
        }

        internal void BackupToCloud()
        {
#if DEBUG
            // to limit the Spam in cloud the contact can be backup when you add the first contact
            if (Context.Contacts.GetContacts().Count > 2)
#else
			// to limit the Spam in cloud the contact can be backup when you add the first contact
			if (Context.Contacts.GetContacts().Count != 0)

#endif
            {
                if (_name != null)
                    Cloud.SendCloudCommands.PostObject(Context, "String", "MyName", SecureStorage.Cryptography.Encrypt(System.Text.Encoding.Unicode.GetBytes(_name), Csp.ExportCspBlob(true))); // Saving the name to the cloud will allow me to get it back when I recover the account with the passphrase
                                                                                                                                                                                                //Cloud.SendCloudCommands.PostBackupUser(Context, Csp.ExportCspBlob(false), Name, FirebaseToken, DeviceToken);
            }
        }

    }
}