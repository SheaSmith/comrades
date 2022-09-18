using Comrades.Services;
using Microsoft.Graph.Beta.Groups.Item.Team.Channels.Item.Messages;
using Microsoft.Graph.Beta.Models;
using Microsoft.Graph;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Comrades.Models
{
    public class IncrementalMessageLoadingCollection : ObservableCollection<ChatMessage>, ISupportIncrementalLoading
    {
        public IncrementalMessageLoadingCollection(string teamId, string channelId)
        {
            TeamId = teamId;
            ChannelId = channelId;
        }

        public bool HasMoreItems => PageIterator?.State != PagingState.Complete;

        public string TeamId { get; }
        public string ChannelId { get; }

        private PageIterator<ChatMessage, ChatMessageCollectionResponse> PageIterator = null;
        private uint LastCount = 0;
        private List<ChatMessage> chatMessages = new List<ChatMessage>();

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async cancelToken =>
            {
                if (PageIterator == null)
                {
                    var graph = await AuthenticationService.GetGraphService();

                    var request = await graph.Teams[TeamId].Channels[ChannelId].Messages.GetAsync(c => { c.QueryParameters.Expand = new string[] { "replies" }; });

                    chatMessages = new List<ChatMessage>();

                    PageIterator = PageIterator<ChatMessage, ChatMessageCollectionResponse>.CreatePageIterator(graph, request, (chat) =>
                    {
                        LastCount++;
                        chatMessages.Add(chat);
                        return true;
                    });

                    LastCount = 0;

                    await PageIterator.IterateAsync();

                    chatMessages.Reverse();
                    foreach (var chat in chatMessages)
                    {
                        Add(chat);
                    }

                    return new LoadMoreItemsResult { Count = LastCount };
                } else
                {
                    LastCount = 0;

                    chatMessages = new List<ChatMessage>();

                    await PageIterator.ResumeAsync();

                    chatMessages.Reverse();
                    foreach (var chat in chatMessages)
                    {
                        Add(chat);
                    }

                    return new LoadMoreItemsResult { Count = LastCount };
                }
            });
        }
    }
}
