using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommunicationChannel;

/*
==========================================================================================
This library provides functions to retrieve messages on the server and store them locally.
Use the NetworkManager library for data transmission.
==========================================================================================
*/

namespace EncryptedMessaging
{
    public class Repository
    {
        internal Dictionary<DateTime, ulong> ReceptionToPostId = new Dictionary<DateTime, ulong>();

        public Repository(Context context) => _context = context;
        private readonly Context _context;
        public const int MaxPostLength = 20971520; //20 MegaByte
        private string _path(ulong chatId) => MapPath(Path.Combine(_context.My.GetId().ToString("X", System.Globalization.CultureInfo.InvariantCulture), _context.Domain.ToString("X", System.Globalization.CultureInfo.InvariantCulture), chatId.ToString("X", System.Globalization.CultureInfo.InvariantCulture)));

        /// <summary>
        /// Add the encrypted post to local storage and return the reception date
        /// </summary>
        /// <param name="dataByteArray"></param>
        /// <param name="chatId"></param>
        /// <param name="receptionDate">return reception date</param>
        /// <returns></returns>
        public void AddPost(byte[] dataByteArray, ulong chatId, ref DateTime receptionDate)
        {
            if (receptionDate == default)
                receptionDate = DateTime.UtcNow;
            var path = _path(chatId);
            Directory.CreateDirectory(path);
            if (dataByteArray != null)
            {
                var fileName = GetFileName(receptionDate, chatId);
                File.WriteAllBytes(fileName, dataByteArray);
                lock (ReceptionToPostId)
                {
                    if (!ReceptionToPostId.ContainsKey(receptionDate))
                        ReceptionToPostId.Add(receptionDate, PostId(dataByteArray));
                }
            }
        }

        private string GetFileName(DateTime dateTime, ulong chatId) => Path.Combine(_path(chatId), dateTime.Ticks.ToString("X", System.Globalization.CultureInfo.InvariantCulture) + ".post");

        private static DateTime GetFileDate(string fileName) => new DateTime(Convert.ToInt64(Path.GetFileName(fileName).Split('.')[0], 16));

        /// <summary>
        /// Read all posts of a given chat and perform an action. If no action is specified, then the default action will be to show messages in the chat
        /// </summary>
        /// <param name="chatId">The id of the chat whose posts you want to read</param>
        /// <param name="action">Action to be performed for each post, the byte[] is binary data of the encrypted post that is read from the repository, DateTime is the time the post was received which you can use as a unique ID (you can use the ticks property of DateTime as a unique id ) </param>
        /// <param name="antecedent">If set, consider only posts that are dated before the value indicated. It is useful for paginating messages in the chat view, or for telling the loading of messages in blocks. How to use this parameter: You need to store the date of the oldest message that is displayed in the chat, when you want to load a second block of messages you have to pass this date in order to get the next block</param>
        /// <param name="take">Limit the number of messages to take. If not set, the value set in the Context.Setting.MessagePagination settings will be used. Pass the Context.Setting.KeepPost value to process all messages!</param>
        /// <param name="exclude">List of posts to exclude using the received date as a filter</param>
        /// <returns>Returns the date of arrival of the oldest message processed by the function. Use this value to page further requests by passing the "antecedent" parameter.</returns>
        public DateTime ReadPosts(ulong chatId, Action<byte[], DateTime> action = null, DateTime antecedent = default, int? take = null, List<DateTime> exclude = null)
        {
            DateTime olderPost = DateTime.MaxValue;
            var path = _path(chatId);
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*.post");
                var filesList = new List<postFile>();
                foreach (var file in files)
                    filesList.Add(new postFile { FileName = file, ReceptionDate = GetFileDate(file) });

                //remove old file
                var n = 0;
                foreach (var file in filesList)
                {
                    if (n >= _context.Setting.KeepPost || (Time.CurrentTimeGMT - file.ReceptionDate >= TimeSpan.FromDays(_context.Setting.PostPersistenceDays)))
                    {
                        File.Delete(file.FileName);
                        filesList.Remove(file);
                        lock (ReceptionToPostId)
                        {
                            if (ReceptionToPostId.ContainsKey(file.ReceptionDate))
                                ReceptionToPostId.Remove(file.ReceptionDate);
                        }
                    }
                    n++;
                }
                filesList = filesList.OrderByDescending(o => o.ReceptionDate).ToList();
                var skip = 0;
                if (antecedent != default)
                {
                    foreach (var file in filesList)
                    {
                        if (file.ReceptionDate < antecedent)
                            break;
                        skip++;
                    }
                }
                if (take == null)
                    take = _context.Setting.MessagePagination;
                var partialList = filesList.Skip(skip).Take((int)take).ToArray();
                if (partialList.Length > 0)
                    olderPost = partialList.Last().ReceptionDate;
                _context.Contacts.RefreshSuspend = true;
                foreach (var file in partialList)
                {
                    if (exclude == null || !exclude.Contains(file.ReceptionDate))
                    {
                        var data = File.ReadAllBytes(file.FileName);
                        if (action != null)
                        {
                            action(data, file.ReceptionDate);
                        }
                        else
                        {
                            lock (ReceptionToPostId)
                            {
                                if (!ReceptionToPostId.ContainsKey(file.ReceptionDate))
                                    ReceptionToPostId.Add(file.ReceptionDate, PostId(data));
                            }
                            _context.Messaging.ShowPost(data, chatId, file.ReceptionDate);
                        }
                    }
                }
                _context.Contacts.RefreshSuspend = false;
            }
            return olderPost;
        }

        private struct postFile
        {
            public string FileName;
            public DateTime ReceptionDate;
        }

        public byte[] ReadLastPost(ulong chatId, out DateTime receptionTime)
        {
            receptionTime = DateTime.MinValue;
            if (!Directory.Exists(_path(chatId)))
                return null;
            var files = Directory.GetFiles(_path(chatId), "*.post");
            Array.Sort(files, StringComparer.InvariantCulture);
            if (files.Length == 0)
                return null;
            else
            {
                receptionTime = GetFileDate(files[files.Length - 1]);
                return File.ReadAllBytes(files[files.Length - 1]);
            }
        }

        public Message GetLastMessageViewable(ulong chatId, out DateTime receptionTime)
        {
            receptionTime = DateTime.MinValue;
            if (!Directory.Exists(_path(chatId)))
                return null;
            var files = Directory.GetFiles(_path(chatId), "*.post");
            Array.Sort(files, StringComparer.InvariantCulture);
            if (files.Length == 0)
                return null;
            else
            {
                foreach (var file in files.Reverse())
                {
                    try
                    {
                        receptionTime = GetFileDate(file);
                        var data = File.ReadAllBytes(file);
                        if (_context.MessageFormat.ReadDataPost(data, chatId, receptionTime, out var message))
                        {
                            if (MessageFormat.MessageDescription.ContainsKey(message.Type))
                                return message;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            return null;
        }

        public byte[] ReadPost(DateTime receptionDateTime, ulong chatId)
        {
            var fileName = GetFileName(receptionDateTime, chatId);
            return File.Exists(fileName) ? File.ReadAllBytes(fileName) : null;
        }
        public byte[] ReadPostByPostId(ulong postId, ulong chatId)
        {
            var receptionDateTime = DateTime.MinValue;
            lock (ReceptionToPostId)
            {
                ReceptionToPostId.ToList().ForEach(x =>
                {
                    if (x.Value == postId)
                    {
                        receptionDateTime = x.Key;
                    }
                });
            }
            return receptionDateTime != DateTime.MinValue ? ReadPost(receptionDateTime, chatId) : null;
        }

        public void DeletePost(DateTime receptionDateTime, Contact contact)
        {
            var fileName = GetFileName(receptionDateTime, contact.ChatId);
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
                lock (ReceptionToPostId)
                {
                    if (ReceptionToPostId.ContainsKey(receptionDateTime))
                        ReceptionToPostId.Remove(receptionDateTime);
                }
            }
            contact.UpdateLastMessagePreview();
        }

        public void DeletePostByPostId(ulong postId, ulong chatId)
        {
            var contact = _context.Contacts.GetContact(chatId);
            if (contact != null)
                DeletePostByPostId(postId, contact);
        }

        public void DeletePostByPostId(ulong postId, Contact contact)
        {
            lock (ReceptionToPostId)
            {
                ReceptionToPostId.ToList().ForEach(x =>
                {
                    if (x.Value == postId)
                    {
                        ReceptionToPostId.Remove(x.Key);
                        DeletePost(x.Key, contact);
                        _context.SecureStorage.ObjectStorage.DeleteObject(typeof(string), "t" + postId);
                    }
                });
            }
        }

        public void ClearPosts(Contact contact)
        {
            try
            {
                var directoryInfo = new DirectoryInfo(_path(contact.ChatId));
                if (directoryInfo != null)
                    foreach (var file in directoryInfo.GetFiles())
                    {
                        file.Delete();
                        var post = new postFile { FileName = file.Name, ReceptionDate = GetFileDate(file.Name) };
                        lock (ReceptionToPostId)
                        {
                            if (ReceptionToPostId.ContainsKey(post.ReceptionDate))
                                ReceptionToPostId.Remove(post.ReceptionDate);
                        }
                    }
            }
            catch (Exception)
            {
            }
        }

        private static string MapPath(string pathNameFile)
        {
            //return System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), PathNameFile);
            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(path, pathNameFile);
        }

        public static DateTime GetDateTimeOfData(byte[] data) => Converter.FromUnixTimestamp(GetTimestampOfData(data));

        public static int GetTimestampOfData(byte[] data)
        {
#if DEBUG || DEBUG_A || DEBUG_B
            if (data == null || data[0] != 0)
            {
                Debug.WriteLine("This feature is for version 0 of the encrypted messaging protocol");
                Debugger.Break();
            }
#endif
            return Converter.BytesToInt(data.Skip(1).Take(4).ToArray());
        }
        public static ulong PostId(byte[] dataPost)
        {
            var p1 = dataPost.Length < 8
                                ? BitConverter.ToUInt64(dataPost.Combine(new byte[8]).Take(8), 0)
                                : BitConverter.ToUInt64(dataPost.Skip(dataPost.Length - 8), 0);
            var p2 = dataPost.Length < 8
                                ? BitConverter.ToUInt64(new byte[8].Combine(dataPost).Skip(dataPost.Length).Reverse(), 0)
                                : BitConverter.ToUInt64(dataPost.Take(8).Reverse(), 0);
            return p1 ^ p2;
        }

    }
}