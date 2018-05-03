using System;
using CometD.NetCore.Bayeux;
using CometD.NetCore.Bayeux.Client;

namespace ReplaySample
{
    internal class SimpleListener : IMessageListener
    {
        public void OnMessage(IClientSessionChannel channel, IMessage message)
        {
            Console.WriteLine(message);
        }
    }
}
