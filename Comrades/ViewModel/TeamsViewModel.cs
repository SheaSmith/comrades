using CommunityToolkit.Mvvm.ComponentModel;
using Comrades.Services;
using Microsoft.Graph;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;
using Microsoft.Graph.Beta.Models;

namespace Comrades.ViewModel
{
    public class TeamsViewModel : ObservableObject
    {
        public ObservableCollection<TeamChannelBindings> teams { get; set; } = new ObservableCollection<TeamChannelBindings>();
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        private Channel _selectedChannel;
        public Channel SelectedChannel
        {
            get => _selectedChannel;
            set
            {
                SetProperty(ref _selectedChannel, value);
                OnPropertyChanged(nameof(CheckSelected));
                SelectedChannelChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedChannel)));
            }
        }

        public event PropertyChangedEventHandler SelectedChannelChanged;

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

        private bool CheckSelected(string channelId)
        {
            return SelectedChannel.Id == channelId;
        }

        public TeamsViewModel()
        {
            ShowLoader = Visibility.Visible;
            Task.Run(async () =>
            {
                var graph = await AuthenticationService.GetGraphService();

                var teams = await graph.Me.Teamwork.AssociatedTeams.GetAsync();

                foreach (var team in teams.Value)
                {
                    var channels = await graph.Teams[team.Id].Channels.GetAsync();
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        this.teams.Add(new TeamChannelBindings(team, channels.Value));
                        ShowLoader = Visibility.Collapsed;
                    });
                }

                _dispatcherQueue.TryEnqueue(() =>
                {
                    var selectedChannelId = Windows.Storage.ApplicationData.Current.LocalSettings.Values["selectedChannelId"] as string;
                    SelectedChannel = selectedChannelId == null ? this.teams.FirstOrDefault()?.Channels.FirstOrDefault() : this.teams.SelectMany((t) => t.Channels).FirstOrDefault(c => c.Id == selectedChannelId);
                });

            });
        }
    }

    public class TeamChannelBindings
    {
        public AssociatedTeamInfo Team { get; set; }
        public ObservableCollection<Channel> Channels { get; set; }

        public TeamChannelBindings(AssociatedTeamInfo team, List<Channel> channels)
        {
            Team = team;
            Channels = new ObservableCollection<Channel>();
            foreach (var channel in channels)
            {
                Channels.Add(channel);
            }
        }
    }
}
