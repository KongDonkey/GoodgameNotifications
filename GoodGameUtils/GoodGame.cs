using HtmlAgilityPack;
using NotificationsExtensions.ToastContent;
/*using MetroLog;
using MetroLog.Targets;*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.Web.Http;
using Windows.Web.Http.Headers;

namespace GoodGameUtils
{
    public class Goodgame
    {
        public Goodgame()
        {
            /*var configuration = new LoggingConfiguration();
#if DEBUG
            configuration.AddTarget(LogLevel.Trace, LogLevel.Fatal, new DebugTarget());
#endif
            configuration.AddTarget(LogLevel.Trace, LogLevel.Fatal, new StreamingFileTarget());
            configuration.IsEnabled = true;

            LogManagerFactory.DefaultConfiguration = configuration;

            m_log = LogManagerFactory.DefaultLogManager.GetLogger<Goodgame>();*/

            Goodgame.ClearAllCookies();
        }

        public Goodgame(string login, string password) : this()
        {
            this.Login = login;
            this.Password = password;
        }

        public string Login
        {
            set
            {
                if (value.Length == 0)
                    throw new InvalidCredentialsException("Login is empty");
                else
                    m_login = value;
            }
            get
            {
                return m_login;
            }
        }

        public string Password
        {
            set
            {
                if (value.Length == 0)
                    throw new InvalidCredentialsException("Password is empty");
                else
                    m_password = value;
            }
            get
            {
                return m_password;
            }
        }

        public async Task Connect()
        {
            if (m_login.Length == 0 || m_password.Length == 0)
                throw new InvalidCredentialsException("Connect with empty login or password");

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, new Uri(LOGIN_URL));

            HttpClient client = new HttpClient();
            HttpResponseMessage response;

            request.Content = new HttpFormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("return", "user"),
                new KeyValuePair<string, string>("nickname", m_login),
                new KeyValuePair<string, string>("password", m_password),
                new KeyValuePair<string, string>("remember", "1")
            });

            client.DefaultRequestHeaders.Add("Host", "goodgame.ru");
            client.DefaultRequestHeaders.Add("Origin", "http://goodgame.ru");
            client.DefaultRequestHeaders.Add("Referer", "http://goodgame.ru/");



            //Initial request for cookies
            try
            {
                response = await client.SendRequestAsync(request, HttpCompletionOption.ResponseHeadersRead);

            }
            catch (Exception e)
            {
                Log("No cookies", e);
                throw new NetworkException("Initial put request failed", e);
            }

            var content = await response.Content.ReadAsStringAsync();
            if (content.Contains("=false"))
                throw new WrongCredentialsException(); ;

            //Confirm
            request = new HttpRequestMessage(HttpMethod.Get, new Uri(GG_MAINPAGE_URL));
       
            try
            {
                response = await client.SendRequestAsync(request, HttpCompletionOption.ResponseHeadersRead);
            }
            catch (Exception e)
            {
                Log("Confirmation request failed", e);
                throw new NetworkException("Confirmation request failed", e);
            }

            m_connectionEstablished = true;

        }

        public async Task<List<FavoriteChannel>> GetFavoriteChannels()
        {

            if (!m_connectionEstablished)
                throw new NetworkException("Not connected");

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, new Uri(SUBSCRIPTION_URL));
            HttpResponseMessage response;
            HttpClient client = new HttpClient();

            try
            {
                response = await client.SendRequestAsync(request, HttpCompletionOption.ResponseHeadersRead);
            }
            catch (Exception e)
            {
                Log("SendRequestAsync put request failed", e);
                m_cookies = null;
                throw new NetworkException("SendRequestAsync get request failed", e);
            }

            string subscriptionPage = await response.Content.ReadAsStringAsync();

            HtmlWeb hw = new HtmlWeb();
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(subscriptionPage);
            var linkTags = doc.DocumentNode.Descendants("link");
            var linkedPages = doc.DocumentNode.Descendants("a")
                                              .Select(a => a.GetAttributeValue("href", null))
                                              .Where(u => !String.IsNullOrEmpty(u)).ToList();

            linkedPages = linkedPages.Distinct().ToList();
            linkedPages.Remove("\\\"http:\\/\\/goodgame.ru\\/channels\\/favorites\\/\\\"");
            var linkedPages2 = new List<string>();
            foreach (var link in linkedPages)
            {
                linkedPages2.Add(Regex.Unescape(link).Replace("\"", ""));
            }

            List<FavoriteChannel> favChannels = new List<FavoriteChannel>();

            for (int i = 0; i < linkedPages.Count(); i++)
            {
                var parts = linkedPages2[i].Split("/".ToCharArray());
                favChannels.Add(new FavoriteChannel() { Name = parts[4], Uri = linkedPages2[i] } );
            }

            return favChannels;
        }

        public static void DisplayNotification(List<FavoriteChannel> streams)
        {
            //Nothing to do if no streams online
            if (streams.Count == 0)
                return;

            var toast = ToastContentFactory.CreateToastText02();
            if (streams.Count > 1)
            {
                toast.TextHeading.Text = streams[0].Name + " and other " + (streams.Count - 1) + " streams are currently online!";
                toast.TextBodyWrap.Text = "Click me to watch them!";
                toast.Launch = "toast://many_online";

            }
            else {
                toast.TextHeading.Text = streams[0].Name + " are online!";
                toast.TextBodyWrap.Text = "Click me to watch it!";
                toast.Launch = streams[0].Uri;
            }

            var notification = toast.CreateNotification();
            if (streams.Count > 1)
                notification.Activated += ToastMany_Activated;
            else
                notification.Activated += ToastOne_Activated;
            ToastNotificationManager.CreateToastNotifier().Show(notification);
        }

        public static async void ToastOne_Activated(ToastNotification sender, object args)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri((string)args));
        }

        public static async void ToastMany_Activated(ToastNotification sender, object args)
        {
            string favUrl = @"http://www.goodgame.ru/channels/favorites";
            await Windows.System.Launcher.LaunchUriAsync(new Uri(favUrl));
        }

        private void Log(string message, Exception e = null, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            //m_log.Warn(message, e);
        }

        public static void ClearAllCookies()
        {
            Windows.Web.Http.Filters.HttpBaseProtocolFilter filter = new Windows.Web.Http.Filters.HttpBaseProtocolFilter();
            filter.CacheControl.ReadBehavior = Windows.Web.Http.Filters.HttpCacheReadBehavior.MostRecent;
            filter.CacheControl.WriteBehavior = Windows.Web.Http.Filters.HttpCacheWriteBehavior.NoCache;
            var cookies = filter.CookieManager.GetCookies(new Uri(LOGIN_URL));

            foreach (HttpCookie cookie in cookies)
            {
                filter.CookieManager.DeleteCookie(cookie);
            }
        }

        public HttpCookieCollection Cookies
        {
            get
            {
                Windows.Web.Http.Filters.HttpBaseProtocolFilter filter = new Windows.Web.Http.Filters.HttpBaseProtocolFilter();

                return filter.CookieManager.GetCookies(new Uri(LOGIN_URL));
            }
        }


        private HttpCookieCollection m_cookies;
        private string m_login;
        private string m_password;
        //private ILogger m_log;
        private bool m_connectionEstablished = false;

        private const string LOGIN_URL = "http://goodgame.ru/ajax/login/";
        private const string SUBSCRIPTION_URL = "http://goodgame.ru/ajax/channel/subscriptions/";
        private const string GG_MAINPAGE_URL = "http://goodgame.ru";
    }

    public struct FavoriteChannel
    {
        public string Name;
        public string Uri;
    }

    public class InvalidCredentialsException : Exception
    {
        public InvalidCredentialsException() { }

        public InvalidCredentialsException(string message) : base(message) { }

        public InvalidCredentialsException(string message, Exception inner) : base(message, inner) { }

    }

    public class WrongCredentialsException : Exception
    {
        public WrongCredentialsException() { }

        public WrongCredentialsException(string message) : base(message) { }

        public WrongCredentialsException(string message, Exception inner) : base (message, inner) { }
    }

    public class NetworkException : Exception
    {
        public NetworkException() { }

        public NetworkException(string message) : base(message) { }

        public NetworkException(string message, Exception inner) : base(message, inner) { }

    }
}
