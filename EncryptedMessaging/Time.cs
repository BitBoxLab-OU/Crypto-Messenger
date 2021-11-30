using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace EncryptedMessaging
{
	public static class Time
	{
		private static readonly bool EnablePrecideDateTime = false;
		private static readonly IsolatedStorageFile _isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly | IsolatedStorageScope.Domain, null, null);
		static bool _updated;
		static TimeSpan _delta = new TimeSpan(long.MinValue);
		static readonly object _lockObj = new object();
		public static DateTime CurrentTimeGMT
		{
			get
			{
				lock (_lockObj)
				{
					if (!EnablePrecideDateTime)
						return DateTime.UtcNow;
					var loaded = false;
					if (!_updated)
					{
						if (GetDateTimeSaved(out _, out _delta))
						{
							_updated = true;
							loaded = true;
						}
						else if (GetNistTime(out _, out _delta))
						{
							_updated = true;
						}
						else if (GetAverageDateTimeFromWeb(out _, out _delta))
						{
							_updated = true;
						}
						if (_updated && !loaded)
						{
							using (var stream = new IsolatedStorageFileStream("deltaTime", FileMode.Create, FileAccess.Write, _isoStore))
							{
								stream.Write(_delta.Ticks.GetBytes(), 0, 8);
								stream.Write((DateTime.UtcNow + _delta).Ticks.GetBytes(), 0, 8);
							}
						}
					}
				}
				return _updated ? DateTime.UtcNow + _delta : DateTime.UtcNow;
			}
		}
		private static bool GetDateTimeSaved(out DateTime dateTime, out TimeSpan delta)
		{
			if (_isoStore.FileExists("deltaTime"))
			{

				using (var stream = new IsolatedStorageFileStream("deltaTime", FileMode.Open, FileAccess.Read, _isoStore))
				{
					while (stream.Position < stream.Length)
					{
						var dataLong = new byte[8];
						stream.Read(dataLong, 0, 8);
						delta = new TimeSpan(BitConverter.ToInt64(dataLong, 0));
						stream.Read(dataLong, 0, 8);
						var saved = new DateTime(BitConverter.ToInt64(dataLong, 0));
						dateTime = DateTime.UtcNow + delta;
						return (saved - dateTime).TotalDays <= 1;
					}
				}
			}
			delta = new TimeSpan();
			dateTime = DateTime.UtcNow;
			return false;
		}

		private static bool GetAverageDateTimeFromWeb(out DateTime dateTime, out TimeSpan delta)
		{
			var webs = new[] {
				"https://foundation.mozilla.org/",
				"https://www.timeanddate.com/",
				"https://www.time.gov/",
				"http://www.wikipedia.org/",
				"https://www.facebook.com/",
				"https://www.linuxfoundation.org/",
				"https://m.youtube.com/",
				"https://www.vk.com/",
				"https://www.amazon.com/",
				"https://www.google.com/",
				"https://www.microsoft.com/",
				"https://nist.time.gov/",
				"https://www.google.co.in/",
				"https://www.rolex.com/",
				"https://creativecommons.org/"
			};
			var deltas = new List<TimeSpan>();
			for (var i = 1; i <= 1; i++)
				foreach (var web in webs)
				{
					var time = GetDateTimeFromWeb(web);
					if (time != null)
						deltas.Add(DateTime.UtcNow - (DateTime)time);
				}
			if (deltas.Count == 0)
			{
				dateTime = DateTime.UtcNow;
				return false;
			}
			var middle = deltas.Count / 2;
			delta = deltas.Count % 2 == 0 ? new TimeSpan(deltas[middle].Ticks / 2 + deltas[middle + 1].Ticks / 2) : deltas[middle];
			dateTime = DateTime.UtcNow.Add(delta);
			return true;
		}

		private static DateTime? GetDateTimeFromWeb(string fromWebsite)
		{
			using (var client = new HttpClient())
			{
				try
				{
					var result = client.GetAsync(fromWebsite, HttpCompletionOption.ResponseHeadersRead).Result;
					if (result.Headers?.Date != null)
						return result.Headers?.Date.Value.UtcDateTime.AddMilliseconds(366); // for stats the time of website have a error of 366 ms; 					
				}
				catch
				{
					// ignored
				}
				return null;
			}
		}

		private static bool GetNistTime(out DateTime dateTime, out TimeSpan delta)
		{
			try
			{
				var request = (HttpWebRequest)WebRequest.Create("http://nist.time.gov/actualtime.cgi?lzbc=siqm9b");
				request.Method = "GET";
				request.Accept = "text/html, application/xhtml+xml, */*";
				request.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)";
				request.ContentType = "application/x-www-form-urlencoded";
				request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore); //No caching
				var response = (HttpWebResponse)request.GetResponse();
				if (response.StatusCode != HttpStatusCode.OK) { dateTime = DateTime.UtcNow; return false; }
				var stream = new StreamReader(response.GetResponseStream() ?? throw new InvalidOperationException());
				var html = stream.ReadToEnd();
				var time = Regex.Match(html, @"(?<=\btime="")[^""]*").Value;
				var milliseconds = Convert.ToInt64(time) / 1000.0;
				dateTime = new DateTime(1970, 1, 1).AddMilliseconds(milliseconds);
				delta = DateTime.UtcNow - dateTime;
				return true;
			}
			catch
			{
				dateTime = DateTime.UtcNow;
				return false;
			}
		}
	}
}
