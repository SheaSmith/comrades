using Comrades.Services;
using Microsoft.Graph;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using ABI.Windows.Data.Json;
using Microsoft.Graph.Beta.Models;

namespace Comrades.Models
{
    public class IncrementalMessageLoadingCollection : ObservableCollection<Tuple<ChatMessage, Dictionary<ChatMessageAttachment, string>>>, ISupportIncrementalLoading
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
        private List<ChatMessage> chatMessages = new();

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async cancelToken =>
            {
                var graph = await AuthenticationService.GetGraphService();

                if (PageIterator == null)
                {

                    var request = await graph.Teams[TeamId].Channels[ChannelId].Messages.GetAsync(cancellationToken: cancelToken);

                    chatMessages = new();

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
                        Dictionary<ChatMessageAttachment, string> attachmentsDict = new();
                        foreach (var attachment in chat.Attachments)
                        {
                            if (attachment.ContentType == "application/vnd.microsoft.card.codesnippet")
                            {
                                var json = JsonSerializer.Deserialize<dynamic>(attachment.Content);
                                string codeUrl = json.codeSnippetUrl;

                                var regex = new Regex("teams\\/(.+)\\/channels\\/(.+)\\/messages\\/(.+)\\/hostedContents\\/(.+)\\/", RegexOptions.Compiled);
                                MatchCollection matches = regex.Matches(codeUrl);
                                var groups = matches.First().Groups;

                                var content = await graph.Teams[groups[1].Value].Channels[groups[2].Value].Messages[groups[3].Value].HostedContents[groups[4].Value].Content.GetAsync(cancellationToken: cancelToken);
                                StreamReader reader = new StreamReader(content);

                                var code = reader.ReadToEnd();

                                attachmentsDict[attachment] = code;
                            }
                            else
                            {
                                attachmentsDict[attachment] = null;
                            }
                        }

                        Add(Tuple.Create(chat, attachmentsDict));
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
                        Dictionary<ChatMessageAttachment, string> attachmentsDict = new();
                        foreach (var attachment in chat.Attachments)
                        {
                            if (attachment.ContentType == "application/vnd.microsoft.card.codesnippet")
                            {
                                var json = JsonSerializer.Deserialize<dynamic>(attachment.Content);
                                string codeUrl = json.codeSnippetUrl;

                                var regex = new Regex("teams\\/(.+)\\/channels\\/(.+)\\/messages\\/(.+)\\/hostedContents\\/(.+)\\/", RegexOptions.Compiled);
                                MatchCollection matches = regex.Matches(codeUrl);
                                var groups = matches.First().Groups;

                                var content = await graph.Teams[groups[1].Value].Channels[groups[2].Value].Messages[groups[3].Value].HostedContents[groups[4].Value].Content.GetAsync(cancellationToken: cancelToken);
                                StreamReader reader = new StreamReader(content);

                                var code = reader.ReadToEnd();

                                attachmentsDict[attachment] = code;
                            }
                            else
                            {
                                attachmentsDict[attachment] = null;
                            }
                        }

                        Add(Tuple.Create(chat, attachmentsDict));
                    }

                    return new LoadMoreItemsResult { Count = LastCount };
                }
            });
        }
    }
}
