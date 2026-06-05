using System;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        string basePath = @"c:\Users\bridg\source\repos\group2SR\smartReception\smartReception\";

        // 1. Fix entry.xaml
        string entryPath = basePath + "entry.xaml";
        string entryContent = File.ReadAllText(entryPath);
        entryContent = Regex.Replace(entryContent, @"(?s)<StackPanel\.Resources>.*?<!-- Footer / Log Out -->", 
@"<StackPanel.Resources>
                    <SolidColorBrush x:Key=""ButtonBackgroundPointerOver"" Color=""#1E293B""/>
                    <SolidColorBrush x:Key=""ButtonForegroundPointerOver"" Color=""#10B981""/>
                    <SolidColorBrush x:Key=""ButtonBackgroundPressed"" Color=""#0F172A""/>
                    <SolidColorBrush x:Key=""ButtonForegroundPressed"" Color=""#10B981""/>
                </StackPanel.Resources>
                <Button Content=""ðŸ“Š  Dashboard"" Click=""NavDashboard_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
                <Button Content=""ðŸ“  Register Clients"" Click=""NavRegister_Click"" Style=""{StaticResource ActiveSidebarButtonStyle}""/>
                <Button Content=""ðŸ“ˆ  General Reports"" Click=""NavReports_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
                <Button Content=""ðŸ“œ  System Logs"" Click=""NavLogs_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
                <Button Content=""ðŸ‘¥  Receptionists"" Click=""NavReceptionists_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
                <Button Content=""âš™ï¸  Settings"" Click=""NavSettings_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
            </StackPanel>

            <!-- Footer / Log Out -->");
        entryContent = Regex.Replace(entryContent, @"(?s)<!-- Footer / Log Out -->.*?</StackPanel>", 
@"<!-- Footer / Log Out -->
            <StackPanel Grid.Row=""2"" Padding=""12,0"" Spacing=""8"">
                <Rectangle Height=""1"" Fill=""#1E293B"" Margin=""0,0,0,12""/>
                <Button Content=""ðŸšª  Sign Out"" Click=""NavLogout_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
            </StackPanel>");
        File.WriteAllText(entryPath, entryContent, System.Text.Encoding.UTF8);


        // 2. Fix dashboard.xaml
        string dashPath = basePath + "dashboard.xaml";
        string dashContent = File.ReadAllText(dashPath);
        dashContent = Regex.Replace(dashContent, @"(?s)<StackPanel\.Resources>.*?<!-- Footer / Log Out -->", 
@"<StackPanel.Resources>
                    <SolidColorBrush x:Key=""ButtonBackgroundPointerOver"" Color=""#1E293B""/>
                    <SolidColorBrush x:Key=""ButtonForegroundPointerOver"" Color=""#10B981""/>
                    <SolidColorBrush x:Key=""ButtonBackgroundPressed"" Color=""#0F172A""/>
                    <SolidColorBrush x:Key=""ButtonForegroundPressed"" Color=""#10B981""/>
                </StackPanel.Resources>
                <Button Content=""ðŸ“Š  Dashboard"" Click=""NavDashboard_Click"" Style=""{StaticResource ActiveSidebarButtonStyle}""/>
                <Button Content=""ðŸ“œ  Access Logs"" Click=""NavAccessControl_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
                <Button Content=""ðŸ“ˆ  General Reports"" Click=""NavReports_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
                <Button Content=""ðŸ“œ  System Logs"" Click=""NavLogs_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
                <Button Content=""ðŸ‘¥  Receptionists"" Click=""NavReceptionists_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
                <Button Content=""âš™ï¸  Settings"" Click=""NavSettings_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
            </StackPanel>

            <!-- Footer / Log Out -->");
        dashContent = Regex.Replace(dashContent, @"(?s)<!-- Footer / Log Out -->.*?</StackPanel>", 
@"<!-- Footer / Log Out -->
            <StackPanel Grid.Row=""2"" Padding=""12,0"" Spacing=""8"">
                <Rectangle Height=""1"" Fill=""#1E293B"" Margin=""0,0,0,12""/>
                <Button Content=""ðŸšª  Sign Out"" Click=""NavLogout_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
            </StackPanel>");
        File.WriteAllText(dashPath, dashContent, System.Text.Encoding.UTF8);

        // 3. Fix MasterDashboard.xaml
        string masterPath = basePath + "MasterDashboard.xaml";
        string masterContent = File.ReadAllText(masterPath);
        masterContent = Regex.Replace(masterContent, @"(?s)<StackPanel\.Resources>.*?<!-- Footer / Log Out -->", 
@"<StackPanel.Resources>
                    <SolidColorBrush x:Key=""ButtonBackgroundPointerOver"" Color=""#1E293B""/>
                    <SolidColorBrush x:Key=""ButtonForegroundPointerOver"" Color=""#10B981""/>
                    <SolidColorBrush x:Key=""ButtonBackgroundPressed"" Color=""#0F172A""/>
                    <SolidColorBrush x:Key=""ButtonForegroundPressed"" Color=""#10B981""/>
                </StackPanel.Resources>
                <Button Content=""ðŸ“Š  Dashboard"" Click=""NavDashboard_Click"" Style=""{StaticResource ActiveSidebarButtonStyle}""/>
                <Button Content=""ðŸ“  Register Clients"" Click=""NavRegister_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
            </StackPanel>

            <!-- Footer / Log Out -->");
        masterContent = Regex.Replace(masterContent, @"(?s)<!-- Footer / Log Out -->.*?</StackPanel>", 
@"<!-- Footer / Log Out -->
            <StackPanel Grid.Row=""2"" Padding=""12,0"" Spacing=""8"">
                <Rectangle Height=""1"" Fill=""#1E293B"" Margin=""0,0,0,12""/>
                <Button Content=""ðŸšª  Sign Out"" Click=""NavLogout_Click"" Style=""{StaticResource SidebarButtonStyle}""/>
            </StackPanel>");
        File.WriteAllText(masterPath, masterContent, System.Text.Encoding.UTF8);

        Console.WriteLine("Done!");
    }
}
