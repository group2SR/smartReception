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

        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;

            // Initialize Supabase
            InitializeSupabase();
        }

        private async void InitializeSupabase()
        {
            try
            {
                var url = "https://zwlczsvsixuiycficjjg.supabase.co";
                var key = "sb_publishable_vSE-Ym1RaTT5swPDp3EJ4w_meN6HNDf";

                var options = new SupabaseOptions
                {
                    AutoRefreshToken = true,
                    AutoConnectRealtime = true
                };

                SupabaseClient = new Supabase.Client(url, key, options);
                await SupabaseClient.InitializeAsync();
            }
            catch (Exception)
            {
                // Prevents the app from exiting with Code 1 if Supabase fails
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
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
                    // CHANGE THIS LINE to show your new UI
                    rootFrame.Navigate(typeof(entry), e.Arguments);
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