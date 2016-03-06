using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Net;
using System.Text;
using Windows.Web.Http;
using System.Net.Http.Headers;
using Windows.Web.Http.Headers;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Security.Credentials;
using Windows.Storage;
using Windows.ApplicationModel.Background;
using Windows.UI.Popups;
using System.Text.RegularExpressions;
using Windows.UI.Notifications;
using HtmlAgilityPack;
using NotificationsExtensions.ToastContent;
using GoodGameUtils;

// Шаблон элемента пустой страницы задокументирован по адресу http://go.microsoft.com/fwlink/?LinkId=234238

namespace App1
{
    /// <summary>
    /// Пустая страница, которую можно использовать саму по себе или для перехода внутри фрейма.
    /// </summary>
    public sealed partial class LoginPage : Page
    {

        private static string _taskName = "NotificationTask"; 
        private static string _taskEntryPoint = "NotificationTask.NotificationTask";

        public LoginPage()
        {
            this.InitializeComponent();
        }


        public static BackgroundTaskRegistration RegisterBackgroundTask()
        {
            //
            // Check for existing registrations of this background task.
            //

            foreach (var cur in BackgroundTaskRegistration.AllTasks)
            {

                if (cur.Value.Name == _taskName)
                {
                    cur.Value.Unregister(true);
                }
            }


            //
            // Register the background task.
            //

            var builder = new BackgroundTaskBuilder();

            builder.Name = _taskName;
            builder.TaskEntryPoint = _taskEntryPoint;
            builder.SetTrigger(new SystemTrigger(SystemTriggerType.InternetAvailable, false));

            BackgroundTaskRegistration task = builder.Register();
            //task.Completed += new BackgroundTaskCompletedEventHandler(OnCompleted);
       

            return task;
        }

        /*private static void OnCompleted(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args)
        {
            throw new NotImplementedException();
        }*/

        private async void LoginButton_clicked(object sender, RoutedEventArgs e)
        {
            var login = loginBox.Text;
            var pass = passBox.Password;

            if (login.Length > 0 && pass.Length > 0)
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["login"] = login;
                localSettings.Values["password"] = pass;
            }
            else
            {
                MessageDialog dialog = new MessageDialog("Login and password should not be empty");
                await dialog.ShowAsync();

                return;
            }


            ConnectToGG();

            RegisterBackgroundTask();
            RequestLockScreenAccess();

            this.Frame.Navigate(typeof(SettingsPage));
        }

        public async void RequestLockScreenAccess()
        {
            var status = BackgroundExecutionManager.GetAccessStatus();
            if (status == BackgroundAccessStatus.Unspecified || status == BackgroundAccessStatus.Denied)
                status = await BackgroundExecutionManager.RequestAccessAsync();

            MessageDialog md = new MessageDialog("");

            switch (status)
            {
                case BackgroundAccessStatus.AllowedWithAlwaysOnRealTimeConnectivity:
                    md.Content = "This app is on the lock screen and has access to Always-On Real Time Connectivity.";
                    break;
                case BackgroundAccessStatus.AllowedMayUseActiveRealTimeConnectivity:
                    md.Content = "This app is on the lock screen and has access to Active Real Time Connectivity.";
                    break;
                case BackgroundAccessStatus.Denied:
                    md.Content = "This app is not on the lock screen.";
                    break;
                case BackgroundAccessStatus.Unspecified:
                    md.Content = "The user has not yet taken any action. This is the default setting and the app is not on the lock screen.";
                    break;
            }

            md.ShowAsync();
        }

        private async void ConnectToGG()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            string login = (string)localSettings.Values["login"];
            string password = (string)localSettings.Values["password"];
            Goodgame gg = new Goodgame(login, password);
            
            try {
                await gg.Connect();
            }
            catch (Exception e)
            {
                throw;
            }

            List<FavoriteChannel> channels;
            try {
                channels = await gg.GetFavoriteChannels();
            }
            catch (Exception e)
            {
                //Just ignore it
                return;
            }

            Goodgame.DisplayNotification(channels);
            
        }

        private async void Old()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            string loginUrl = "http://goodgame.ru/ajax/login/";
            string subscriptionUrl = "http://goodgame.ru/ajax/channel/subscriptions/";
            string formUrl = "http://goodgame.ru";
            string login = (string)localSettings.Values["login"];
            string password = (string)localSettings.Values["password"];
            string cookieHeader;

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, new Uri(loginUrl));
            HttpFormUrlEncodedContent content = new HttpFormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("return", "user"),
                new KeyValuePair<string, string>("nickname", login),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("remember", "1")
            });

            request.Content = content;

            HttpClient client = new HttpClient();

            var buffer = Windows.Security.Cryptography.CryptographicBuffer.ConvertStringToBinary(login + ":" + password, Windows.Security.Cryptography.BinaryStringEncoding.Utf16LE);
            string base64token = Windows.Security.Cryptography.CryptographicBuffer.EncodeToBase64String(buffer);
            request.Headers.Authorization = new HttpCredentialsHeaderValue("Basic", base64token);
            request.Content = content;

            HttpResponseMessage response;

            try
            {
                response = await client.SendRequestAsync(request, HttpCompletionOption.ResponseHeadersRead);
            }
            catch (Exception e)
            {
                return;
            }

            cookieHeader = response.Headers["Set-cookie"];

            /*
                Cookies are set now
            */

            request = new HttpRequestMessage(HttpMethod.Get, new Uri(formUrl));
            response = await client.SendRequestAsync(request, HttpCompletionOption.ResponseHeadersRead);

            request = new HttpRequestMessage(HttpMethod.Get, new Uri(subscriptionUrl));
            response = await client.SendRequestAsync(request);

            string subscriptionPage = await response.Content.ReadAsStringAsync();

            HtmlWeb hw = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(subscriptionPage);
            var linkTags = doc.DocumentNode.Descendants("link");
            var linkedPages = doc.DocumentNode.Descendants("a")
                                              .Select(a => a.GetAttributeValue("href", null))
                                              .Where(u => !String.IsNullOrEmpty(u)).ToList();

            linkedPages = linkedPages.Distinct().ToList();

            linkedPages.Remove("\\\"http:\\/\\/goodgame.ru\\/channels\\/favorites\\/\\\"");
            List<string> streamers = new List<string>();

            foreach (var link in linkedPages)
            {
                var parts = link.Split("\\/".ToCharArray());
                streamers.Add(parts[9]);
            }

            //DisplayNotification(streamers);
            //}

            //
            //MessageDialog md = new MessageDialog(hrefs);
        }


        private void Ws_MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                using (DataReader reader = args.GetDataReader())
                {
                    reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                    string readws = reader.ReadString(reader.UnconsumedBufferLength);
                }
            }
            catch {
            }
        }
    }

}
