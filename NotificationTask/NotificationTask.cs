using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.Web.Http;
using Windows.Web.Http.Headers;
using GoodGameUtils;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Newtonsoft.Json;

namespace NotificationTask
{
    

    public sealed class NotificationTask : IBackgroundTask
    {

        public bool Connected
        {
            get
            {
                return _connected;
            }
        }

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            string login = (string)localSettings.Values["login"];
            string password = (string)localSettings.Values["password"];
            
            var deferral = taskInstance.GetDeferral();

            if (!_connected)
            {
                try
                {

                    gg = new Goodgame(login, password);

                    try
                    {
                        await gg.Connect();
                    }
                    catch (Exception e)
                    {
                        throw;
                    }

                    List<FavoriteChannel> channels;
                    try
                    {
                        channels = await gg.GetFavoriteChannels();
                    }
                    catch (Exception e)
                    {
                        //Just ignore it
                        return;
                    }

                    _connected = true;
                    Goodgame.DisplayNotification(channels);
                    cookies = gg.Cookies;
                    foreach (HttpCookie cookie in cookies)
                    {
                        if (cookie.Name == "ggtoken")
                        {
                            ggtoken = cookie.Value;
                        }
                        if (cookie.Name == "uid")
                        {
                            uid = Convert.ToInt32(cookie.Value);
                        }
                    }

                    RegisterCCT();

                }
                catch (Exception)
                {

                }
            }

            

            deferral.Complete();
        }

        private async void RegisterCCT()
        {
            try
            {
                channel = new ControlChannelTrigger("channelOne", 30, ControlChannelTriggerResourceType.RequestSoftwareSlot);
            }
            catch (UnauthorizedAccessException e)
            {
                //log
                return;
            }
            catch (Exception e)
            {
                return;
            }

            // Register the apps background task with the trigger for keepalive. 
            var keepAliveBuilder = new BackgroundTaskBuilder();
            keepAliveBuilder.Name = "KeepaliveTaskForChannelOne";
            keepAliveBuilder.TaskEntryPoint = WebSocketKeepAliveTask;
            keepAliveBuilder.SetTrigger(channel.KeepAliveTrigger);
            keepAliveBuilder.Register();

            try
            {
                socket = new MessageWebSocket();
                channel.UsingTransport(socket);
                socket.SetRequestHeader("Cookie", gg.Cookies);
                socket.SetRequestHeader("Origin", "http://goodgame.ru");
                socket.SetRequestHeader("Host", "notify.goodgame.ru:8087");
                socket.MessageReceived += Socket_MessageReceived;
                await socket.ConnectAsync(new Uri("ws://notify.goodgame.ru:8087/"));
                messageWriter = new DataWriter(socket.OutputStream);
            }
            catch
            {

            }

        }

        private void Socket_MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            dynamic message = ReadMessage(args);

            //Subscribe
            if (message.type == "{welcome}")
            {
                //messageWriter.WriteString(JsonConvert.SerializeObject
            }
            else if (message.type == "canSubscribe" && message.data.answer == "{ok}")
            {

            }
        }

        private dynamic ReadMessage(MessageWebSocketMessageReceivedEventArgs args)
        {
            DataReader messageReader = args.GetDataReader();
            messageReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            string messageString = messageReader.ReadString(messageReader.UnconsumedBufferLength);

            dynamic welcome = JsonConvert.DeserializeObject(messageString);
            return welcome;
        }

        ControlChannelTrigger channel;
        const string channelId = "goodgamenotifier";
        const int serverKeepAliveInterval = 2;
        const string WebSocketKeepAliveTask = "Windows.Networking.Sockets.WebSocketKeepAlive";
        bool _connected = false;
        MessageWebSocket socket;
        HttpCookieCollection cookies;
        Goodgame gg;
        private DataWriter messageWriter;
        private string ggtoken;
        private int uid;
    }
}
