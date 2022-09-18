using Microsoft.Graph.Beta.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Comrades.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ChannelPage : Page
    {
        private ChannelNavigateArgs Parameters { get; set; }

        public ChannelPage()
        {
            this.InitializeComponent();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Parameters = e.Parameter as ChannelNavigateArgs;
        }

        private void nvSample_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args != null && args.SelectedItemContainer.Tag.ToString() == "messages")
            {
                contentFrame.Navigate(typeof(ChannelMessages), Parameters);
            }
        }
    }

    public class ChannelNavigateArgs
    {
        public AssociatedTeamInfo Team { get; set; }

        public Channel Channel { get; set; }

        public ChannelNavigateArgs(AssociatedTeamInfo team, Channel channel)
        {
            Team = team;
            Channel = channel;
        }
    }
}
