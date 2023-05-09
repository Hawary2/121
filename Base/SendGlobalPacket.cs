using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;

namespace GameServer
{
    public unsafe class SendGlobalPacket
    {
        public unsafe void Enqueue(ServerSockets.Packet data)
        {
            var array = Pool.GamePoll.Values.ToArray();
            foreach (var user in Pool.GamePoll.Values)
            {
                user.Send(data);
            }
        }
    }
}
