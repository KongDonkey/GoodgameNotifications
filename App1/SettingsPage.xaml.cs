using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// Шаблон элемента пустой страницы задокументирован по адресу http://go.microsoft.com/fwlink/?LinkId=234238

namespace App1
{
    /// <summary>
    /// Пустая страница, которую можно использовать саму по себе или для перехода внутри фрейма.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
        }

        private void UnloginButton_clicked(object sender, RoutedEventArgs e)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.DeleteContainer("login");
            localSettings.DeleteContainer("password");

            this.Frame.Navigate(typeof(LoginPage));
        }
    }
}
