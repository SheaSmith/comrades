using Comrades.MarkupConverter;
using Comrades.Services;
using Microsoft.Graph;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Comrades.MarkupConverter.MarkupConversion;
using Microsoft.Graph.Beta.Models;
using Microsoft.Graph.Beta.Models.ODataErrors;
using static System.Net.Mime.MediaTypeNames;
using Application = Microsoft.UI.Xaml.Application;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Comrades.Controls
{
    public sealed partial class ChannelMessage : UserControl
    {
        public ChannelMessage()
        {
            this.InitializeComponent();
        }

        public double VisibleHeight = 200;
        public Visibility ShowExpand = Visibility.Collapsed;
        public string ShowMoreText = "Show more";

        public ChatMessage ChatMessage;

        public object Message
        {
            get { return GetValue(MessageProperty); }
            set { 
                SetValue(MessageProperty, value);

                var test = (Tuple<ChatMessage, Dictionary<ChatMessageAttachment, string>>)value;
                ChatMessage = test.Item1;

                // var quoteBrush = Application.Current.Resources["ControlExampleDisplayBrush"];
                // ConvertFromHtml3.QuoteBrush = (SolidColorBrush)quoteBrush;
                LastWidth = richText.Width;
                try
                {
                    // ConvertFromHtml3.FromHtml(test.Item1.Body.Content, richText, richText.Width, test.Item2);
                    new HtmlToWinUiConverter().Convert(test.Item1.Body.Content, richText);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }

                GetProfilePicture();
            }
        }
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(object), typeof(ChannelMessage), new PropertyMetadata(null));


        private double LastWidth = double.NaN;

        private void RichTextBlock_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                ConvertFromHtml3.UpdateSize((RichTextBlock)sender, e.NewSize.Width, LastWidth);
                LastWidth = e.NewSize.Width;
            }
        }

        private void RichTextBlock_BringIntoViewRequested(UIElement sender, BringIntoViewRequestedEventArgs args)
        {
            if (sender is FrameworkElement)
            {
                var width = ((FrameworkElement)sender).Width;
                ConvertFromHtml3.UpdateSize((RichTextBlock)sender, width, LastWidth);
                LastWidth = width;
            }
        }

        private void textContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ShowExpand = e.NewSize.Height >= 200 ? Visibility.Visible : Visibility.Collapsed;
            Bindings.Update();
        }

        private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (VisibleHeight == double.PositiveInfinity)
            {
                VisibleHeight = 200;
                ShowMoreText = "Show more";
            }
            else
            {
                VisibleHeight = double.PositiveInfinity;
                ShowMoreText = "Show less";
            }
            Bindings.Update();
        }


        private async void GetProfilePicture()
        {
            var graph = await AuthenticationService.GetGraphService();

            BitmapImage imageSource = new BitmapImage();
            if (ChatMessage.From?.User != null)
            {
                try
                {
                    var photo = await graph.Users[ChatMessage.From.User.Id].Photo.Content.GetAsync();

                    imageSource.SetSource(photo.AsRandomAccessStream());
                }
                catch (ODataError e) { }
            }
            //else if (ChatMessage.From.Application != null)
            //{
            //    var photo = await graph.AppCatalogs.TeamsApps[ChatMessage.From.Application.Id].AppDefinitions
            //}

            ProfilePicture.ProfilePicture = imageSource;

            if (ChatMessage.From?.User != null)
            {
                var availability = await graph.Users[ChatMessage.From.User.Id].Presence.GetAsync();

                if (availability.Availability.StartsWith("Available"))
                {
                    ProfilePicture.BadgeGlyph = "\U0000E73E";
                    if (!ProfilePicture.Resources.ThemeDictionaries.ContainsKey("PersonPictureEllipseBadgeFillThemeBrush"))
                        ProfilePicture.Resources.ThemeDictionaries.Add("PersonPictureEllipseBadgeFillThemeBrush", new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)));
                    else {
                        //var resourceDictionary = new ResourceDictionary();
                        //resourceDictionary["PersonPictureEllipseBadgeFillThemeBrush"] = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));

                        ////ProfilePicture.Resources.ThemeDictionaries.Add("Default", resourceDictionary);
                        //ProfilePicture.Resources.ThemeDictionaries.Add("Light", resourceDictionary);
                        ////ProfilePicture.Resources.ThemeDictionaries.Add("HighContrast", resourceDictionary);
                        ///

                        (ProfilePicture.Resources.ThemeDictionaries["PersonPictureEllipseBadgeFillThemeBrush"] as SolidColorBrush).Color = Windows.UI.Color.FromArgb(255, 255, 0, 0);
                    }
                }
                else if (availability.Availability == "Away" || availability.Availability == "BeRightBack")
                {
                    ProfilePicture.BadgeGlyph = "\U0000E845";
                }
                else if (availability.Availability == "DoNotDisturb")
                {
                    ProfilePicture.BadgeGlyph = "\U0000E921";
                }
                else if (availability.Availability.StartsWith("Busy"))
                {
                    ProfilePicture.BadgeGlyph = " ";
                }
                else if (availability.Availability == "Offline")
                {
                    ProfilePicture.BadgeGlyph = "\U0000EDAE";
                }
                else
                {
                    ProfilePicture.BadgeGlyph = "\U0000E897";
                }
            }
            
        }
    }
}
