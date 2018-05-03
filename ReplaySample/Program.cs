using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Threading;
using CometD.NetCore.Bayeux;
using CometD.NetCore.Bayeux.Client;
using CometD.NetCore.Client;
using CometD.NetCore.Client.Extension;
using CometD.NetCore.Client.Transport;

namespace ReplaySample
{
    class Program
    {
        // Substitute these with real values
        const string channelName = "your_channel";
        const string url = "https://your_domain.my.salesforce.com/cometd/42.0";
        const string accessToken = "access_token_for_OAuth_2.0";

        /// <summary>
        /// <see cref="ReplayExtension"/>
        /// </summary>
        private static long replayId = -2;

        static void Main(string[] args)
        {
            var longPollingTransport = new LongPollingTransport(null)
            {
                HeaderCollection = new WebHeaderCollection
                {
                    new NameValueCollection
                    {
                        {
                            "Content-Type",
                            "application/json"
                        },
                        {
                            "Authorization",
                            $"Bearer {accessToken}"
                        }
                    },
                },
                CookieCollection = new CookieCollection(),
            };

            var client = new BayeuxClient(url, new List<ClientTransport> { longPollingTransport });

            // Save the newReplayId in a reliable way. This is basically the last message processed
            // So when your application recovers a crash the next subscription should start from the last replay id processed
            var replayExtension = new ReplayExtension((changedChannel, newReplayId) => Console.WriteLine($"{changedChannel}: {newReplayId}"));
            replayExtension.SetReplayId(channelName, replayId);
            client.AddExtension(replayExtension);

            client.Handshake(new Dictionary<string, object>
            {
                { MessageFields.ReplayField, true }
            });

            var result = client.WaitFor(6000, new List<BayeuxClient.State> { BayeuxClient.State.Connected });

            // Subscription to channels
            IClientSessionChannel channel = client.GetChannel(channelName);
            var listener = new SimpleListener();

            channel.Subscribe(listener);
            //channel.Unsubscribe(listener);
            //replayExtension.SetReplayId(channelName, 100);
            //channel.Subscribe(listener);

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
