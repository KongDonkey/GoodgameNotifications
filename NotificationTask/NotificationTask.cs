using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using GoodGameUtils;
using System.Xml.Serialization;
using System.IO;
using System.Diagnostics;

namespace NotificationTask
{


    public sealed class NotificationTask : IBackgroundTask
    {

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            try {
                //taskInstance.TriggerDetails;
                string login = (string)localSettings.Values["login"];
                string password = (string)localSettings.Values["password"];
                DateTime date = new DateTime();
                bool date_is_ok = DateTime.TryParse((string)localSettings.Values["date"], out date);
                if (!date_is_ok)
                    goto channels_expired;
                if ((date - DateTime.Now).TotalHours > 2)
                    goto channels_expired;

                channels = (List<FavoriteChannel>)DeserializeObject<List<FavoriteChannel>>((string)localSettings.Values["channels"], typeof(List<FavoriteChannel>));

            channels_expired:
                var deferral = taskInstance.GetDeferral();

                try
                {
                    gg = new Goodgame(login, password);
                    await gg.Connect();
                    Goodgame.DisplayNotification(await UpdateChannels());
                }
                catch (InvalidCredentialsException e)
                {
                    Debug.WriteLine("Login or password is empty string", e.Message);
                }
                catch (WrongCredentialsException e)
                {
                    Debug.WriteLine("Server didnt accept your login/password", e.Message);
                }
                catch (NullReferenceException e)
                {
                    Debug.WriteLine("Null reference exception", e.Message);
                }
                catch (NetworkException e)
                {
                    Debug.WriteLine("Something wrong with network", e.Message);
                }


                deferral.Complete();


            }
            catch (Exception e)
            {
                Debug.WriteLine("Unknown exception", e.Message);
            }
        }

        private async Task<List<FavoriteChannel>> UpdateChannels()
        {
            var result = new List<FavoriteChannel>();
            List<FavoriteChannel> new_channels;
            try
            {
                new_channels = await gg.GetFavoriteChannels();
                
                foreach (var ch in new_channels)
                {
                    if (!channels.Contains(ch))
                    {
                        result.Add(ch);
                    }
                }
            }
            catch (Exception)
            {
                //Just ignore it
                return null;
            }

            channels = new_channels;
            localSettings.Values["channels"] = SerializeObject(channels);
            localSettings.Values["date"] = DateTime.Now.ToString();
            return result;
        }

        static string SerializeObject<T>(T toSerialize)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(toSerialize.GetType());

            using (StringWriter textWriter = new StringWriter())
            {
                xmlSerializer.Serialize(textWriter, toSerialize);
                return textWriter.ToString();
            }
        }

        static object DeserializeObject<T>(string toDeserialize, Type t)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(t);
            object result;

            using (StringReader text = new StringReader(toDeserialize))
            {
                
                result = xmlSerializer.Deserialize(text);
            }

            return result;
        }

        Goodgame gg;
        List<FavoriteChannel> channels = new List<FavoriteChannel>();
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
    }
}
