using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static EncryptedMessaging.MessageFormat;

namespace EncryptedMessaging.Cloud
{
    /// <summary>
    /// This class contains commands that can be sent to the cloud server
    /// </summary>
    public static class SendCloudCommands
    {
        /// <summary>
        /// Ask for contact
        /// </summary>
        /// <param name="context">Context</param>
        public static void GetBackupUser(Context context)
        {
            var param1 = Tuple.Create((byte)Keys.CommandType, ((int)CommandType.Get).GetBytes());
            var param2 = Tuple.Create((byte)Keys.Subject, ((int)Subject.BackupContact).GetBytes());
            //var param3 = Tuple.Create((byte)Keys.Id, ofUserId.GetBytes());
            context.Messaging.SendKeyValueCollectionToCloud(false, false, param1, param2);
        }

        /// <summary>
        /// Send a post message which contains the data of my contact
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="publicKey">Public key</param>
        /// <param name="userName">Name of contact</param>
        /// <param name="firebaseToken">Token Firebase</param>
        /// <param name="deviceToken">Device Token of iOS</param>
        /// <param name="sendToClient">Indicates the client where to send the post, if null the post will be sent to the cloud server</param>
        public static void PostBackupUser(Context context, byte[] publicKey, string userName, string firebaseToken, string deviceToken, Contact sendToClient = null)
        {
            var param1 = Tuple.Create((byte)Keys.CommandType, ((int)CommandType.Post).GetBytes());
            var param2 = Tuple.Create((byte)Keys.Subject, ((int)Subject.BackupContact).GetBytes());
            var param3 = Tuple.Create((byte)Keys.PubKey, publicKey);
            var param4 = Tuple.Create((byte)Keys.Name, userName.GetBytes());
            var param5 = Tuple.Create((byte)Keys.Token, firebaseToken != null ? firebaseToken.GetBytesFromASCII() : Array.Empty<byte>());
            var param6 = Tuple.Create((byte)Keys.Device, deviceToken != null ? deviceToken.GetBytesFromASCII() : Array.Empty<byte>());
            if (sendToClient != null)
                context.Messaging.SendKeyValueCollection(sendToClient, false, false, param1, param2, param3, param4, param5, param6);
            else
                context.Messaging.SendKeyValueCollectionToCloud(false, false, param1, param2, param3, param4, param5, param6);
        }

        /// <summary>
        /// Request a user's avatar from the cloud
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="ofUserId">Request this user's avatar</param>
        /// <param name="lastSize">Used to check the length of the file to avoid resending the avatar if it has not been updated</param>
        public static void GetAvatar(Context context, ulong ofUserId, int lastSize)
        {
            var param1 = Tuple.Create((byte)Keys.CommandType, ((int)CommandType.Get).GetBytes());
            var param2 = Tuple.Create((byte)Keys.Subject, ((int)Subject.Avatar).GetBytes());
            var param3 = Tuple.Create((byte)Keys.Id, ofUserId.GetBytes());
            var param4 = Tuple.Create((byte)Keys.Hash, lastSize.GetBytes()); // Used to check the length of the file to avoid resending the avatar if it has not been updated
            context.Messaging.SendKeyValueCollectionToCloud(false, false, param1, param2, param3, param4);
        }

        /// <summary>
        /// Send a post message which contains the data of a contact.
        /// You can use this command to set your avatar in the cloud, or the cloud can use this command to set a contact's avatar to the client when it asks for it.
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="data">Data of picture</param>
        /// <param name="sendToClient">Indicates the client where to send the post, if null the post will be sent to the cloud server</param>
        /// <param name="ofUserId">This parameter is mandatory if sendToClient is set</param>
        public static void PostAvatar(Context context, byte[] data, Contact sendToClient = null, ulong? ofUserId = null)
        {
            var param1 = Tuple.Create((byte)Keys.CommandType, ((int)CommandType.Post).GetBytes());
            var param2 = Tuple.Create((byte)Keys.Subject, ((int)Subject.Avatar).GetBytes());
            var param3 = Tuple.Create((byte)Keys.Data, data ?? Array.Empty<byte>());
            if (sendToClient != null)
            {
                if (ofUserId != null)
                {
                    var param4 = Tuple.Create((byte)Keys.Id, BitConverter.GetBytes((ulong)ofUserId));
                    context.Messaging.SendKeyValueCollection(sendToClient, false, false, param1, param2, param3, param4);
                }
            }
            else
            {
                context.Messaging.SendKeyValueCollectionToCloud(false, false, param1, param2, param3);
            }
        }

        /// <summary>
        /// Send an item to save to the cloud or to keep on the client
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="objectTypeName">The name of the object type, for example "String", is used to group objects</param>
        /// <param name="key">An identification key allows you to identify the data by giving it a name</param>
        /// <param name="objectData">The data that represents the object</param>
        /// <param name="sendToClient">Indicates the client where to send the post, if null the post will be sent to the cloud server</param>
        public static void PostObject(Context context, string objectTypeName, string key, byte[] objectData, Contact sendToClient = null)
        {
            var param1 = Tuple.Create((byte)Keys.CommandType, ((int)CommandType.Post).GetBytes());
            var param2 = Tuple.Create((byte)Keys.Subject, ((int)Subject.DataStorage).GetBytes());
            var param3 = Tuple.Create((byte)Keys.Name, objectTypeName.GetBytesFromASCII());
            var param4 = Tuple.Create((byte)Keys.Id, key.GetBytesFromASCII());
            var param5 = Tuple.Create((byte)Keys.Data, objectData);
            if (sendToClient != null)
                context.Messaging.SendKeyValueCollection(sendToClient, false, false, param1, param2, param3, param4, param5);
            else
                context.Messaging.SendKeyValueCollectionToCloud(false, false, param1, param2, param3, param4, param5);
        }

        /// <summary>
        /// Delete a object in the cloud
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="objectTypeName">The name of the object type, for example "String", is used to group objects</param>
        /// <param name="key">An identification key allows you to identify the data by giving it a name</param>
        /// <param name="sendToClient">Indicates the client where to send the post, if null the post will be sent to the cloud server</param>
        public static void DeleteObject(Context context, string objectTypeName, string key, Contact sendToClient = null)
        {
            var param1 = Tuple.Create((byte)Keys.CommandType, ((int)CommandType.Post).GetBytes());
            var param2 = Tuple.Create((byte)Keys.Subject, ((int)Subject.DataStorage).GetBytes());
            var param3 = Tuple.Create((byte)Keys.Name, objectTypeName.GetBytesFromASCII());
            var param4 = Tuple.Create((byte)Keys.Id, key.GetBytesFromASCII());
            if (sendToClient != null)
                context.Messaging.SendKeyValueCollection(sendToClient, false, false, param1, param2, param3, param4);
            else
                context.Messaging.SendKeyValueCollectionToCloud(false, false, param1, param2, param3, param4);
        }

        /// <summary>
        /// Send an get request to cloud or client.  The response are processed in class ProcessResponsesFromCloud the method <see cref="ProcessResponsesFromCloud.OnData(Context, string, string, byte[])">OnData</see>
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="objectTypeName">The name of the object type, for example "String", is used to group objects</param>
        /// <param name="key">Object ID, if null then is a request of all object</param>
        /// <param name="sendToClient">Indicates the client where to send the request, if null the request will be sent to the cloud server</param>
        public static void GetObject(Context context, string objectTypeName, string key, Contact sendToClient = null)
        {
            var param1 = Tuple.Create((byte)Keys.CommandType, ((int)CommandType.Get).GetBytes());
            var param2 = Tuple.Create((byte)Keys.Subject, ((int)Subject.DataStorage).GetBytes());
            var param3 = Tuple.Create((byte)Keys.Name, objectTypeName.GetBytesFromASCII());
            var param4 = Tuple.Create((byte)Keys.Id, key == null ? Array.Empty<byte>() : key.GetBytesFromASCII());
            if (sendToClient != null)
                context.Messaging.SendKeyValueCollection(sendToClient, false, false, param1, param2, param3, param4);
            else
                context.Messaging.SendKeyValueCollectionToCloud(false, false, param1, param2, param3, param4);
        }

        /// <summary>
        /// Send an get request to cloud or client. The response are processed in class ProcessResponsesFromCloud the method <see cref="ProcessResponsesFromCloud.OnData(Context, string, string, byte[])">OnData</see>
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="objectTypeName">The name of the object type, for example "String", is used to group objects</param>
        /// <param name="sendToClient">Indicates the client where to send the request, if null the request will be sent to the cloud server</param>
        public static void GetAllObject(Context context, string objectTypeName, Contact sendToClient = null)
        {
            GetObject(context, objectTypeName, null, sendToClient);
        }


        public static void PostPushNotification(Context context, string deviceToken, ulong chatId, bool isVideo, string contactNameOrigin)
        {
            var param1 = Tuple.Create((byte)Keys.CommandType, ((int)CommandType.Post).GetBytes());
            var param2 = Tuple.Create((byte)Keys.Subject, ((int)Subject.PushNotification).GetBytes());
            var param3 = Tuple.Create((byte)Keys.Device, deviceToken.GetBytesFromASCII());
            var param4 = Tuple.Create((byte)Keys.Id, chatId.GetBytes());
            var param5 = Tuple.Create((byte)Keys.IsVideo, (isVideo ? 1 : 0).GetBytes());
            var param6 = Tuple.Create((byte)Keys.Name, contactNameOrigin.GetBytes());
            context.Messaging.SendKeyValueCollectionToCloud(true, false, param1, param2, param3, param4, param5, param6);
        }
    }

    public static class ReceiveCloudCommands
    {
        /// <summary>
        /// This routine performs the necessary actions for received messages, both Post and Get
        /// </summary>
        /// <param name="keyValue">Pairs of keys and values received</param>
        private static void ExecuteMessage(Context context, Message message, Dictionary<byte, byte[]> keyValue)
        {
            var creation = message.Creation;
            var fromContact = message.Contact;
            var commandType = (CommandType)(keyValue[(byte)Keys.CommandType]?[0]);
            var request = (Subject)(keyValue[(byte)Keys.Subject]?[0]);
            if (message.Encrypted)
            {
                var id = (ulong)fromContact.UserId;
                if (request == Subject.BackupContact)
                {
                    if (commandType == CommandType.Get && context.IsServer)
                    {
                        var backupContact = LoadData(id, "BackupContact");
                        if (backupContact != null)
                        {
                            var parts = Functions.SplitData(backupContact, true);
                            var pubKey = parts[0];
                            var userName = parts[1];
                            var firebaseToken = parts[2];
                            var deviceToken = parts[3];
                            SendCloudCommands.PostBackupUser(context, pubKey, Encoding.Unicode.GetString(userName), Encoding.ASCII.GetString(firebaseToken), Encoding.ASCII.GetString(deviceToken), fromContact);
                        }
                    }
                    else if (commandType == CommandType.Post)
                    {
                        var pubKey = keyValue[(byte)Keys.PubKey];
                        var userName = keyValue[(byte)Keys.Name];
                        var firebaseToken = keyValue[(byte)Keys.Token];
                        var deviceToken = keyValue[(byte)Keys.Device];
                        if (context.IsServer)
                        {
                            var backupContact = Functions.JoinData(true, pubKey, userName, firebaseToken);
                            SaveData(id, "BackupContact", backupContact);
                            Counter.NewUser++;
                        }
                        else
                        {
                            var contact = context.Contacts.GetContactByUserID(id);
                            if (contact != null && contact.PublicKeys == pubKey.ToASCII())
                            {
                                contact._firebaseToken = firebaseToken.ToASCII(); // set the internal parameter to don't Save automatically
                                contact._deviceToken = deviceToken.ToASCII();
                                contact.Name = userName.ToUnicode(); // Save automatically the contact
                                                                     //contact.Save();				
                            }
                        }
                    }
                }
                else if (request == Subject.Avatar)
                {
                    if (commandType == CommandType.Get && context.IsServer)
                    {
                        var userId = BitConverter.ToUInt64(keyValue[(byte)Keys.Id], 0);
                        var hash = BitConverter.ToInt32(keyValue[(byte)Keys.Hash], 0);
                        var avatar = LoadData(userId, "Avatar");
                        if (avatar != null && avatar.Length != hash) // Check the file length to send the avatar only if it has been updated
                            SendCloudCommands.PostAvatar(context, avatar, fromContact, userId);
                    }
                    else if (commandType == CommandType.Post)
                    {
                        var data = keyValue[(byte)Keys.Data];
                        if (context.IsServer)
                        {
                            SaveData(id, "Avatar", data);
                        }
                        else
                        {
                            if (keyValue.TryGetValue((byte)Keys.Id, out var avatarUserId))
                            {
                                var avatarId = BitConverter.ToUInt64(avatarUserId, 0);
                                var contact = context.Contacts.GetContactByUserID(avatarId);
                                if (contact != null)
                                {
                                    // The first eight bytes of a PNG file always contain the following (decimal) values: 137 80 78 71 13 10 26 10
                                    //if (data[0] == 137 && data[1] == 80 && data[2] == 78 && data[3] == 71 && data[4] == 13 && data[5] == 10 && data[6] == 26 && data[7] == 10)
                                    //	contact.Avatar = data; // Compatibility with the old version which sends unencrypted avatars to the cloud
                                    //else
                                    //{
                                    try
                                    {
                                        var myKey = context.My.GetPublicKeyBinary();
                                        var contactKey = contact.Participants.Find(x => !x.SequenceEqual(myKey));
                                        contact.Avatar = Functions.Decrypt(data, contactKey); // The avatar is public but is encrypted using the contact's public key as a password, in this way it can only be decrypted by users who have this contact in the address book
                                    }
                                    catch (Exception e)
                                    {
                                        System.Diagnostics.Debug.WriteLine(e.Message);
                                        contact.Avatar = data;
                                    }
                                    //}
                                }
                            }
                        }
                    }
                }
                else if (request == Subject.DataStorage)
                {
                    var key = keyValue[(byte)Keys.Id].ToASCII();
                    var groupName = keyValue[(byte)Keys.Name].ToASCII();
                    if (commandType == CommandType.Get && context.IsServer)
                    {
                        Counter.GetDataStorage++;
                        byte[] data = null;
                        if (key.Length == 0) // request get all data stored
                        {
                            var dataCollection = LoadAllData(id, groupName);
                            if (dataCollection != null)
                                data = Functions.JoinData(false, dataCollection.ToArray());
                        }
                        else
                        {
                            data = LoadData(id, key, groupName);
                        }
                        if (data != null)
                            SendCloudCommands.PostObject(context, groupName, key, data, fromContact);
                    }
                    else if (commandType == CommandType.Post)
                    {
                        Counter.PostDataStorage++;
                        if (keyValue.TryGetValue((byte)Keys.Data, out var data))
                        {
                            if (context.IsServer)
                            {
                                SaveData(id, key, data, groupName);
                            }
                            else if (fromContact.IsServer)
                            {
                                ProcessResponsesFromCloud.OnResponse(context, groupName, key, data);
                            }
                        }
                        else // delete
                        {
                            DeleteData(id, key, groupName);
                        }
                    }
                }
            }

            if (context.IsServer)
            {
                if (commandType == CommandType.Post)
                    if (OnPost.TryGetValue(request, out var onPost))
                        onPost.Invoke(message, keyValue);
                    else if (commandType == CommandType.Get)
                        if (OnGet.TryGetValue(request, out var onGet))
                            onGet.Invoke(message, keyValue);
            }

        }
        public static Dictionary<Subject, Action<Message, Dictionary<byte, byte[]>>> OnPost = new Dictionary<Subject, Action<Message, Dictionary<byte, byte[]>>>();
        public static Dictionary<Subject, Action<Message, Dictionary<byte, byte[]>>> OnGet = new Dictionary<Subject, Action<Message, Dictionary<byte, byte[]>>>();
       /// <summary>
       /// Set a custom path to the account.
       /// </summary>
       /// <param name="customPath">String</param>
       /// <param name="eraseNotUsedAccount">Boolean</param>
        public static void SetCustomPath(string customPath, bool eraseNotUsedAccount)
        {
            CustomPath = customPath;
            if (eraseNotUsedAccount && customPath != null)
                EraseNotUsedAccountTimer = new System.Threading.Timer(new System.Threading.TimerCallback((state) => EraseNotUsedAccount()), null, new TimeSpan(24, 0, 0), new TimeSpan(24, 0, 0));
        }
        private static string CustomPath = null;
        private static string GetPath(ulong forUserId, string group = null)
        {
            var appData = AppData();
            var directory = "id" + forUserId;
            return group == null ? Path.Combine(appData, directory) : Path.Combine(appData, directory, group);
        }
        private static string AppData() => Path.Combine(CustomPath ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppData");
        private const string Extension = ".d";
        private static System.Threading.Timer EraseNotUsedAccountTimer;
        /// <summary>
        /// Erase the data of accounts that are no longer in use. If you use a custom path to keep data on the cloud, a timer will periodically call this function to clean up
        /// </summary>
        public static void EraseNotUsedAccount()
        {
            if (!EraseRunning)
            {
                EraseRunning = true;
                var dirs = new DirectoryInfo(AppData());
                if (dirs.Exists)
                {
                    foreach (var dir in dirs.GetDirectories())
                    {
                        var files = dir.GetFiles("*.*", SearchOption.AllDirectories);
                        FileInfo newer = null;
                        foreach (var file in files)
                        {
                            if (newer == null)
                                newer = file;
                            else if (file.LastWriteTimeUtc > newer.LastWriteTimeUtc)
                                newer = file;
                        }
                        if (newer != null)
                        {
                            var lastWriteTimeInDays = (DateTime.UtcNow - newer.LastWriteTimeUtc).TotalDays;
                            if (lastWriteTimeInDays > EraseAfterDays || files.Length <= 2 && lastWriteTimeInDays >= 10) // Delete accounts that have not been used for more than a year OR clear data of newly created accounts that have not been used (usually these are test accounts to test the application)
                            {
                                try { dir.Delete(true); }
                                catch (Exception ex) { }
                            }
                        }
                    }
                }
                EraseRunning = false;
            }
        }
        private const double EraseAfterDays = 365;
        private static bool EraseRunning;
        private static void SaveData(ulong forUserId, string dataName, byte[] data, string group = null)
        {
            var path = GetPath(forUserId, group);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            var fileName = Path.Combine(path, dataName) + Extension;
            if (data == null || data.Length == 0)
                File.Delete(fileName);
            else
                File.WriteAllBytes(fileName, data);
        }
        private static void DeleteData(ulong forUserId, string dataName, string group = null)
        {
            var path = GetPath(forUserId, group);
            if (Directory.Exists(path))
            {
                var fileName = Path.Combine(path, dataName) + Extension;
                if (File.Exists(fileName))
                    File.Delete(fileName);
            }
        }
        private static byte[] LoadData(ulong forUserId, string dataName, string group = null)
        {
            var path = GetPath(forUserId, group);
            if (!Directory.Exists(path))
                return null;
            var fileName = Path.Combine(path, dataName) + Extension;
            return !File.Exists(fileName) ? null : File.ReadAllBytes(fileName);
        }
        private static List<byte[]> LoadAllData(ulong forUserId, string group = null)
        {
            var path = GetPath(forUserId, group);
            if (!Directory.Exists(path))
                return null;
            var files = Directory.GetFiles(path, "*" + Extension);
            if (files.Length == 0)
                return null;
            var datas = new List<byte[]>();
            foreach (var file in files)
            {
                datas.Add(File.ReadAllBytes(file));
            }
            return datas;
        }

        /// <summary>
        /// This event is automatically executed when a command is received from another client. You can receive its request (Get) and data entry (Post) commands
        /// </summary>
        /// <param name="context">Command type</param>
        /// <param name="message">The client contact who created the command</param>
        internal static void OnCommand(Context context, Message message)
        {
            if (message.Type == MessageType.SmallData || message.Type == MessageType.Data)
            {
                var keyValue = Functions.SplitIncomingData(message.Data, message.Type == MessageType.SmallData);
                if (keyValue[(byte)CommandType.Post] != null || keyValue[(byte)CommandType.Get] != null)
                {
                    ExecuteMessage(context, message, keyValue);
                }
            }
        }
    }

    public static class Counter
    {
        public static int NewUser;
        public static int GetDataStorage;
        public static int PostDataStorage;
    }

    //=============================================================================

    // Keys
    public enum Keys : byte
    {
        CommandType,
        Subject,
        Id,
        Data,
        Name,
        PubKey,
        Token,
        IsVideo,
        Device,
        All,
        Hash,
    }

    // Values
    public enum CommandType : byte
    {
        Post,
        Get,
    }

    public enum Subject : byte
    {
        BackupContact, // backup my user account
        PushNotification,
        DataStorage,
        Avatar,
        ClientRequest,
    }

}
