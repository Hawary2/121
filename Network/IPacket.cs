// * Created by AccServer
// * Copyright © 2020-2021
// * AccServer - Project

namespace AccServer.Interfaces
{
    public unsafe interface IPacket
    {
        byte[] ToArray();
        void Deserialize(byte[] buffer);
    }
}