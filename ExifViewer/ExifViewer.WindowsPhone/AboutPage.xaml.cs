using System.Windows;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;

namespace WindowsPhone
{
    public partial class AboutPage : PhoneApplicationPage
    {
        public AboutPage()
        {
            InitializeComponent();
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            EmailComposeTask task = new EmailComposeTask();
            
            task.Subject = "Regarding Photo Data Viewer";
            task.Body = "Love the app! When are you free for dinner?";
            task.To = "joshuakgoldberg@outlook.com";

            task.Show();
        }
    }
}