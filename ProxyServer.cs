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
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;
using log4net.Config;

[assembly: XmlConfigurator(Watch = true)]

namespace Winterleaf.ProxyServer
{
    public sealed class ProxyServer
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly ProxyServer _server = new ProxyServer();

        private static readonly int BUFFER_SIZE = 8192;
        private static readonly char[] semiSplit = new char[] {';'};
        private static readonly char[] equalSplit = new char[] {'='};
        private static readonly String[] colonSpaceSplit = new string[] {": "};
        private static readonly char[] spaceSplit = new char[] {' '};
        private static readonly char[] commaSplit = new char[] {','};
        private static readonly Regex cookieSplitRegEx = new Regex(@",(?! )");
        private static string _certFullFolderPath = "";
        private static string _certFolder = "TemporaryCertificates";
        private static object locker = new object();

        //private static X509Certificate2 _certificate;
        private static object _outputLockObj = new object();
        private Thread _cacheMaintenanceThread;

        private TcpListener _listener;
        private Thread _listenerThread;

        private ProxyServer()
        {
            _listener = new TcpListener(ListeningIPInterface, ListeningPort);
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        }

        public IPAddress ListeningIPInterface
        {
            get
            {
                IPAddress addr = IPAddress.Loopback;
                if (ConfigurationManager.AppSettings["ListeningIPInterface"] != null)
                    IPAddress.TryParse(ConfigurationManager.AppSettings["ListeningIPInterface"], out addr);

                return addr;
            }
        }

        public Int32 ListeningPort
        {
            get
            {
                Int32 port = 8081;
                if (ConfigurationManager.AppSettings["ListeningPort"] != null)
                    Int32.TryParse(ConfigurationManager.AppSettings["ListeningPort"], out port);

                return port;
            }
        }

        public Boolean DumpHeaders { get; set; }
        public Boolean DumpPostData { get; set; }
        public Boolean DumpResponseData { get; set; }

        public static ProxyServer Server
        {
            get { return _server; }
        }

        public bool Start()
        {
            log.Info("Starting Proxy Service.");
            try
                {
                InstallRootCert();
                _listener.Start();
                log.Info("Proxy Service Started.");
                }
            catch (Exception ex)
                {
                log.Error("Proxy Service Failed to start.", ex);
                return false;
                }

            _listenerThread = new Thread(Listen);
            _cacheMaintenanceThread = new Thread(() => ProxyCache.CacheMaintenance(log));
            _listenerThread.Start(_listener);
            _cacheMaintenanceThread.Start();

            return true;
        }

        public void Stop()
        {
            _listener.Stop();

            //wait for server to finish processing current connections...

            _listenerThread.Abort();
            _cacheMaintenanceThread.Abort();
            _listenerThread.Join();
            _listenerThread.Join();
            UninstallCertificates();
            log.Info("ProxyServer stopped.");
        }

        private static void Listen(Object obj)
        {
            TcpListener listener = (TcpListener) obj;
            try
                {
                while (true)
                    {
                    TcpClient client = listener.AcceptTcpClient();
                    while (!ThreadPool.QueueUserWorkItem(new WaitCallback(ProcessClient), client))
                        ;
                    }
                }
            catch (ThreadAbortException)
                {
                }
            catch (SocketException)
                {
                }
        }

        private static void ProcessClient(Object obj)
        {
            TcpClient client = (TcpClient) obj;
            try
                {
                DoHttpProcessing(client);
                }
            catch (Exception ex)
                {
                log.Error("ProcessClient Exception", ex);
                }
            finally
                {
                client.Close();
                }
        }

        private static void DoHttpProcessing(TcpClient client)
        {
            Stream clientStream = client.GetStream();
            Stream outStream = clientStream; //use this stream for writing out - may change if we use ssl
            SslStream sslStream = null;
            StreamReader clientStreamReader = new StreamReader(clientStream);
            CacheEntry cacheEntry = null;
            MemoryStream cacheStream = null;

            if (Server.DumpHeaders || Server.DumpPostData || Server.DumpResponseData)
                {
                //make sure that things print out in order - NOTE: this is bad for performance
                Monitor.TryEnter(_outputLockObj, TimeSpan.FromMilliseconds(-1.0));
                }

            try
                {
                //read the first line HTTP command
                String httpCmd = clientStreamReader.ReadLine();
                if (String.IsNullOrEmpty(httpCmd))
                    {
                    clientStreamReader.Close();
                    clientStream.Close();
                    return;
                    }
                //break up the line into three components
                String[] splitBuffer = httpCmd.Split(spaceSplit, 3);

                String method = splitBuffer[0];
                String remoteUri = splitBuffer[1];
                Version version = new Version(1, 0);

                HttpWebRequest webReq;
                HttpWebResponse response = null;

                if (splitBuffer[0].ToUpper() == "CONNECT")
                    {
                    //Browser wants to create a secure tunnel
                    //instead = we are going to perform a man in the middle "attack"
                    //the user's browser should warn them of the certification errors however.
                    //Please note: THIS IS ONLY FOR TESTING PURPOSES - you are responsible for the use of this code
                    remoteUri = "https://" + splitBuffer[1];
                    while (!String.IsNullOrEmpty(clientStreamReader.ReadLine()))
                        ;
                    StreamWriter connectStreamWriter = new StreamWriter(clientStream);
                    connectStreamWriter.WriteLine("HTTP/1.0 200 Connection established");
                    connectStreamWriter.WriteLine(String.Format("Timestamp: {0}", DateTime.Now.ToString()));
                    connectStreamWriter.WriteLine("Proxy-agent: matt-dot-net");
                    connectStreamWriter.WriteLine();
                    connectStreamWriter.Flush();

                    sslStream = new SslStream(clientStream, false);
                    try
                        {
                        string tmp = remoteUri.Substring(8);
                        string domain = tmp.Substring(0, tmp.IndexOf(":"));
                        X509Certificate2 _certificate = GetCertificate(domain);
                        sslStream.AuthenticateAsServer(_certificate, false, SslProtocols.Tls | SslProtocols.Ssl3 | SslProtocols.Ssl2, true);
                        }
                    catch (Exception err)
                        {
                        log.Debug("DoHttpProcessing", err);
                        sslStream.Close();
                        clientStreamReader.Close();
                        connectStreamWriter.Close();
                        clientStream.Close();
                        return;
                        }

                    //HTTPS server created - we can now decrypt the client's traffic
                    clientStream = sslStream;
                    clientStreamReader = new StreamReader(sslStream);
                    outStream = sslStream;
                    //read the new http command.
                    httpCmd = clientStreamReader.ReadLine();
                    if (String.IsNullOrEmpty(httpCmd))
                        {
                        clientStreamReader.Close();
                        clientStream.Close();
                        sslStream.Close();
                        return;
                        }
                    splitBuffer = httpCmd.Split(spaceSplit, 3);
                    method = splitBuffer[0];
                    remoteUri = remoteUri + splitBuffer[1];
                    }

                //construct the web request that we are going to issue on behalf of the client.
                webReq = (HttpWebRequest) HttpWebRequest.Create(remoteUri);
                webReq.Method = method;
                webReq.ProtocolVersion = version;

                //read the request headers from the client and copy them to our request
                int contentLen = ReadRequestHeaders(clientStreamReader, webReq);

                webReq.Proxy = null;
                webReq.KeepAlive = false;
                webReq.AllowAutoRedirect = false;
                webReq.AutomaticDecompression = DecompressionMethods.None;

                string requestURL = webReq.RequestUri.ToString();
                if (requestURL.Contains("&x123Referer="))
                    {
                    string refurl = requestURL.Substring(requestURL.IndexOf("&x123Referer=", StringComparison.Ordinal) + "&x123Referer=".Length);
                    if (refurl.Contains("&"))
                        refurl = refurl.Substring(0, refurl.IndexOf("&", StringComparison.Ordinal));
                    webReq.Referer = refurl;
                    }
                if (Server.DumpHeaders)
                    {
                    log.Info(String.Format("{0} {1} HTTP/{2}", webReq.Method, webReq.RequestUri.AbsoluteUri, webReq.ProtocolVersion));
                    DumpHeaderCollectionToConsole(webReq.Headers);
                    }

                //using the completed request, check our cache
                if (method.ToUpper() == "GET")
                    cacheEntry = ProxyCache.GetData(webReq);
                else if (method.ToUpper() == "POST")
                    {
                    char[] postBuffer = new char[contentLen];
                    int bytesRead;
                    int totalBytesRead = 0;
                    StreamWriter sw = new StreamWriter(webReq.GetRequestStream());
                    while (totalBytesRead < contentLen && (bytesRead = clientStreamReader.ReadBlock(postBuffer, 0, contentLen)) > 0)
                        {
                        totalBytesRead += bytesRead;
                        sw.Write(postBuffer, 0, bytesRead);
                        if (Server.DumpPostData)
                            log.Info(new string(postBuffer).Substring(0, bytesRead));
                        }
                    sw.Close();
                    }

                if (cacheEntry == null)
                    {
                    log.Info(String.Format("ThreadID: {2} Requesting {0} on behalf of client {1}", webReq.RequestUri, client.Client.RemoteEndPoint.ToString(), Thread.CurrentThread.ManagedThreadId));
                    webReq.Timeout = int.MaxValue;
                    try
                        {
                        response = (HttpWebResponse) webReq.GetResponse();
                        }
                    catch (WebException webEx)
                        {
                        response = webEx.Response as HttpWebResponse;
                        }
                    if (response != null)
                        {
                        List<Tuple<String, String>> responseHeaders = ProcessResponse(response);
                        StreamWriter myResponseWriter = new StreamWriter(outStream);
                        Stream responseStream = response.GetResponseStream();
                        try
                            {
                            //send the response status and response headers
                            WriteResponseStatus(response.StatusCode, response.StatusDescription, myResponseWriter);
                            WriteResponseHeaders(myResponseWriter, responseHeaders);

                            DateTime? expires = null;
                            CacheEntry entry = null;
                            Boolean canCache = (sslStream == null && ProxyCache.CanCache(response.Headers, ref expires));
                            if (canCache)
                                {
                                entry = ProxyCache.MakeEntry(webReq, response, responseHeaders, expires);
                                if (response.ContentLength > 0)
                                    cacheStream = new MemoryStream(entry.ResponseBytes);
                                }

                            Byte[] buffer;
                            if (response.ContentLength > 0)
                                buffer = new Byte[response.ContentLength];
                            else
                                buffer = new Byte[BUFFER_SIZE];

                            int bytesRead;

                            while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                if (cacheStream != null)
                                    cacheStream.Write(buffer, 0, bytesRead);
                                outStream.Write(buffer, 0, bytesRead);
                                if (Server.DumpResponseData)
                                    log.Info(UTF8Encoding.UTF8.GetString(buffer, 0, bytesRead));
                                }
                            responseStream.Close();
                            if (cacheStream != null)
                                {
                                cacheStream.Flush();
                                cacheStream.Close();
                                }

                            outStream.Flush();
                            if (canCache)
                                ProxyCache.AddData(entry);
                            }
                        catch (Exception ex)
                            {
                            log.Error("DoHttpProcessing", ex);
                            }
                        finally
                            {
                            responseStream.Close();
                            response.Close();
                            myResponseWriter.Close();
                            }
                        }
                    }
                else
                    {
                    //serve from cache
                    StreamWriter myResponseWriter = new StreamWriter(outStream);
                    try
                        {
                        WriteResponseStatus(cacheEntry.StatusCode, cacheEntry.StatusDescription, myResponseWriter);
                        WriteResponseHeaders(myResponseWriter, cacheEntry.Headers);
                        if (cacheEntry.ResponseBytes != null)
                            {
                            outStream.Write(cacheEntry.ResponseBytes, 0, cacheEntry.ResponseBytes.Length);
                            if (Server.DumpResponseData)
                                log.Info(UTF8Encoding.UTF8.GetString(cacheEntry.ResponseBytes));
                            }
                        myResponseWriter.Close();
                        }
                    catch (Exception ex)
                        {
                        log.Error("DoHttpProcessing", ex);
                        }
                    finally
                        {
                        myResponseWriter.Close();
                        }
                    }
                }
            catch (Exception ex)
                {
                log.Error("DoHttpProcessing", ex);
                }
            finally
                {
                if (Server.DumpHeaders || Server.DumpPostData || Server.DumpResponseData)
                    {
                    //release the lock
                    Monitor.Exit(_outputLockObj);
                    }

                clientStreamReader.Close();
                clientStream.Close();
                if (sslStream != null)
                    sslStream.Close();
                outStream.Close();
                if (cacheStream != null)
                    cacheStream.Close();
                }
        }

        private static List<Tuple<String, String>> ProcessResponse(HttpWebResponse response)
        {
            String value = null;
            String header = null;
            List<Tuple<String, String>> returnHeaders = new List<Tuple<String, String>>();
            foreach (String s in response.Headers.Keys)
                {
                if (s.ToLower() == "set-cookie")
                    {
                    header = s;
                    value = response.Headers[s];
                    }
                else
                    returnHeaders.Add(new Tuple<String, String>(s, response.Headers[s]));
                }

            if (!String.IsNullOrWhiteSpace(value))
                {
                response.Headers.Remove(header);
                String[] cookies = cookieSplitRegEx.Split(value);
                foreach (String cookie in cookies)
                    returnHeaders.Add(new Tuple<String, String>("Set-Cookie", cookie));
                }
            returnHeaders.Add(new Tuple<String, String>("X-Proxied-By", "matt-dot-net proxy"));
            return returnHeaders;
        }

        private static void WriteResponseStatus(HttpStatusCode code, String description, StreamWriter myResponseWriter)
        {
            String s = String.Format("HTTP/1.0 {0} {1}", (Int32) code, description);
            myResponseWriter.WriteLine(s);
            if (Server.DumpHeaders)
                log.Info(s);
        }

        private static void WriteResponseHeaders(StreamWriter myResponseWriter, List<Tuple<String, String>> headers)
        {
            if (headers != null)
                {
                foreach (Tuple<String, String> header in headers)
                    myResponseWriter.WriteLine(String.Format("{0}: {1}", header.Item1, header.Item2));
                }
            myResponseWriter.WriteLine();
            myResponseWriter.Flush();

            if (Server.DumpHeaders)
                DumpHeaderCollectionToConsole(headers);
        }

        private static void DumpHeaderCollectionToConsole(WebHeaderCollection headers)
        {
            foreach (String s in headers.AllKeys)
                log.Info(String.Format("{0}: {1}", s, headers[s]));
            log.Info("");
        }

        private static void DumpHeaderCollectionToConsole(List<Tuple<String, String>> headers)
        {
            foreach (Tuple<String, String> header in headers)
                log.Info(String.Format("{0}: {1}", header.Item1, header.Item2));
        }

        private static int ReadRequestHeaders(StreamReader sr, HttpWebRequest webReq)
        {
            String httpCmd;
            int contentLen = 0;
            do
                {
                httpCmd = sr.ReadLine();
                if (String.IsNullOrEmpty(httpCmd))
                    return contentLen;
                String[] header = httpCmd.Split(colonSpaceSplit, 2, StringSplitOptions.None);
                switch (header[0].ToLower())
                    {
                        case "host":
                            webReq.Host = header[1];
                            break;
                        case "user-agent":
                            webReq.UserAgent = header[1];
                            break;
                        case "accept":
                            webReq.Accept = header[1];
                            break;
                        case "referer":
                            webReq.Referer = header[1];
                            break;
                        case "cookie":
                            webReq.Headers["Cookie"] = header[1];
                            break;
                        case "proxy-connection":
                        case "connection":
                        case "keep-alive":
                            //ignore these
                            break;
                        case "content-length":
                            int.TryParse(header[1], out contentLen);
                            break;
                        case "content-type":
                            webReq.ContentType = header[1];
                            break;
                        case "if-modified-since":
                            String[] sb = header[1].Trim().Split(semiSplit);
                            DateTime d;
                            if (DateTime.TryParse(sb[0], out d))
                                webReq.IfModifiedSince = d;
                            break;
                        default:
                            try
                                {
                                webReq.Headers.Add(header[0], header[1]);
                                }
                            catch (Exception ex)
                                {
                                log.Error("ReadRequestHeaders - " + String.Format("Could not add header {0}.  Exception message:{1}", header[0], ex.Message), ex);
                                }
                            break;
                    }
                }
            while (!String.IsNullOrWhiteSpace(httpCmd));
            return contentLen;
        }

        private static X509Certificate2 GetCertificate(string domain)
        {
            lock (locker)
                {
                X509Store store = new X509Store(StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certificates = store.Certificates;
                X509Certificate2 rval = certificates.Cast<X509Certificate2>().FirstOrDefault(certificate => certificate.SubjectName.Name.ToLower().Contains("cn=" + domain.ToLower()));
                store.Close();

                if (rval == null)
                    {
                    Process myProcess = new Process {StartInfo = {FileName = @"makecert.exe", Arguments = "-pe -ss my -n \"CN=" + domain + ", O=myCert, OU=Created by me\" -sky exchange -in MyCustomRoot -is my -eku 1.3.6.1.5.5.7.3.1 -cy end -a sha1 -m 132 -b 10/08/2011 " + _certFolder + "\\\\site-" + domain + ".cer"},};
                    myProcess.StartInfo.RedirectStandardOutput = true;
                    myProcess.StartInfo.UseShellExecute = false;
                    myProcess.StartInfo.CreateNoWindow = true;
                    myProcess.Start();
                    myProcess.WaitForExit();
                    //Succeeded
                    if (myProcess.StandardOutput.ReadToEnd().StartsWith("Succeeded"))
                        rval = new X509Certificate2(Path.Combine(Directory.GetCurrentDirectory(), Path.Combine(_certFullFolderPath, "site-" + domain + ".cer")));
                    else
                        log.Error("Unable to find Certificate for " + Path.Combine(_certFullFolderPath, "site-" + domain + ".cer"));
                    }
                return rval;
                }
        }

        private static void UninstallCertificates()
        {
            X509Store store = new X509Store(StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            X509Certificate2Collection certificates = store.Certificates;
            List<X509Certificate2> toRemove = certificates.Cast<X509Certificate2>().Where(certificate => certificate.Issuer == "CN=MyCustomRoot, O=myCert, OU=Created by me").ToList();
            foreach (X509Certificate2 cert in toRemove)
                store.Remove(cert);
            store.Close();

            store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            certificates = store.Certificates;
            toRemove = certificates.Cast<X509Certificate2>().Where(certificate => certificate.Issuer == "CN=MyCustomRoot, O=myCert, OU=Created by me").ToList();
            foreach (X509Certificate2 cert in toRemove)
                store.Remove(cert);
            store.Close();

            _certFullFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "TemporaryCertificates");
            if (Directory.Exists(_certFullFolderPath))
                Directory.Delete(_certFullFolderPath, true);
        }

        private static void InstallRootCert()
        {
            _certFullFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "TemporaryCertificates");
            if (!Directory.Exists(_certFullFolderPath))
                Directory.CreateDirectory(_certFullFolderPath);

            string fileName = "sig.cer";

            Process myProcess = new Process {StartInfo = {FileName = @"makecert.exe", Arguments = "-r -ss my -n \"CN=MyCustomRoot, O=myCert, OU=Created by me\" -sky signature -eku 1.3.6.1.5.5.7.3.1 -h 1 -cy authority -a sha1 -m 120 -b 09/05/2011 " + _certFolder + "\\\\" + fileName},};
            myProcess.StartInfo.RedirectStandardOutput = true;
            myProcess.StartInfo.UseShellExecute = false;
            myProcess.StartInfo.CreateNoWindow = true;
            myProcess.Start();
            myProcess.WaitForExit();
            if (!myProcess.StandardOutput.ReadToEnd().StartsWith("Succeeded"))
                log.Error("Failed to create certificate for " + _certFolder + "\\\\" + fileName);

            X509Certificate2 cert = new X509Certificate2(Path.Combine(_certFullFolderPath, fileName));

            X509Store store = new X509Store(StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            try
                {
                X509ContentType contentType = X509Certificate2.GetCertContentType(Path.Combine(_certFullFolderPath, fileName));
                byte[] pfx = cert.Export(contentType);
                cert = new X509Certificate2(pfx, (string) null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet);
                store.Add(cert);
                }
            finally
                {
                store.Close();
                }

            store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            try
                {
                X509ContentType contentType = X509Certificate2.GetCertContentType(Path.Combine(_certFullFolderPath, fileName));
                byte[] pfx = cert.Export(contentType);
                cert = new X509Certificate2(pfx, (string) null, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.MachineKeySet);
                store.Add(cert);
                }
            finally
                {
                store.Close();
                }
        }
    }
}