using System;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        string basePath = @"c:\Users\bridg\source\repos\group2SR\smartReception\smartReception\";
        string[] files = { "Access_Control.xaml.cs", "Reports.xaml.cs", "SystemLogs.xaml.cs", "UsersReceptionist.xaml.cs", "Settings.xaml.cs" };

        string replacement = @"private void NavDashboard_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
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
        }";

        foreach (string file in files)
        {
            string path = Path.Combine(basePath, file);
            if (!File.Exists(path)) continue;
            
            string content = File.ReadAllText(path);
            content = Regex.Replace(content, @"(?s)private void NavDashboard_Click.*NavLogout_Click[^{]*{[^}]*}", replacement);
            
            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        }

        Console.WriteLine("Codebehinds Fixed!");
    }
}
