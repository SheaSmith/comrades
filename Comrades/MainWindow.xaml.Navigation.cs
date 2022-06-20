using Comrades.Services;
using Comrades.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Comrades
{
    public sealed partial class MainWindow : INavigation
    {
        private void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            SetCurrentNavigationViewItem(GetNavigationViewItems(typeof(TeamsPage)).First());
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            SetCurrentNavigationViewItem(args.SelectedItemContainer as NavigationViewItem);
        }

        public List<NavigationViewItem> GetNavigationViewItems()
        {
            List<NavigationViewItem> result = new();
            var items = NavigationViewControl.MenuItems.Select(i => (NavigationViewItem)i).ToList();
            items.AddRange(NavigationViewControl.FooterMenuItems.Select(i => (NavigationViewItem)i));
            result.AddRange(items);

            foreach (NavigationViewItem mainItem in items)
            {
                result.AddRange(mainItem.MenuItems.Select(i => (NavigationViewItem)i));
            }

            return result;
        }

        public List<NavigationViewItem> GetNavigationViewItems(Type type)
        {
            return GetNavigationViewItems().Where(i => i.Tag.ToString() == type.FullName).ToList();
        }

        public List<NavigationViewItem> GetNavigationViewItems(Type type, string title)
        {
            return GetNavigationViewItems(type).Where(ni => ni.Content.ToString() == title).ToList();
        }

        public void SetCurrentNavigationViewItem(NavigationViewItem item)
        {
            if (item == null)
            {
                return;
            }

            if (item.Tag == null)
            {
                return;
            }

            if (NavigationViewControl.SelectedItem == null || !(NavigationViewControl.SelectedItem as NavigationViewItem).Tag.Equals(item.Tag)) {
                rootFrame.Navigate(Type.GetType(item.Tag.ToString()), item.Content);
                NavigationViewControl.Header = item.Content;
                NavigationViewControl.SelectedItem = item;
            }
        }

        public NavigationViewItem GetCurrentNavigationViewItem()
        {
            return NavigationViewControl.SelectedItem as NavigationViewItem;
        }

        public void SetCurrentPage(Type type)
        {
            rootFrame.Navigate(type);
        }
    }
}
