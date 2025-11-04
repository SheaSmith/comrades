using Comrades.Services;
using Comrades.ViewModel;
using Microsoft.Graph;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.Graph.Beta.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Comrades.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TeamsPage : Page
    {
        public TeamsPage()
        {
            this.InitializeComponent();

            ViewModel.SelectedChannelChanged += new PropertyChangedEventHandler((sender, args) =>
            {
                Channel channel = ViewModel.SelectedChannel;

                if (channel != null)
                {
                    treeView.SelectedItem = channel;
                    navigate(channel);
                }
            });
        }

        private void treeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (args != null && args.InvokedItem is Channel)
            {
                var channel = args.InvokedItem as Channel;
                Windows.Storage.ApplicationData.Current.LocalSettings.Values["selectedChannelId"] = channel.Id;
                navigate(channel);
            }
        }

        private void navigate(Channel channel)
        {
            var team = (treeView.ItemsSource as ObservableCollection<TeamChannelBindings>).FirstOrDefault(t => t.Channels.Any(c => c.Id == channel.Id)).Team;
            contentFrame.Navigate(typeof(ChannelPage), new ChannelNavigateArgs(team, channel));
        }
    }

    class TeamsChannelsSelector : DataTemplateSelector
    {
        public DataTemplate TeamTemplate { get; set; }
        public DataTemplate ChannelTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (item is Channel)
            {
                return ChannelTemplate;
            }

            return TeamTemplate;
        }
    }
}
