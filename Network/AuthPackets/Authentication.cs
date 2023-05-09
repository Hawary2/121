using System;
using System.IO;
using System.Text;
using AccServer.Network.Cryptography;

namespace AccServer.Network.AuthPackets
{
    public unsafe class LoaderEncryption
    {
        static byte[] Key1 = new byte[32] { 99, 88, 140, 22, 33, 190, 244, 115, 160, 191, 213, 45, 241, 203, 99, 230, 142, 4, 19, 113, 106, 59, 245, 220, 169, 232, 121, 79, 241, 95, 193, 149 };
        static byte[] Key2 = new byte[32] { 101, 110, 251, 209, 129, 108, 198, 113, 154, 252, 111, 231, 84, 199, 183, 190, 57, 26, 225, 218, 121, 227, 232, 36, 55, 224, 48, 169, 254, 163, 166, 43 };
        public static string Decrypt(byte[] data, byte size)
        {
            byte[] BufferOut = new byte[Math.Min((int)size, 32)];
            for (int x = 0; x < Math.Min((int)size, 32); x++)
            {
                BufferOut[x] = (byte)(Key1[x * 44 % 32] ^ data[x]);
                BufferOut[x] = (byte)(Key2[x * 99 % 32] ^ BufferOut[x]);
            }
            return Encoding.Default.GetString(BufferOut).Replace("\0", "");
        }
        public static string DecryptSerial(byte[] val)
        {
            for (int x = 0; x < val.Length; x++)
            {
                val[x] = (byte)(val[x] + 250);
            }
            return Encoding.Default.GetString(val).Replace("\0", "");
        }
    }
    public class Authentication : Interfaces.IPacket
    {
        public string Username;
        public string Password;
        public string Server;
        public Authentication()
        {

        }
        public void Deserialize(byte[] buffer)
        {
            try
            {
                MemoryStream MS = new MemoryStream(buffer);
                BinaryReader BR = new BinaryReader(MS);
                BR.ReadUInt16();
                BR.ReadUInt16();
                byte UserLen = BR.ReadByte();
                byte PwLen = BR.ReadByte();
                byte ServerLen = BR.ReadByte();
                ushort serial = BR.ReadUInt16();
                BR.ReadByte();
                Username = Encoding.Default.GetString(BR.ReadBytes(UserLen));
                Username = Username.Replace("\0", "");
                byte Size = (byte)(PwLen);
                byte[] passord = new byte[PwLen];
                passord = BR.ReadBytes(PwLen);
                Password = LoaderEncryption.Decrypt(passord, Size);
                Server = Encoding.Default.GetString(BR.ReadBytes(ServerLen));
                Server = Server.Replace("\0", "");
                BR.Close();
                MS.Close();
            }
            catch
            {
                Console.WriteLine("Invalid login packet.");
            }
        }
        public unsafe byte[] ToArray()
        {
            throw new NotImplementedException();
        }
    }
}