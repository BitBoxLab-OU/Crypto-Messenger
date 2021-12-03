using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunicationChannel;

namespace EncryptedMessaging
{
    /// <summary>
    /// Our mission is to exacerbate the concept of security in messaging and create something conceptually new and innovative from a technical point of view.
    /// Top-level encrypted communication (there is no backend , there is no server-side contact list, there is no server but a simple router, the theory is that if the server does not exist then the server cannot be hacked, the communication is anonymous, the IDs are derived from a hash of the public keys, therefore in no case it is possible to trace who originates the messages, the encryption key is changed for each single message, and a system of digital signatures guarantees the origin of the messages and prevents attacks "men in de middle").
    /// We use different concepts introduced with Bitcoin technology and the library itself: there are no accounts, the account is simply a pair of public and private keys, groups are also supported, the group ID is derived from a hash computed through the public keys of the members, since the hash process is irreversible, the level of anonymity is maximum).
    /// The publication of the source wants to demonstrate the genuineness of the concepts we have adopted! Thanks for your attention!
    /// </summary>

    public class Context
    {
        /// <summary>
        /// This method initializes the network.
        /// You can join the network as a node, and contribute to decentralization, or hook yourself to the network as an external user.
        /// To create a node, set the MyAddress parameter with your web address.If MyAddress is not set then you are an external user.
        /// </summary>
        /// <param name="invokeOnMainThread"></param>
        /// <param name="viewMessage">It is the function delegate who writes a message in the chat</param>
        /// <param name="onContactEvent"></param>
        /// <param name="onNotification">This event occurs whenever a notification arrives. The first parameter (ulong) is the chatId of origin of the message and the second (byte[]) the data. To read the data, use the function EncryptedMessaging.MessageFormat.ReadDataPost(...)</param>
        /// <param name="internetAccess">True if network is available</param>
        /// <param name="entryPoint">The entry point server, to access the network</param>
        /// <param name="networkName">The name of the infrastructure. For tests we recommend using "testnet"</param>
        /// <param name="runtimePlatform"></param>
        /// <param name="multipleChatModes">If this mode is enabled there will be multiple chat rooms simultaneously, all archived messages will be preloaded with the initialization of this library, this involves a large use of memory but a better user experience. Otherwise, only one char room will be managed at a time, archived messages will be loaded only when you enter the chat, this mode consumes less memory.</param>
        /// <param name="onLastReadedTimeChange"></param>
        /// <param name="onMessageDelivered">Event that occurs when a message has been sent</param>
        /// <param name="privateKeyOrPassphrase"></param>
        /// <param name="isServer"></param>
        /// <param name="CloudPath">Specify the location of the cloud directory (where it saves and reads files), if you don't want to use the system one. The cloud is used only in server mode</param>
        /// <param name="getSecureKeyValue">System secure function to read passwords and keys saved with the corresponding set function</param>
        /// <param name="setSecureKeyValue">System secure function for saving passwords and keys</param>
        public Context(Action<Action> invokeOnMainThread, Messaging.ViewMessageUi viewMessage, Action<Message> onContactEvent, Messaging.OnMessageArrived onNotification, Action<Contact, ulong, DateTime> onLastReadedTimeChange, Action<Contact, DateTime, bool> onMessageDelivered, bool internetAccess, string entryPoint, string networkName = "testnet", Contact.RuntimePlatform runtimePlatform = Contact.RuntimePlatform.Undefined, bool multipleChatModes = false, string privateKeyOrPassphrase = null, bool isServer = false, Func<string, string> getSecureKeyValue = null, SecureStorage.Initializer.SetKeyKalueSecure setSecureKeyValue = null, string CloudPath = null)
        {
            _internetAccess = internetAccess;
#if DEBUG
            if (CloudPath != null && !isServer)
                System.Diagnostics.Debugger.Break(); // Set up cloud path functions for server applications only
#endif
            Cloud.ReceiveCloudCommands.SetCustomPath(CloudPath, isServer);
            PingAddress = new UriBuilder(entryPoint).Uri;
            RuntimePlatform = runtimePlatform;
            IsServer = isServer;
            SessionTimeout = isServer ? DefaultServerSessionTimeout : Timeout.InfiniteTimeSpan;
            Domain = Converter.BytesToInt(Encoding.ASCII.GetBytes(networkName));
            InvokeOnMainThread = invokeOnMainThread ?? ((Action action) => action.Invoke());
            MessageFormat = new MessageFormat(this);
            SecureStorage = new SecureStorage.Initializer(Instances.ToString(), getSecureKeyValue, setSecureKeyValue);
            Storage = new Storage(this);
            Setting = new Setting(this);
            Repository = new Repository(this);

            ContactConverter = new ContactConverter(this);
            My = new My(this);

#if DEBUG_A
            privateKeyOrPassphrase = privateKeyOrPassphrase ?? PassPhrase_A;
#elif DEBUG_B
            privateKeyOrPassphrase = privateKeyOrPassphrase ?? PassPhrase_B;
#endif
            if (!string.IsNullOrEmpty(privateKeyOrPassphrase))
                My.SetPrivateKey(privateKeyOrPassphrase);
            Messaging = new Messaging(this, viewMessage, onNotification, multipleChatModes);
            Contacts = new Contacts(this, onLastReadedTimeChange, onMessageDelivered);
            OnContactEvent = onContactEvent;
            var keepConnected = runtimePlatform != Contact.RuntimePlatform.Android && runtimePlatform != Contact.RuntimePlatform.iOS;
            Channell = new Channell(entryPoint, Domain, Messaging.ExecuteOnDataArrival, Messaging.OnDataDeliveryConfirm, My.GetId(), isServer || keepConnected ? Timeout.Infinite : 120 * 1000); // *1* // If you change this value, it must also be changed on the server			

            //Contacts.LoadContacts();
            if (Instances == 0)
                new Task(() => _ = Time.CurrentTimeGMT).Start();
            IsRestored = !string.IsNullOrEmpty(privateKeyOrPassphrase);
            ConnectivityChangeEventCollection.Add((bool connectivity) => Channell.InternetAccess = connectivity);


            ThreadPool.QueueUserWorkItem(new WaitCallback(RunAfterInstanceCreate));

            //var afterInstanceCreate = new Thread((obj) => RunAfterInstanceCreate(obj)) { IsBackground = true };                    
            //afterInstanceCreate.Start(null);
        }

#if DEBUG_A
        internal const string PassPhrase_A = "team grief spoil various much amount average erode item ketchup keen path"; // It is a default key for debugging only, used for testing, it does not affect security
        internal const string PubK_B = "AjRuC/k3zaUe0eXbqTyDTllvND1MCRkqwXThp++OodKw";
#elif DEBUG_B
		internal const string PassPhrase_B = "among scan notable siren begin gentle swift move melody album borrow october"; // It is a default key for debugging only, used for testing, it does not affect security
		internal const string PubK_A = "A31zN58YQFk78iIGE0hJKtht4gUVwF+fCeOMxV2NEsOH";
#endif

        private readonly bool IsRestored;
        private void RunAfterInstanceCreate(object obj)
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            Contacts.LoadContacts(IsRestored);
            if (IsRestored && !IsServer)
                Contacts.RestoreContactFromCloud();
            OnConnectivityChange(_internetAccess);
            IsReady = true;
        }

        public bool IsReady { get; private set; }

        private static readonly List<Action<bool>> ConnectivityChangeEventCollection = new List<Action<bool>>();

        private static bool _internetAccess;

        internal static bool InternetAccess
        {
            get { return _internetAccess; }
        }
        internal static Uri PingAddress;
        public static void OnConnectivityChange(bool Connectivity)
        {
            _internetAccess = Connectivity;
            ConnectivityChangeEventCollection.ForEach(x => x.Invoke(Connectivity));
        }

        internal Contact.RuntimePlatform RuntimePlatform;
        private static int Instances => ConnectivityChangeEventCollection.Count;
        public Setting Setting;
        public readonly Storage Storage;
        public SecureStorage.Initializer SecureStorage;
        internal ContactConverter ContactConverter;
        internal Repository Repository;
        internal MessageFormat MessageFormat;
        public Messaging Messaging;
        public bool IsServer { get; }
        internal static readonly TimeSpan DefaultServerSessionTimeout = new TimeSpan(0, 20, 0);
        public TimeSpan SessionTimeout;
        internal Action<Message> OnContactEvent;
        public My My;
        public Contacts Contacts;
        public delegate void AlertMessage(string text);
        public delegate bool ShareTextMessage(string text);
        // Through this we can program an action that is triggered when a message arrives from a certain chat id
        public Dictionary<ulong, Action<Message>> OnMessageBinaryCome = new Dictionary<ulong, Action<Message>>();

        internal int Domain;
        public Channell Channell;
        public static void ReEstablishConnection(bool iMSureThereIsConnection = false)
        {
            if (iMSureThereIsConnection)
                Functions.TrySwitchOnConnectivityByPing(PingAddress);
            Channell.ReEstablishConnection();
        }

        /// <summary>
        /// Use this property to call the main thread when needed:
        /// The main thread must be used whenever the user interface needs to be updated, for example, any operation on an ObservableCollection that changes elements must be done by the main thread,  otherwise rendering on the graphical interface will generate an error.
        /// </summary>
        public static Action<Action> InvokeOnMainThread;
    }
}
