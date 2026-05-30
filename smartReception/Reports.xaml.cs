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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace smartReception
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Reports : Page
    {
        public Reports()
        {
            this.InitializeComponent();
        }

        private void SystemsLogsbtn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SystemLogs));
        }

        private void reportsBacktbtn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(entry));
        }

        private void NavDashboard_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (this.GetType() == typeof(dashboard)) return;
            Frame.Navigate(typeof(dashboard));
        }

        private void NavAccessControl_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (this.GetType() == typeof(Access_Control)) return;
            Frame.Navigate(typeof(Access_Control));
        }

        private void NavReports_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (this.GetType() == typeof(Reports)) return;
            Frame.Navigate(typeof(Reports));
        }

        private void NavLogs_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (this.GetType() == typeof(SystemLogs)) return;
            Frame.Navigate(typeof(SystemLogs));
        }

        private void NavReceptionists_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (this.GetType() == typeof(UsersReceptionist)) return;
            Frame.Navigate(typeof(UsersReceptionist));
        }

        private void NavSettings_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (this.GetType() == typeof(Settings)) return;
            Frame.Navigate(typeof(Settings));
        }

        private void NavLogout_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LogOut));
        }
    }
}
