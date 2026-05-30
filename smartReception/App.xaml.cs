using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Supabase;
using System.Threading.Tasks;

namespace smartReception
{
    sealed partial class App : Application
    {
        public static Supabase.Client SupabaseClient { get; private set; }
        public const string SupabaseUrl = "https://zwlczsvsixuiycficjjg.supabase.co";
        public const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Inp3bGN6c3ZzaXh1aXljZmljampnIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Nzc5ODUzNjcsImV4cCI6MjA5MzU2MTM2N30.olF3cnXlrR-cCa9i2wUn-_kxkCNX6IAInEjaw0-PN0w";
        private static Task _supabaseInitializationTask;

        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            _supabaseInitializationTask = InitializeSupabaseAsync();
        }

        public static async Task EnsureSupabaseInitializedAsync()
        {
            if (_supabaseInitializationTask == null)
            {
                _supabaseInitializationTask = InitializeSupabaseAsync();
            }

            await _supabaseInitializationTask;
        }

        private static async Task InitializeSupabaseAsync()
        {
            try
            {

                var options = new SupabaseOptions
                {
                    AutoRefreshToken = true,
                    AutoConnectRealtime = true
                };
                SupabaseClient = new Supabase.Client(SupabaseUrl, SupabaseAnonKey, options);
                await SupabaseClient.InitializeAsync();
            }
            catch (Exception)
            {
                SupabaseClient = null;
            }
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    await EnsureSupabaseInitializedAsync();
                    rootFrame.Navigate(typeof(dashboard), e.Arguments);

                }
                Window.Current.Activate();
            }
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}