using Comrades.Models;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Comrades.ViewModel
{
    public class ChannelPostsViewModel : ObservableObject
    {
        private Visibility _showLoader;
        public Visibility ShowLoader // using T10 SettingsService
        {
            get => _showLoader;
            set
            {
                SetProperty(ref _showLoader, value);
                OnPropertyChanged(nameof(ShowLoader));
            }
        }

        public IncrementalMessageLoadingCollection Messages {  get; set; }

        public ChannelPostsViewModel(string teamId, string channelId)
        {
            Messages = new IncrementalMessageLoadingCollection(teamId, channelId);
        }
    }
}
