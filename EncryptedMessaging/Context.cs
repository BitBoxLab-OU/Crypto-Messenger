using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
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
        /// <param name="entryPoint">The entry point server, to access the network</param>
        /// <param name="networkName">The name of the infrastructure. For tests we recommend using "testnet"</param>
        /// <param name="multipleChatModes">If this mode is enabled there will be multiple chat rooms simultaneously, all archived messages will be preloaded with the initialization of this library, this involves a large use of memory but a better user experience. Otherwise, only one char room will be managed at a time, archived messages will be loaded only when you enter the chat, this mode consumes less memory.</param>
        /// <param name="privateKeyOrPassphrase"></param>
        /// <param name="isServer"></param>
        /// <param name="internetAccess">True if network is available</param>
        /// <param name="invokeOnMainThread">Method that starts the main thread: Actions that have consequences with updating the user interface must run on the main thread otherwise they cause a crash</param>
        /// <param name="getSecureKeyValue">System secure function to read passwords and keys saved with the corresponding set function</param>
        /// <param name="setSecureKeyValue">System secure function for saving passwords and keys</param>
        /// <param name="getFirebaseToken">Function to get FirebaseToken (the function is passed and not the value, so as not to block the main thread as this sometimes takes a long time). FirebaseToken is used by firebase, to send notifications to a specific device. The sender needs this information to make the notification appear to the recipient.</param>
        /// <param name="getAppleDeviceToken">Function to get AppleDeviceToken (the function is passed and not the value, so as not to block the main thread as this sometimes takes a long time). In ios AppleDeviceToken is used to generate notifications for the device. Whoever sends the encrypted message needs this data to generate a notification on the device of who will receive the message.</param>
        /// <param name="cloudPath">Specify the location of the cloud directory (where it saves and reads files), if you don't want to use the system one. The cloud is used only in server mode</param>
        public Context(string entryPoint, string networkName = "testnet", bool multipleChatModes = false, string privateKeyOrPassphrase = null, bool isServer = false, bool? internetAccess = null, Action<Action> invokeOnMainThread = null, Func<string, string> getSecureKeyValue = null, SecureStorage.Initializer.SetKeyKalueSecure setSecureKeyValue = null, Func<string> getFirebaseToken = null, Func<string> getAppleDeviceToken = null, string cloudPath = null)
        {
            _internetAccess = internetAccess ?? NetworkInterface.GetIsNetworkAvailable();
#if DEBUG
            if (cloudPath != null && !isServer)
                System.Diagnostics.Debugger.Break(); // Set up cloud path functions for server applications only
#endif
            Cloud.ReceiveCloudCommands.SetCustomPath(cloudPath, isServer);
            PingAddress = new UriBuilder(entryPoint).Uri;
            Contact.RuntimePlatform runtimePlatform = Contact.RuntimePlatform.Undefined;

            var platform = Environment.OSVersion.Platform;
            if (platform == PlatformID.Win32Windows || platform == PlatformID.Win32NT || platform == PlatformID.WinCE || platform == PlatformID.Xbox)
            {
                runtimePlatform = Contact.RuntimePlatform.Windows;
            }
            else if (platform == PlatformID.Unix || platform == PlatformID.MacOSX)
            {
                var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties().ToString().ToLower();
                if (ipGlobalProperties.Contains(".android"))
                    runtimePlatform = Contact.RuntimePlatform.Android;
                else if (getAppleDeviceToken != null)
                    runtimePlatform = Contact.RuntimePlatform.iOS;
                else
                    runtimePlatform = Contact.RuntimePlatform.Unix;
            }
            RuntimePlatform = runtimePlatform;
            IsServer = isServer;
            SessionTimeout = isServer ? DefaultServerSessionTimeout : Timeout.InfiniteTimeSpan;
            Domain = Converter.BytesToInt(Encoding.ASCII.GetBytes(networkName));
            InvokeOnMainThread = invokeOnMainThread ?? ThreadSafeCalls;
            MessageFormat = new MessageFormat(this);
            SecureStorage = new SecureStorage.Initializer(Instances.ToString(), getSecureKeyValue, setSecureKeyValue);
            Setting = new Setting(this);
            Repository = new Repository(this);
            ContactConverter = new ContactConverter(this);
            My = new My(this, getFirebaseToken, getAppleDeviceToken);

#if DEBUG_A
            privateKeyOrPassphrase = privateKeyOrPassphrase ?? PassPhrase_A;
#elif DEBUG_B
            privateKeyOrPassphrase = privateKeyOrPassphrase ?? PassPhrase_B;
#endif
            if (!string.IsNullOrEmpty(privateKeyOrPassphrase))
                My.SetPrivateKey(privateKeyOrPassphrase);
            Messaging = new Messaging(this, multipleChatModes);
            Contacts = new Contacts(this);
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
        /// <summary>
        /// Delegate for the action to be taken when messages arrive
        /// </summary>
        /// <param name="message">Message</param>
        public delegate void OnMessageArrived(Message message);
        /// <summary>
        /// Delegate that runs automatically when messages are received. On systems that have a stable connection (server or desktop), this event can be used to generate notifications.
        /// Note: Only messages that have viewable content in the chat trigger this event
        /// </summary>
        public event OnMessageArrived OnNotification;
        internal void OnNotificationInvoke(Message message)
        {
            OnNotification?.Invoke(message);
        }
        /// <summary>
        /// Function delegated with the event that creates the message visible in the user interface. This function will then be called whenever a message needs to be drawn in the chat. Server-type host systems that don't have messages to render in chat probably don't need to set this action
        /// </summary>
        /// <param name="message">The message to render in the chat view</param>
        /// <param name="isMyMessage">True if you call it to render my message</param>
        public delegate void ViewMessageUi(Message message, bool isMyMessage);
        /// <summary>
        /// It is the function delegate who writes a message in the chat. This function must be set when the App() class is initialized in the common project.
        /// </summary>
        public event ViewMessageUi ViewMessage;
        internal void ViewMessageInvoke(Message message, bool isMyMessage)
        {
            ViewMessage?.Invoke(message, isMyMessage);
        }
        public delegate void OnContactEventDelegate(Message message);
        /// <summary>
        /// This delegate allows you to set up a event that will be called whenever a system message arrives. Messages that have a graphical display in the chat do not trigger this event.
        /// Use OnMessageArrived to intercept incoming messages that have a content display in the chat
        /// </summary>
        public event Action<Message> OnContactEvent;
        internal void OnContactEventInvoke(Message message)
        {
            OnContactEvent?.Invoke(message);
        }
        /// <summary>
        /// Event that is raised to inform when someone has read a sent message
        /// </summary>
        /// <param name="contact">Contact (group or single user))</param>
        /// <param name="participantId">ID of participant who has read</param>
        /// <param name="lastRadTime">When the last reading took place</param>
        public delegate void LastReadedTimeChangeEvent(Contact contact, ulong participantId, DateTime lastRadTime);
        /// <summary>
        /// Event that is performed when a contact reads a message that has been sent
        /// </summary>              
        public event LastReadedTimeChangeEvent OnLastReadedTimeChange;
        internal void OnLastReadedTimeChangeInvoke(Contact contact, ulong participantId, DateTime lastRadTime)
        {
            InvokeOnMainThread(() => OnLastReadedTimeChange?.Invoke(contact, participantId, lastRadTime));
        }
        /// <summary>
        /// Delegate for the event that notifies when messages are sent
        /// </summary>
        /// <param name="contact"></param>
        /// <param name="deliveredTime"></param>
        /// <param name="isMy"></param>
        public delegate void MessageDeliveredEvent(Contact contact, DateTime deliveredTime, bool isMy);
        /// <summary>
        /// Event that occurs when a message has been sent
        /// </summary>
        public event MessageDeliveredEvent OnMessageDelivered;
        internal void OnMessageDeliveredInvoke(Contact contact, DateTime deliveredTime, bool isMy)
        {
            OnMessageDelivered?.Invoke(contact, deliveredTime, isMy);
        }

        /// <summary>
        /// thread-safe calls
        /// https://docs.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-make-thread-safe-calls?view=netdesktop-6.0
        /// </summary>
        /// <param name="action"></param>
        private void ThreadSafeCalls(Action action)
        {
            var threadParameters = new ThreadStart(delegate { action.Invoke(); });
            var thread2 = new Thread(threadParameters);
            thread2.Start();
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
            My.CheckUpdateTheNotificationKeyToMyContacts();
            if (IsRestored && !IsServer)
                Contacts.RestoreContactFromCloud();
            OnConnectivityChange(_internetAccess);
            IsReady = true;
            OnContextIsInitialized?.Invoke(this);
        }

        /// <summary>
        /// Function that is called when the context has been fully initialized.
        /// If you want to automate something after context initialization, you can do so by assigning an action to this value!
        /// </summary>
        public static Action<Context> OnContextIsInitialized;

        public bool IsReady { get; private set; }

        private static readonly List<Action<bool>> ConnectivityChangeEventCollection = new List<Action<bool>>();

        private static bool _internetAccess;

        internal static bool InternetAccess
        {
            get { return _internetAccess; }
        }
        internal static Uri PingAddress;
        /// <summary>
        /// Function that must be called whenever the host system has a change of state on the connection. This parameter must be set when starting the application.
        /// If it is not set, the libraries do not know if there are changes in the state of the internet connection, and the messages could remain in the queue without being sent.
        /// </summary>
        /// <param name="Connectivity"></param>
        public static void OnConnectivityChange(bool Connectivity)
        {
            _internetAccess = Connectivity;
            ConnectivityChangeEventCollection.ForEach(x => x.Invoke(Connectivity));
        }

        internal Contact.RuntimePlatform RuntimePlatform;
        private static int Instances => ConnectivityChangeEventCollection.Count;
        public Setting Setting;
        public SecureStorage.Initializer SecureStorage;
        internal ContactConverter ContactConverter;
        internal Repository Repository;
        internal MessageFormat MessageFormat;
        public Messaging Messaging;
        public bool IsServer { get; }
        internal static readonly TimeSpan DefaultServerSessionTimeout = new TimeSpan(0, 20, 0);
        public TimeSpan SessionTimeout;

        public My My;
        public Contacts Contacts;
        public delegate void AlertMessage(string text);
        public delegate bool ShareTextMessage(string text);
        // Through this we can program an action that is triggered when a message arrives from a certain chat id

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
