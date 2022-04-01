using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using CommunicationChannel;
using static EncryptedMessaging.MessageFormat;

namespace EncryptedMessaging
{
    /// <summary>
    /// This class contains all the functions related to the messaging functionality for the users.
    /// </summary>
    public class Messaging
    {
        /// <summary>
        /// Class initializer (prepares for use)
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="multipleChatModes">If false, one messaging chat will be handled at a time</param>
        public Messaging(Context context, bool multipleChatModes)
        {
            MultipleChatModes = multipleChatModes;
            Context = context;
        }
        internal Context Context;
        internal bool MultipleChatModes { get; private set; }

        /// <summary>
        /// By default set to false, as only one messaging chat is handled.
        /// </summary>
        /// <param name="value">Boolean</param>
        public void SetMultipleChatModes(bool value)
        {
            MultipleChatModes = value;
        }
        private Contact _currentChatRoom;

        /// <summary>
        /// Get the current chat group and set the time of last viewed and if message read.
        /// </summary>
        public Contact CurrentChatRoom
        {
            get => _currentChatRoom;
            set
            {
                if (_currentChatRoom != null && value != _currentChatRoom)
                {
                    //When you exit the chat set the time of the last view
                    _currentChatRoom.LastSeen = DateTime.UtcNow;
                    _currentChatRoom.Save();
                }

                // When you leave, notify the group participants that your messages have been read
                if (_currentChatRoom?.SendConfirmationOfReading == true && value == null && _currentChatRoom.LastNotMyMessageTime != _currentChatRoom.LastNotMyMessageTimeAtSendLastReading)
                {
                    _currentChatRoom.LastNotMyMessageTimeAtSendLastReading = _currentChatRoom.LastNotMyMessageTime;
                    SendLastReading(_currentChatRoom);
                }

                _currentChatRoom = value;
                if (_currentChatRoom != null)
                {
                    _currentChatRoom.SetUnreadMessages(0, true);
                    if (!MultipleChatModes)
                        new Thread(() => _currentChatRoom.ReadPosts(true)).Start();
                }
            }
        }

        internal void ExecuteOnDataArrival(ulong chatId, byte[] data)
        {
            var receptionTime = DateTime.UtcNow;
            if (Context.MessageFormat.ReadDataPost(data, chatId, receptionTime, out var message, true))
            {
                var isViewable = MessageDescription.ContainsKey(message.Type);
                if (!message.Encrypted) // Incoming messages without encryption are ignored
                {                    
                    if (!isViewable)
                        ExecuteCommand(chatId, message);
                }
                else // Only encrypted messages are taken into consideration, they have been validated by verifying the digital signature
                {
                    if (!message.Contact.IsBlocked || message.Type == MessageType.ContactStatus)
                    {
                        message.Contact.ExtendSessionTimeout();
                        if (isViewable || message.Type == MessageType.LastReading)
                        {
                            Context.Repository.AddPost(data, chatId, ref receptionTime);
                            if (isViewable)
                            {
                                Context.OnNotificationInvoke(message);
                                if (CurrentChatRoom != message.Contact)
                                    message.Contact.SetUnreadMessages(message.Contact.UnreadMessages + 1, true);
                                if (MultipleChatModes || CurrentChatRoom == message.Contact)
                                    // The device is viewing this chat, so I add the message immediately
                                    ShowMessage(message, message.GetAuthor().SequenceEqual(Context.My.GetPublicKeyBinary()));
                                message.Contact.RaiseEventLastMessageChanged(message);
                            }
                        }
                        if (!isViewable)
                            ExecuteCommand(chatId, message);
                        UpdateReaded(message);
                    }
                    else
                    {
                        SendContactStatus(message.Contact);
                    }
                }
            }
        }

        private void InvokeOnContactEvent(Message message)
        {
            if (Context.IsServer)
                Context.OnContactEventInvoke(message);
            else
                Context.InvokeOnMainThread(() => Context.OnContactEventInvoke(message));
        }

        private void ExecuteCommand(ulong chatId, Message message)
        {
            if (message.Type == MessageType.Delete)
            {
                Context.Repository.DeletePostByPostId(Converter.BytesToUlong(message.Data), chatId);
            }
            else if (message.Type == MessageType.NameChange)
            {
                message.Contact.MyRemoteName = Encoding.Unicode.GetString(message.Data);
            }
            else if (message.Type == MessageType.ContactStatus)
            {
                message.Contact.ImBlocked = (BitConverter.ToUInt32(message.Data, 0) & 0b10000000_00000000_00000000_00000000) != 0; //bit 0 = is blocked;
            }
            else if (message.Type == MessageType.Inform)
            {
                var informType = (InformType)message.Data[0];
                if (informType == InformType.AvatarHasUpdated)
                    message.Contact.RequestAvatarUpdate();
            }
            InvokeOnContactEvent(message);
        }


        /// <summary>
        /// if showMessage is null, add a post on the chat page, otherwise it executes the method showMessage that is passed.
        /// </summary>
        /// <param name="dataPost">the encrypted message</param>
        /// <param name="chatId">id of chat</param>
        /// <param name="receptionTime">Date of receipt of the message. This data must be passed to be included in the message object</param>
        /// <param name="showMessage">Action that must be performed for each message read, the action has two parameters: The message object is a boolean value that indicates if the message is mine (generated by me). If this value is omitted then the message display method of the host system will be used which is set when the context is initialized (the rendering engine of the user interface that displays messages in the chat).</param>
        internal void ShowPost(byte[] dataPost, ulong chatId, DateTime receptionTime, Action<Message, bool> showMessage = null)
        {
            if (Context.MessageFormat.ReadDataPost(dataPost, chatId, receptionTime, out var message))
            {
                if (MessageDescription.ContainsKey(message.Type))
                {

                    if (showMessage == null)
                        showMessage = ShowMessage;
                    showMessage(message, message.GetAuthor().SequenceEqual(Context.My.GetPublicKeyBinary()));
                }
                UpdateReaded(message);
            }
        }

        /// <summary>
        /// Add a post on the chat page.
        /// </summary>
        /// <param name="message">the message to show</param>
        /// <param name="isMy">He has to render my message</param>
        private void ShowMessage(Message message, bool isMy)
        {

            // Update the empirical value of unread messages from the contact (value to be included in notifications)
            if (isMy)
                message.Contact.RemoteUnreaded++;
            Context.ViewMessageInvoke(message, isMy);
        }

        private void UpdateReaded(Message message)
        {
            var isMy = message.GetAuthor().SequenceEqual(Context.My.GetPublicKeyBinary());
            var lastReading = DateTime.MinValue;
            if (message.Type == MessageType.LastReading)
                lastReading = Converter.FromUnixTimestamp(message.Data);
            else if (!isMy)
                lastReading = message.Creation;
            if (lastReading != DateTime.MinValue)
            {
                var userId = ContactConverter.GetUserId(message.Author);
                message.Contact?.UpdateLastReaded(userId, lastReading);
            }
            // Update the empirical value of unread messages from the contact (value to be included in notifications)
            if (!isMy)
                message.Contact.RemoteUnreaded = 0;
        }
        internal class SendMessageParameters
        {
            /// <summary>The type of data</summary>
            public MessageType Type;
            /// <summary>The body of the data in binary format</summary>
            public byte[] Data;
            /// <summary>Recipient of the message. ToContact and toIdUsers cannot be set simultaneousl</summary>
            public Contact ToContact;
            /// <summary>The post Id property of the message you want to reply to</summary>
            public ulong? ReplyToPostId;
            /// <summary>Set this value if toContact is null, that is, if the message is not encrypted</summary>
            public ulong? ChatId;
            /// <summary>Id of the members of the group the message is intended for. ToContact and toIdUsers cannot be set simultaneously</summary>
            public ulong[] ToIdUsers;
            /// <summary>If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</summary>
            public bool DirectlyWithoutSpooler;
            /// <summary>Clients are only able to receive encrypted messages. Non-encrypted messages are reserved for communications with cloud servers if the data is already encrypted and does not require a second encryption and if the message must be delivered to a server that does not have the client in the address book and therefore could not otherwise read it</summary>
            public bool Encrypted = true;
            /// <summary>Avoid a recursive loop for the login message</summary>
            public bool IsLogin;
        }

        /// <summary>
        /// Send the message to all participants in the current chat room, and save a copy of the local storage
        /// </summary>
        /// <param name="type">The type of data</param>
        /// <param name="data">The body of the data in binary format</param>
        /// <param name="toContact">Recipient of the message. ToContact and toIdUsers cannot be set simultaneousl</param>
        /// <param name="replyToPostId">The post Id property of the message you want to reply to</param>
        /// <param name="chatId">Set this value if toContact is null, that is, if the message is not encrypted</param>
        /// <param name="toIdUsers">Id of the members of the group the message is intended for. ToContact and toIdUsers cannot be set simultaneously</param>
        /// <param name="directlyWithoutSpooler">If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</param>
        /// <param name="encrypted">Clients are only able to receive encrypted messages. Non-encrypted messages are reserved for communications with cloud servers if the data is already encrypted and does not require a second encryption and if the message must be delivered to a server that does not have the client in the address book and therefore could not otherwise read it</param>
        /// <param name="isLogin">Flad used only for the login command to avoid a recursive loop</param>
        public void SendMessage(MessageType type, byte[] data, Contact toContact, ulong? replyToPostId = null, ulong? chatId = null, ulong[] toIdUsers = null, bool directlyWithoutSpooler = false, bool encrypted = true, bool isLogin = false)
        {
#if DEBUG
            if (isLogin == true && type != MessageType.Contact)
                Debugger.Break(); // The isLogin flag can only be used for the login method
            //if (toContact.IsServer == false && encrypted == false)
            //    Debugger.Break(); // Client are not allow to received not encripted data (The encrypted data gives us the certainty of who is sending them since the post has a digital signature)
#endif
            SendMessage(new SendMessageParameters()
            {
                Type = type,
                Data = data,
                ToContact = toContact,
                ReplyToPostId = replyToPostId,
                ChatId = chatId,
                ToIdUsers = toIdUsers,
                DirectlyWithoutSpooler = directlyWithoutSpooler,
                Encrypted = encrypted,
                IsLogin = isLogin
            }); ;
        }

        internal Queue<SendMessageParameters> SendMessageQueue = new Queue<SendMessageParameters>();

        internal void SendMessagesInQueue()
        {
            var queue = SendMessageQueue;
            SendMessageQueue = null;
            if (queue != null)
                while (queue.Count != 0)
                { SendMessage(queue.Dequeue()); }
        }

        internal void SendMessage(SendMessageParameters @params)
        {
#if DEBUG
            if (@params.ReplyToPostId != null && !MessageDescription.ContainsKey(@params.Type))
                Debugger.Break(); // It is recommended that you use reply to a message, only with messages that can be viewed in the chat
            if (@params.ToContact != null && @params.ToIdUsers != null)
                Debugger.Break(); // toContact and toIdUsers cannot be set simultaneously
            if (@params.ToIdUsers != null && @params.Encrypted)
                Debugger.Break(); // It is not possible to use encryption if you do not have the contact
            if (Context.AfterInstanceThread == Thread.CurrentThread)
                Debugger.Break(); // It is not allowed to send messages from the RunAfterInstanceCreate method, move the sending of messages into RunAfterInitialization
#endif
            if (!Context.IsReady)
            {
                SendMessageQueue.Enqueue(@params);
                return;
            }

            if (@params.ToContact?.IsServer == true && @params.Encrypted && !@params.IsLogin) // The servers don't have the client's public key because they don't have the contact list. The login consists in sending your contact to the server, so that it can have the public key to communicate in encrypted form.
            {
                LoginToServer(@params.DirectlyWithoutSpooler, @params.ToContact);
            }

            if (!Context.InternetAccess)
            {
                // The status of the Internet connection may not correspond to reality, so let's try a ping to verify it, in order to update the current status if necessary
                if (!AlreadyTrySwitchOnConnectivity)
                {
                    AlreadyTrySwitchOnConnectivity = true;
                    Functions.TrySwitchOnConnectivityByPing(Context.EntryPoint);
                }
            }
            else
                AlreadyTrySwitchOnConnectivity = false;

            if (@params.Type != MessageType.ContactStatus && @params.ToContact?.IsBlocked == true) return;
            @params.ToContact?.ExtendSessionTimeout();
            byte[] dataPost; DateTime creationDate;
            dataPost = @params.ToIdUsers != null ? Context.MessageFormat.CreateDataPostUnencrypted(@params.Type, @params.Data, @params.ToIdUsers, out creationDate, @params.ReplyToPostId) : Context.MessageFormat.CreateDataPost(@params.Type, @params.Data, @params.ToContact.Participants, out creationDate, @params.ReplyToPostId, @params.Encrypted);
            if (MessageDescription.ContainsKey(@params.Type) && @params.ToContact?.IsVisible == true)
            {
                var localStorageTime = DateTime.UtcNow;
                //Prepare the plain-text message to view
                var message = new Message(Context, @params.ToContact, @params.Type, Context.My.Csp.ExportCspBlob(false), creationDate, @params.Data, localStorageTime, Repository.PostId(dataPost), @params.Encrypted, @params.ToContact.ChatId, Context.My.GetId())
                {
                    ReplyToPostId = @params.ReplyToPostId
                };
                Context.Repository.AddPost(dataPost, @params.ToContact.ChatId, ref localStorageTime); // Add the encrypted message to the local storage and also forward it to the server
                @params.ToContact.SetLastMessagePreview(message);
                @params.ToContact.SetLastMessageSent(creationDate, Utility.DataId(dataPost));
                @params.ToContact.Save();
                ShowMessage(message, true);
            }
            Context.Channel.CommandsForServer.SendPostToServer(@params.ToContact != null ? @params.ToContact.ChatId : (ulong)@params.ChatId, dataPost, @params.DirectlyWithoutSpooler);
        }

        private bool AlreadyTrySwitchOnConnectivity;
        //private bool AntiRecursive; // Avoid the recursive loop


        internal void DeleteMessage(ulong postId, Contact toContact)
        {
            var data = Converter.GetBytes(postId);
            SendMessage(MessageType.Delete, data, toContact);
        }

        /// <summary>
        /// It is executed when the server confirms that it has received a message
        /// </summary>
        /// <param name="dataId"></param>
        internal void OnDataDeliveryConfirm(uint dataId)
        {
            lock (Context.Contacts.ContactsList)
                foreach (var contact in Context.Contacts.ContactsList)
                    if (contact.OnMessageDelivered(dataId))
                        break;
        }

        /// <summary>
        /// Send a text
        /// </summary>
        /// <param name="text">The text to send</param>
        /// <param name="toContact">The recipient</param>
        /// <param name="replyToPostId">The post Id property of the message you want to reply to</param>
        public void SendText(string text, Contact toContact, ulong? replyToPostId = null)
        {
            text = text?.Trim();
            if (!string.IsNullOrEmpty(text))
                SendMessage(MessageType.Text, Encoding.Unicode.GetBytes(text), toContact, replyToPostId);
        }

        /// <summary>
        /// For images, the only format allowed is PNG. The longest side of the image must not exceed 800 pixels, for example 800 X 480, or 480 X 800 (in no case any side must exceed 800 pixels in length)
        /// </summary>
        /// <param name="png">PNG file, maximum side size = 800 pix</param>
        /// <param name="toContact">The recipient</param>
        /// <param name="replyToPostId">The post Id property of the message you want to reply to</param>
        public void SendPicture(byte[] png, Contact toContact, ulong? replyToPostId = null) =>
            SendMessage(MessageType.Image, png, toContact, replyToPostId);

        /// <summary>
        /// Send a info
        /// </summary>
        /// <param name="inform">Info type</param>
        /// <param name="toContact">The recipient</param>
        /// <param name="chatId">Set this value if toContact is null, that is, if the message is not encrypted</param>
        /// <param name="toIdUsers">Id of the members of the group the message is intended for. ToContact and toIdUsers cannot be set simultaneously</param>
        /// <param name="directlyWithoutSpooler">If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</param>
        /// <param name="encrypted">Clients are only able to receive encrypted messages. Non-encrypted messages are reserved for communications with cloud servers if the data is already encrypted and does not require a second encryption and if the message must be delivered to a server that does not have the client in the address book and therefore could not otherwise read it</param>

        public void SendInfo(InformType inform, Contact toContact, ulong? chatId = null, ulong[] toIdUsers = null, bool directlyWithoutSpooler = false, bool encrypted = true)
        {
            SendMessage(MessageType.Inform, new[] { (byte)inform }, toContact, null, chatId, toIdUsers, directlyWithoutSpooler, encrypted);
        }


        /// <summary>
        /// Send a binary data block
        /// To read the data on the target client/server: var values = Functions.SplitData(true,data);
        /// </summary>
        /// <param name="toContact">The recipient</param>
        /// <param name="binaryData">Binary data block to send</param>
        /// <param name="directlyWithoutSpooler">If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</param>
        /// <param name="encrypted">Clients are only able to receive encrypted messages. Non-encrypted messages are reserved for communications with cloud servers if the data is already encrypted and does not require a second encryption and if the message must be delivered to a server that does not have the client in the address book and therefore could not otherwise read it</param>
        public void SendBinary(Contact toContact, byte[] binaryData, bool directlyWithoutSpooler = false, bool encrypted = true) => SendMessage(MessageType.Binary, binaryData, toContact, null, null, null, directlyWithoutSpooler, encrypted);

        /// <summary>
        /// Send a binary unencrypted (Clients are only able to receive encrypted messages: do not use this command to send messages to communicate between clients)
        /// To read the data on the target client/server: var values = Functions.SplitData(true,data);
        /// </summary>
        /// <param name="toIdUsers">The recipients</param>
        /// <param name="binaryData">Binary data block to send</param>
        /// <param name="chatId">Id of the chat</param>
        /// <param name="directlyWithoutSpooler">If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</param>
        public void SendBinaryUnencrypetd(ulong chatId, ulong[] toIdUsers, byte[] binaryData, bool directlyWithoutSpooler = false) => SendMessage(MessageType.Binary, binaryData, null, null, chatId, toIdUsers, directlyWithoutSpooler, false);


        /// <summary>
        /// Send array of bytes, not exceeding 255 bytes
        /// To read the data on the target client: var values = Functions.SplitData(true,data);
        /// </summary>
        /// <param name="toContact">The recipient</param>
        /// <param name="directlyWithoutSpooler">If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</param>
        /// <param name="encrypted">Clients are only able to receive encrypted messages. Non-encrypted messages are reserved for communications with cloud servers if the data is already encrypted and does not require a second encryption and if the message must be delivered to a server that does not have the client in the address book and therefore could not otherwise read it</param>
        /// <param name="values">Data blocks not exceeding 255 bytes each</param>
        public void SendSmallData(Contact toContact, bool directlyWithoutSpooler = false, bool encrypted = true, params byte[][] values) => SendMessage(MessageType.SmallData, Functions.JoinData(true, values), toContact, null, null, null, directlyWithoutSpooler, encrypted);

        /// <summary>
        /// Send array of bytes
        /// To read the data on the target client: var values = Functions.SplitData(false,data);
        /// </summary>
        /// <param name="toContact">The recipient</param>
        /// <param name="directlyWithoutSpooler">If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</param>
        /// <param name="encrypted">Clients are only able to receive encrypted messages. Non-encrypted messages are reserved for communications with cloud servers if the data is already encrypted and does not require a second encryption and if the message must be delivered to a server that does not have the client in the address book and therefore could not otherwise read it</param>
        /// <param name="values">Data blocks not exceeding 255 bytes each</param>
        public void SendData(Contact toContact, bool directlyWithoutSpooler = false, bool encrypted = true, params byte[][] values) => SendMessage(MessageType.Data, Functions.JoinData(false, values), toContact, null, null, null, directlyWithoutSpooler, encrypted);


        /// <summary>
        /// Send array of bytes, not exceeding 255 bytes
        /// To read the data on the target client: var values = Functions.SplitData(data);
        /// </summary>
        /// <param name="cloud">Cloud contact</param>
        /// <param name="directlyWithoutSpooler">If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</param>
        /// <param name="encrypted">Clients are only able to receive encrypted messages. Non-encrypted messages are reserved for communications with cloud servers if the data is already encrypted and does not require a second encryption and if the message must be delivered to a server that does not have the client in the address book and therefore could not otherwise read it</param>
        /// <param name="values">Data blocks not exceeding 255 bytes each</param>
        public void SendSmallDataToCloud(Contact cloud, bool directlyWithoutSpooler = false, bool encrypted = true, params byte[][] values)
        {
            SendSmallData(cloud, directlyWithoutSpooler, encrypted, values);
        }

        /// <summary>
        /// Sends a sequence of key-values, where the key is a byte and the value is an array of 256 bytes
        /// To read the data on the target client: var values = Functions.SplitData(data);
        /// </summary>
        /// <param name="toContact">The recipient</param>
        /// <param name="directlyWithoutSpooler">If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</param>
        /// <param name="keyValue">Data blocks not exceeding 255 bytes each</param>
        public void SendKeyValueCollection(Contact toContact, bool directlyWithoutSpooler = false, params Tuple<byte, byte[]>[] keyValue)
        {
            SendKeyValueCollection(toContact, directlyWithoutSpooler, true, keyValue);
        }

        /// <summary>
        /// Sends a sequence of key-values, where the key is a byte and the value a byte array
        /// To read the data on the target client: var values = Functions.SplitData(data);
        /// </summary>
        /// <param name="toContact">The recipient</param>
        /// <param name="directlyWithoutSpooler">If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</param>
        /// <param name="valueMustBeLessOf256Bytes">If true: Ideal for saving values no larger than 256 bytes</param>
        /// <param name="keyValue">Data blocks of data</param>
        public void SendKeyValueCollection(Contact toContact, bool directlyWithoutSpooler = false, bool valueMustBeLessOf256Bytes = false, params Tuple<byte, byte[]>[] keyValue)
        {
            var list = new List<byte[]>();
            foreach (var tuple in keyValue)
            {
                list.Add(new byte[] { tuple.Item1 });
                list.Add(tuple.Item2);
            }
            var data = Functions.JoinData(valueMustBeLessOf256Bytes, list.ToArray());
            SendMessage(valueMustBeLessOf256Bytes ? MessageType.SmallData : MessageType.Data, data, toContact, null, null, null, directlyWithoutSpooler);
        }

        /// <summary>
        /// Sends a sequence of key-values, where the key is a byte and the value is an array of max 256 bytes
        /// To read the data on the target client: var values = Functions.SplitData(data);
        /// </summary>
        /// <param name="toContact">The recipient</param>
        /// <param name="directlyWithoutSpooler">If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</param>
        /// <param name="keyValue">Data blocks not exceeding 255 bytes each</param>
        public void SendKeyValueCollectionToCloud(Contact toContact, bool directlyWithoutSpooler = false, params Tuple<byte, byte[]>[] keyValue)
        {
            SendKeyValueCollection(toContact, directlyWithoutSpooler, keyValue);
        }

        /// <summary>
        /// Sends a sequence of key-values, where the key is a byte and the value is an array of bytes
        /// To read the data on the target client: var values = Functions.SplitData(data);
        /// </summary>
        /// <param name="toContact">The recipient</param>
        /// <param name="directlyWithoutSpooler">If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</param>
        /// <param name="valueMustBeLessOf256Bytes">Limits the length of the saved values by saving communication bandwidth for data which is generally small</param>
        /// <param name="keyValue">Data blocks, not exceeding 255 bytes each, if specified by the valueMustBeLessOf256Bytes parameter</param>
        public void SendKeyValueCollectionToCloud(Contact toContact, bool directlyWithoutSpooler = false, bool valueMustBeLessOf256Bytes = false, params Tuple<byte, byte[]>[] keyValue)
        {
            SendKeyValueCollection(toContact, directlyWithoutSpooler, valueMustBeLessOf256Bytes, keyValue);
        }

        /// <summary>
        /// The servers don't have the client's public key because they don't have the contact list. The login consists in sending your contact to the server, so that it can have the public key to communicate in encrypted form.
        /// If there are no more communications, the server will automatically remove the contact, so in the future it will be necessary to log in again
        /// </summary>
        /// <param name="directlyWithoutSpooler">If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</param>
        /// <param name="onServer">Server to login</param>
        public void LoginToServer(bool directlyWithoutSpooler, Contact onServer)
        {
            var now = DateTime.UtcNow;
            if ((now - onServer.ServerLoggedTime) >= Context.DefaultServerSessionTimeout.Add(-new TimeSpan(0, 1, 0)))
            {
                SendContact(Context.My.Contact, onServer, directlyWithoutSpooler, isLogin: true);
                onServer.ServerLoggedTime = now;
            }
        }

        /// <summary>
        /// The only type of audio file allowed is mp3, with a speed of 64 k bps or lower.
        /// </summary>
        /// <param name="mp3">mp3 64 kbps file</param>
        /// <param name="toContact">The recipient</param>
        /// <param name="replyToPostId">The post Id property of the message you want to reply to</param>
        public void SendAudio(byte[] mp3, Contact toContact, ulong? replyToPostId = null) => SendMessage(MessageType.Audio, mp3, toContact, replyToPostId);

        /// <summary>
        /// The only type of audio file allowed is mp3, with a speed of 64 k bps or lower.
        /// </summary>
        /// <param name="contact">mp3 64 kbps file</param>
        /// <param name="toContact">The recipient</param>
        /// <param name="directlyWithoutSpooler"></param>
        /// <param name="purposeIsUpdateOnly"></param>
        /// <param name="isLogin">Flad used only for the login command to avoid a recursive loop</param>
        public void SendContact(Contact contact, Contact toContact, bool directlyWithoutSpooler = false, bool purposeIsUpdateOnly = false, bool isLogin = false)
        {
            var data = ContactMessage.GetDataMessageContact(contact, Context, purposeIsUpdateOnly: purposeIsUpdateOnly);
            SendMessage(MessageType.Contact, data, toContact, null, null, null, directlyWithoutSpooler, isLogin: isLogin);
        }

        /// <summary>
        /// // I send the name with which the contract is registered in my address book, so I can have notifications with the name used locally in my contacts
        /// </summary>
        /// <param name="toContact">The recipient</param>
        public void NotifyContactNameChange(Contact toContact)
        {
            var data = toContact.Name.GetBytes();
            SendMessage(MessageType.NameChange, data, toContact);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="toContact"></param>
        public void SendContactStatus(Contact toContact)
        {
            // bit 0 = is blocked - bit 1 to 31 undefined in this version
            uint value = 0;
            if (toContact.IsBlocked)
                value |= 0b10000000_00000000_00000000_00000000; //bit 0 = is blocked
            var data = value.GetBytes();
            SendMessage(MessageType.ContactStatus, data, toContact);
        }


        /// <summary>
        /// Send last reading
        /// </summary>
        /// <param name="toContact">The recipient</param>
        internal void SendLastReading(Contact toContact)
        {
            SendMessage(MessageType.LastReading, Converter.ToUnixTimestamp(Time.CurrentTimeGMT).GetBytes(), toContact);
        }

        /// <summary>
        /// The only type of audio file allowed is mp3, with a speed of 64 k bps or lower.
        /// </summary>
        /// <param name="call">audio call</param>
        /// <param name="toContact">The recipient</param>
        public void SendAudioCall(byte[] call, Contact toContact) => SendMessage(MessageType.AudioCall, call, toContact);

        /// <summary>
        /// The only type of audio file allowed is mp3, with a speed of 64 k bps or lower.
        /// </summary>
        /// <param name="call">audio call</param>
        /// <param name="toContact">The recipient</param>
        public void SendVideoCall(byte[] call, Contact toContact) => SendMessage(MessageType.VideoCall, call, toContact);

        /// <summary>
        /// Start a group audio call for the chat room.
        /// </summary>
        /// <param name="call">Audio call</param>
        /// <param name="toContact">The recipients</param>
        public void SendStartAudioGroupCall(byte[] call, Contact toContact) => SendMessage(MessageType.StartAudioGroupCall, call, toContact);

        /// <summary>
        /// Start a group video call for the chat room.
        /// </summary>
        /// <param name="call">Video call</param>
        /// <param name="toContact">The recipients</param>
        public void SendStartVideoGroupCall(byte[] call, Contact toContact) => SendMessage(MessageType.StartAudioGroupCall, call, toContact);

        /// <summary>
        ///  End a group call for the chat room.
        /// </summary>
        /// <param name="call">Audio/Video call</param>
        /// <param name="toContact">The recipient</param>
        public void SendEndCall(byte[] call, Contact toContact) => SendMessage(MessageType.EndCall, call, toContact);

        /// <summary>
        /// Decline a call from the recipent.
        /// </summary>
        /// <param name="call"></param>
        /// <param name="toContact"></param>
        public void SendDeclinedCall(byte[] call, Contact toContact) => SendMessage(MessageType.DeclinedCall, call, toContact);

        /// <summary>
        /// Submit a geographic location
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="toContact"></param>
        /// <param name="replyToPostId"></param>
        public void SendLocation(double latitude, double longitude, Contact toContact, ulong? replyToPostId = null)
        {
            SendMessage(MessageType.Location, BitConverter.GetBytes(latitude).Combine(BitConverter.GetBytes(longitude)), toContact, replyToPostId);
        }

        /// <summary>
        /// Send a document of the format pdf.
        /// </summary>
        /// <param name="document">file to send</param>
        /// <param name="toContact">The recipient</param>
        /// <param name="replyToPostId"></param>
        public void SendPdfDocument(byte[] document, Contact toContact, ulong? replyToPostId = null) => SendMessage(MessageType.PdfDocument, document, toContact, replyToPostId);

        /// <summary>
        /// Send a contact card to the recipent 
        /// </summary>
        /// <param name="phoneContact">Contact card</param>
        /// <param name="toContact"> The recipient</param>
        /// <param name="replyToPostId"></param>
        public void SendPhoneContact(byte[] phoneContact, Contact toContact, ulong? replyToPostId = null) => SendMessage(MessageType.PhoneContact, phoneContact, toContact, replyToPostId);

        /// <summary>
        /// This command allows sub-applications (plugins, modules, extensions) to send commands with parameters. Use the "<see cref="Message.GetSubApplicationCommandWithParameters(out ushort, out ushort, out List{byte[]})"/>" method of the Message class to read this command on the receiving device
        /// </summary>
        /// <param name="toContact">Recipient</param>
        /// <param name="appId">Sub application Id (plugin Id)</param>
        /// <param name="command">Id of the command used in the protocol of the sub application</param>
        /// <param name="directlyWithoutSpooler">If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</param>
        /// <param name="encrypted">Clients are only able to receive encrypted messages. Non-encrypted messages are reserved for communications with cloud servers if the data is already encrypted and does not require a second encryption and if the message must be delivered to a server that does not have the client in the address book and therefore could not otherwise read it</param>
        /// <param name="values">Data blocks (Command parameters to use in the plugin or extension). NOTE: If you intend to send single data (not an array of parameters), use the other overload</param>
        public void SendCommandToSubApplication(Contact toContact, ushort appId, ushort command, bool directlyWithoutSpooler = false, bool encrypted = true, params byte[][] values) => SendMessage(MessageType.SubApplicationCommandWithParameters, Functions.JoinData(false, values).Combine(BitConverter.GetBytes(appId), BitConverter.GetBytes(command)), toContact, null, null, null, directlyWithoutSpooler, encrypted);
        /// <summary>
        /// This command allows sub-applications (plugins, modules, extensions) to send commands with data. Use the "<see cref="Message.GetSubApplicationCommandWithData(out ushort, out ushort, out byte[])"/>" method of the Message class to read this command on the receiving device
        /// </summary>
        /// <param name="toContact">Recipient</param>
        /// <param name="appId">Sub application Id (plugin Id)</param>
        /// <param name="command">Id of the command used in the protocol of the sub application</param>
        /// <param name="directlyWithoutSpooler">If this parameter is true, the data will be sent immediately without any reception check, if the recipient is not on-line they will be lost</param>
        /// <param name="encrypted">Clients are only able to receive encrypted messages. Non-encrypted messages are reserved for communications with cloud servers if the data is already encrypted and does not require a second encryption and if the message must be delivered to a server that does not have the client in the address book and therefore could not otherwise read it</param>
        /// <param name="data">Data relating to the command sent. NOTE: if you intend to send an array of data, use the other overload</param>
        public void SendCommandToSubApplication(Contact toContact, ushort appId, ushort command, bool directlyWithoutSpooler = false, bool encrypted = true, byte[] data = null)
        {
            if (data == null)
                data = Array.Empty<byte>();
            SendMessage(MessageType.SubApplicationCommandWithData, data.Combine(BitConverter.GetBytes(appId), BitConverter.GetBytes(command)), toContact, null, null, null, directlyWithoutSpooler, encrypted);
        }

        /// <summary>
        /// Share encrypted content on the server with other contacts. Use the "<see cref="Message.GetShareEncryptedContentData(out string, out byte[], out string, out string)"/>" method of the Message class to read this command on the receiving device
        /// </summary>
        /// <param name="toContact">Recipient</param>
        /// <param name="contentType">Three characters describing the type of content being shared. Use the three characters of the file expansion, for example: MP4, DOC, PDF, ISO, etc ...</param>
        /// <param name="privateKey">The private key to decrypt the content</param>
        /// <param name="description">Literal description of content</param>
        /// <param name="serverUrl">The URL (max 256 char) of the server where the shared content resides. The name of the file is not necessary because it is obtained from the private key. If this value is allowed, then the server will be the default one</param>
        public void ShareEncryptedContent(Contact toContact, string contentType, byte[] privateKey, string description, string serverUrl = null)
        {
            serverUrl = serverUrl ?? "";
            description = description ?? "";
#if DEBUG
            if (contentType.Length != 3)
                throw new ArgumentException("The content type must be 3 characters long");
            if (serverUrl.Length >= 256)
                throw new ArgumentException("This value must not exceed 256 characters");
#endif
            var data = Functions.JoinData(true, Encoding.ASCII.GetBytes(contentType), privateKey, Encoding.Unicode.GetBytes(description), Encoding.ASCII.GetBytes(serverUrl));
            SendMessage(MessageType.ShareEncryptedContent, data, toContact);
        }


        public void SendPushNotification(string deviceToken, ulong chatId, bool isVideo, string contactNameOrigin)
        {
            Context.CloudManager?.SendPushNotification(deviceToken, chatId, isVideo, contactNameOrigin);
        }

#if DEBUG_A
        internal const string PrivK_A = "EVsmjQouKEOrhDxuKr3gZrjMZT8BscYNYzZIiC/ZdH8=";
		internal const string PubK_B = "AsTj4Gk6atGeKPPRe1YTdltQKHAKRHTUIXv6WNt2eZJK";
#elif DEBUG_B
		internal const string PrivK_B = "3UVEkn68RJGvFmEl2/E7gflriywwQK/5rjPTQSERtIQ=";
		internal const string PubK_A =  "AoSbPis+K8FdGif6kuFEjrbZ1E63tn3dzP7w/t4eNqND";
#endif
    }
}