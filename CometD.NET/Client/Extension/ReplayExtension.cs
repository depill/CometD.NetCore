using System;
using System.Collections.Concurrent;
using CometD.NetCore.Bayeux;
using CometD.NetCore.Bayeux.Client;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CometD.NetCore.Client.Extension
{
    /// <summary>
    /// The Bayeux extension for replay
    /// Author: mit.suthar@quantium.com.au
    /// CometD > 37.0 supports Replay functionality.
    /// All the messages will have a replayId in the data/event/replayId. 
    /// There is a callback every time a new replay id is processed. Which can be saved in your application to 
    ///     keep track of what was processed last. When the application recovers a crash the the last processed replay id
    ///     can be set with SetReplayId and subscribe should follow.
    /// ReplayId usage:
    /// -2 : Get all the messages originated in last 24 hours on subscription
    /// -1 : Get all the new messages originated after subscription
    /// 5 : Get all the messages originated in last 24 hours and having replay id greater than 5 
    /// 
    /// Limitation (as of now):
    /// The replay id should be known to salesforce (it should be in the retention window - last 24 hours).
    /// Ideally, if the replay id falls outside of retention window, it shold be treated as -2 or -1.
    /// Currently it does not work. It does not even get the new messages.
    /// https://developer.salesforce.com/forums/?id=9060G000000I3rlQAC
    /// 
    /// Workaround:
    /// Your application has to handle this. The best way to handle this would be,
    /// On recovering a crash subscribe via a -2 first. Look at all the messages and see if your last known replay id is there.
    /// If yes, unsubscribe, set replay id and subscribe again.
    /// If no, means your application took more than 24 hours to recover. start processing the queue you got from -2.
    /// This is not an issue if you assume that you will never have a crash which will last longer than 24 hours.
    /// </summary>
    public class ReplayExtension : IExtension
    {
        private const string ExtField = MessageFields.ReplayField;
        private bool _serverSupportsReplay;
        private readonly ConcurrentDictionary<string, long> _channelToReplayId;
        private readonly Action<string, long> _onReplayIdChanged;

        /// <param name="onReplayIdChanged">Can be used to keep track of last processed ReplayId in your application</param>
        public ReplayExtension(Action<string, long> onReplayIdChanged = null)
        {
            _channelToReplayId = new ConcurrentDictionary<string, long>();
            _onReplayIdChanged = onReplayIdChanged;
        }

        private static long? GetReplayId(IMutableMessage message)
        {
            var replayId = ((JObject)((JObject)message[MessageFields.DataField])?[MessageFields.EventField])?[MessageFields.ReplayIdField];
            return replayId != null
                ? (long)replayId
                : (long?)null;
        }

        public void SetReplayId(string channel, long replayId)
        {
            _channelToReplayId.AddOrUpdate(channel, replayId, (key, oldValue) => replayId);

            _onReplayIdChanged?.Invoke(channel, replayId);
        }

        public bool Receive(IClientSession session, IMutableMessage message)
        {
            var replayId = GetReplayId(message);
            if (_serverSupportsReplay && replayId != null)
            {
                SetReplayId(message.Channel, (long)replayId);
            }

            return true;
        }

        public bool ReceiveMeta(IClientSession session, IMutableMessage message)
        {
            if (ChannelFields.MetaHandshake.Equals(message.Channel))
            {
                var ext = message.GetExt(false);
                _serverSupportsReplay = ext?[ExtField] != null;
            }

            return true;
        }

        public bool Send(IClientSession session, IMutableMessage message)
        {
            return true;
        }

        public bool SendMeta(IClientSession session, IMutableMessage message)
        {
            switch (message.Channel)
            {
                case ChannelFields.MetaHandshake:
                    message.GetExt(true)[ExtField] = true;
                    break;
                case ChannelFields.MetaSubscribe:
                    message.GetExt(true)[ExtField] = new Dictionary<string, long>(_channelToReplayId);
                    break;
            }

            return true;
        }
    }
}
