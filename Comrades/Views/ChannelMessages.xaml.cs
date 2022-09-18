using Comrades.MarkupConverter;
using Comrades.ViewModel;
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
using Application = Microsoft.UI.Xaml.Application;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Comrades.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ChannelMessages : Page
    {
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var parameters = e.Parameter as ChannelNavigateArgs;
            DataContext = new ChannelPostsViewModel(parameters.Team.Id, parameters.Channel.Id);
        }

        public ChannelMessages()
        {
            this.InitializeComponent();
        }

        private void RichTextBlock_Loading(FrameworkElement sender, object args)
        {
            
        }

        private void RichTextBlock_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var test = (string)sender.DataContext;
            var quoteBrush = Application.Current.Resources["ControlExampleDisplayBrush"];
            if (test != null)
            {
                ConvertFromHtml.QuoteBrush = (SolidColorBrush)quoteBrush;
                ConvertFromHtml.FromHtml(test, (RichTextBlock)sender, sender.Width);
            }
        }

        private void RichTextBlock_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                var test = (string)((FrameworkElement)sender).DataContext;
                var quoteBrush = Application.Current.Resources["ControlExampleDisplayBrush"];
                if (test != null)
                {
                    ConvertFromHtml.QuoteBrush = (SolidColorBrush)quoteBrush;
                    ConvertFromHtml.FromHtml(test, (RichTextBlock)sender, e.NewSize.Width);
                }
            }
        }

        private void RichTextBlock_LayoutUpdated(object sender, object e)
        {
            if (sender != null)
            {
                var test = (string)((FrameworkElement)sender).DataContext;
                var quoteBrush = Application.Current.Resources["ControlExampleDisplayBrush"];
                if (test != null)
                {
                    ConvertFromHtml.QuoteBrush = (SolidColorBrush)quoteBrush;
                    ConvertFromHtml.FromHtml(test, (RichTextBlock)sender, ((FrameworkElement)sender).Width);
                }
            }
        }
    }
}
