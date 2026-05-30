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
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace smartReception
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class reception : Page
    {
        public reception()
        {
            this.InitializeComponent();
        }

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage),null, new SuppressNavigationTransitionInfo());
        }

        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).Opacity = 0.7;
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).Opacity = 1;
        }

        private void ReceptionistLogin_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(dashboard));
        }
    }
}
