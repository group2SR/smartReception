using System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace smartReception
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Suppress transition for a "snappy" professional feel
            Frame.Navigate(typeof(reception), null, new SuppressNavigationTransitionInfo());
        }

        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // 1. Change cursor to Hand (Fixes the need for the XAML Cursor attribute)
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Hand, 0);

            // 2. Visual feedback: Subtle Opacity and Background shift
            if (sender is Grid card)
            {
                card.Opacity = 0.85;
                card.Background = new SolidColorBrush(Windows.UI.Colors.WhiteSmoke);
            }
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // 1. Reset cursor to Arrow
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);

            // 2. Reset visual feedback
            if (sender is Grid card)
            {
                card.Opacity = 1.0;
                card.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 244, 246));
            }
        }
    }
}