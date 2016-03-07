using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Storage;
using Windows.ApplicationModel.Background;
using Windows.UI.Popups;
using GoodGameUtils;
using System.Diagnostics;

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
            var trigger = new TimeTrigger(15, false);
            builder.Name = _taskName;
            builder.TaskEntryPoint = _taskEntryPoint;
            builder.SetTrigger(trigger);

            BackgroundTaskRegistration task = builder.Register();



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

            try {
                ConnectToGG();

                RegisterBackgroundTask();
                RequestLockScreenAccess();
            }
            catch (Exception ex)
            {
                MessageDialog md = new MessageDialog("Exception: ", ex.Message);
                await md.ShowAsync();
            }

            this.Frame.Navigate(typeof(SettingsPage));
        }

        public async void RequestLockScreenAccess()
        {
            var status = BackgroundExecutionManager.GetAccessStatus();
            if (status == BackgroundAccessStatus.Unspecified || status == BackgroundAccessStatus.Denied)
                status = await BackgroundExecutionManager.RequestAccessAsync();

            switch (status)
            {
                case BackgroundAccessStatus.AllowedWithAlwaysOnRealTimeConnectivity:
                    Debug.WriteLine("This app is on the lock screen and has access to Always-On Real Time Connectivity.");
                    break;
                case BackgroundAccessStatus.AllowedMayUseActiveRealTimeConnectivity:
                    Debug.WriteLine("This app is on the lock screen and has access to Active Real Time Connectivity.");
                    break;
                case BackgroundAccessStatus.Denied:
                    Debug.WriteLine("This app is not on the lock screen.");
                    break;
                case BackgroundAccessStatus.Unspecified:
                    Debug.WriteLine("The user has not yet taken any action. This is the default setting and the app is not on the lock screen.");
                    break;
            }
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
            catch (WrongCredentialsException e)
            {
                MessageDialog md = new MessageDialog("Login or password is wrong, try again", e.Message);
                await md.ShowAsync();
                return;
            }
            catch (Exception e)
            {
                MessageDialog md = new MessageDialog("Exception: ", e.Message);
                await md.ShowAsync();
                //continue;
            }

            List<FavoriteChannel> channels;
            try {
                channels = await gg.GetFavoriteChannels();
            }
            catch (Exception)
            {
                return;
            }

            Goodgame.DisplayNotification(channels);
            
        }

    }
}
