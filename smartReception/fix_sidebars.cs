using System;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        string basePath = @"c:\Users\bridg\source\repos\group2SR\smartReception\smartReception\";

        string[] files = { "Access_Control.xaml", "Reports.xaml", "SystemLogs.xaml", "UsersReceptionist.xaml", "Settings.xaml" };

        foreach (string file in files)
        {
            string path = Path.Combine(basePath, file);
            if (!File.Exists(path)) continue;
            string content = File.ReadAllText(path);

            string dashboardStyle = "SidebarButtonStyle";
            string accessLogsStyle = "SidebarButtonStyle";
            string reportsStyle = "SidebarButtonStyle";
            string systemLogsStyle = "SidebarButtonStyle";
            string receptionistsStyle = "SidebarButtonStyle";
            string settingsStyle = "SidebarButtonStyle";

            if (file == "Access_Control.xaml") accessLogsStyle = "ActiveSidebarButtonStyle";
            if (file == "Reports.xaml") reportsStyle = "ActiveSidebarButtonStyle";
            if (file == "SystemLogs.xaml") systemLogsStyle = "ActiveSidebarButtonStyle";
            if (file == "UsersReceptionist.xaml") receptionistsStyle = "ActiveSidebarButtonStyle";
            if (file == "Settings.xaml") settingsStyle = "ActiveSidebarButtonStyle";

            string replacement = String.Format(@"<StackPanel.Resources>
                    <SolidColorBrush x:Key=""ButtonBackgroundPointerOver"" Color=""#1E293B""/>
                    <SolidColorBrush x:Key=""ButtonForegroundPointerOver"" Color=""#10B981""/>
                    <SolidColorBrush x:Key=""ButtonBackgroundPressed"" Color=""#0F172A""/>
                    <SolidColorBrush x:Key=""ButtonForegroundPressed"" Color=""#10B981""/>
                </StackPanel.Resources>
                <Button Content=""📊  Dashboard"" Click=""NavDashboard_Click"" Style=""{{StaticResource {0}}}""/>
                <Button Content=""📜  Access Logs"" Click=""NavAccessControl_Click"" Style=""{{StaticResource {1}}}""/>
                <Button Content=""📈  General Reports"" Click=""NavReports_Click"" Style=""{{StaticResource {2}}}""/>
                <Button Content=""📜  System Logs"" Click=""NavLogs_Click"" Style=""{{StaticResource {3}}}""/>
                <Button Content=""👥  Receptionists"" Click=""NavReceptionists_Click"" Style=""{{StaticResource {4}}}""/>
                <Button Content=""⚙️  Settings"" Click=""NavSettings_Click"" Style=""{{StaticResource {5}}}""/>
            </StackPanel>

            <!-- Footer / Log Out -->", dashboardStyle, accessLogsStyle, reportsStyle, systemLogsStyle, receptionistsStyle, settingsStyle);

            content = Regex.Replace(content, @"(?s)<StackPanel\.Resources>.*?<!-- Footer / Log Out -->", replacement);

            string footerReplacement = @"<!-- Footer / Log Out -->
            <StackPanel Grid.Row=""2"" Padding=""12,0"" Spacing=""8"">
                <Rectangle Height=""1"" Fill=""#1E293B"" Margin=""0,0,0,12""/>
                <Button Content=""🚪  Sign Out"" Click=""NavLogout_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
            </StackPanel>";
            
            content = Regex.Replace(content, @"(?s)<!-- Footer / Log Out -->.*?</StackPanel>", footerReplacement);

            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        }

        Console.WriteLine("Done Fixing Sidebars!");
    }
}
