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
            Goodgame.ClearAllCookies();
        }

        public Goodgame(string login, string password) : this()
        {
            m_login = login;
            m_password = password;
            if (m_login == null || m_password == null)
                throw new NullReferenceException("Goodgame.ctor failed: login or password is null");
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
                throw new NetworkException("Initial put request failed", e);
            }

            var content = await response.Content.ReadAsStringAsync();
            if (!content.Contains("true")) //FIXME
                throw new WrongCredentialsException();

            //Confirm
            request = new HttpRequestMessage(HttpMethod.Get, new Uri(GG_MAINPAGE_URL));
       
            try
            {
                response = await client.SendRequestAsync(request, HttpCompletionOption.ResponseHeadersRead);
            }
            catch (Exception e)
            {
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
            if (streams == null || streams.Count == 0)
                return;

            var toast = ToastContentFactory.CreateToastText02();
            if (streams.Count > 1)
            {
                foreach (var stream in streams)
                {
                    toast.TextHeading.Text += (stream.Name + " ");
                }           
                toast.Launch = "http://goodgame.ru/channels/favorites/";

            }
            else {
                toast.TextHeading.Text = streams[0].Name;
                toast.Launch = streams[0].Uri;
            }
            toast.TextBodyWrap.Text = "are now live!";

            var notification = toast.CreateNotification();
            notification.Activated += Toast_Activated;

            ToastNotificationManager.CreateToastNotifier().Show(notification);
        }

        public static async void Toast_Activated(ToastNotification sender, object args)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri((string)args));
        }

        public static void ClearAllCookies()
        {
            Windows.Web.Http.Filters.HttpBaseProtocolFilter filter = new Windows.Web.Http.Filters.HttpBaseProtocolFilter();
            //filter.CacheControl.ReadBehavior = Windows.Web.Http.Filters.HttpCacheReadBehavior.MostRecent;
            //filter.CacheControl.WriteBehavior = Windows.Web.Http.Filters.HttpCacheWriteBehavior.NoCache;
            var cookies = filter.CookieManager.GetCookies(new Uri(LOGIN_URL));

            foreach (HttpCookie cookie in cookies)
            {
                filter.CookieManager.DeleteCookie(cookie);
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
