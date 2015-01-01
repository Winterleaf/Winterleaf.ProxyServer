// WinterLeaf Entertainment
// Copyright (c) 2014, WinterLeaf Entertainment LLC
// 
// 
// THIS SOFTWARE IS PROVIDED BY WINTERLEAF ENTERTAINMENT LLC ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES,
//  INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR 
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL WINTERLEAF ENTERTAINMENT LLC BE LIABLE FOR ANY DIRECT, INDIRECT, 
// INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND 
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR 
// OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH 
// DAMAGE. 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using log4net;

namespace Winterleaf.ProxyServer
{
    public static class ProxyCache
    {
        private static Hashtable _cache = new Hashtable();
        private static Object _cacheLockObj = new object();
        private static Object _statsLockObj = new object();
        private static Int32 _hits;

        public static CacheEntry GetData(HttpWebRequest request)
        {
            CacheKey key = new CacheKey(request.RequestUri.AbsoluteUri, request.UserAgent);
            if (_cache[key] != null)
                {
                CacheEntry entry = (CacheEntry) _cache[key];
                if (entry.FlagRemove || (entry.Expires.HasValue && entry.Expires < DateTime.Now))
                    {
                    //don't remove it here, just flag
                    entry.FlagRemove = true;
                    return null;
                    }
                Monitor.Enter(_statsLockObj);
                _hits++;
                Monitor.Exit(_statsLockObj);
                return entry;
                }
            return null;
        }

        public static CacheEntry MakeEntry(HttpWebRequest request, HttpWebResponse response, List<Tuple<String, String>> headers, DateTime? expires)
        {
            CacheEntry newEntry = new CacheEntry();
            newEntry.Expires = expires;
            newEntry.DateStored = DateTime.Now;
            newEntry.Headers = headers;
            newEntry.Key = new CacheKey(request.RequestUri.AbsoluteUri, request.UserAgent);
            newEntry.StatusCode = response.StatusCode;
            newEntry.StatusDescription = response.StatusDescription;
            if (response.ContentLength > 0)
                newEntry.ResponseBytes = new Byte[response.ContentLength];
            return newEntry;
        }

        public static void AddData(CacheEntry entry)
        {
            Monitor.Enter(_cacheLockObj);
            if (!_cache.Contains(entry.Key))
                _cache.Add(entry.Key, entry);
            Monitor.Exit(_cacheLockObj);
        }

        public static Boolean CanCache(WebHeaderCollection headers, ref DateTime? expires)
        {
            foreach (String s in headers.AllKeys)
                {
                String value = headers[s].ToLower();
                switch (s.ToLower())
                    {
                        case "cache-control":
                            if (value.Contains("max-age"))
                                {
                                int seconds;
                                if (int.TryParse(value, out seconds))
                                    {
                                    if (seconds == 0)
                                        return false;
                                    DateTime d = DateTime.Now.AddSeconds(seconds);
                                    if (!expires.HasValue || expires.Value < d)
                                        expires = d;
                                    }
                                }

                            if (value.Contains("private") || value.Contains("no-cache"))
                                return false;
                            else if (value.Contains("public") || value.Contains("no-store"))
                                return true;

                            break;

                        case "pragma":

                            if (value == "no-cache")
                                return false;

                            break;
                        case "expires":
                            DateTime dExpire;
                            if (DateTime.TryParse(value, out dExpire))
                                {
                                if (!expires.HasValue || expires.Value < dExpire)
                                    expires = dExpire;
                                }
                            break;
                    }
                }
            return true;
        }

        public static void CacheMaintenance(ILog log)
        {
            try
                {
                while (true)
                    {
                    Thread.Sleep(30000);
                    List<CacheKey> keysToRemove = new List<CacheKey>();
                    foreach (CacheKey key in _cache.Keys)
                        {
                        CacheEntry entry = (CacheEntry) _cache[key];
                        if (entry.FlagRemove || entry.Expires < DateTime.Now)
                            keysToRemove.Add(key);
                        }

                    foreach (CacheKey key in keysToRemove)
                        _cache.Remove(key);

                    log.Info(String.Format("Cache maintenance complete.  Number of items stored={0} Number of cache hits={1} ", _cache.Count, _hits));
                    }
                }
            catch (ThreadAbortException)
                {
                }
        }

    }
}