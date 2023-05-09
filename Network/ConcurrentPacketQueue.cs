// Decompiled with JetBrains decompiler
// Type: AccServer.Network.ConcurrentPacketQueue
// Assembly: AccServer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FDFCA3C4-90EE-49AE-B6B0-6D3A101B9C51
// Assembly location: D:\شغل كونكر محمود حسن\شغلى مهم جدا\شغل سي شارب\سورسات السي شارب كامله\سورس اليكس\سورس اليكس 3 دى بورتو\AccServer.exe

using System;

namespace AccServer.Network
{
    public class ConcurrentPacketQueue
    {
        private int enqueuePointer = 0;
        private int dequeuePointer = 0;
        private int enqueuedData = 0;
        private int currentPacketSize = -1;
        private byte[] queue = new byte[8192];
        private const int capacity = 8191;
        private int tqPadding;
        private object syncRoot;

        public ConcurrentPacketQueue(int padding = 8)
        {
            this.tqPadding = padding;
            this.syncRoot = new object();
        }

        public int CurrentLength
        {
            get
            {
                this.reviewPacketSize();
                return this.currentPacketSize;
            }
        }

        public void Enqueue(byte[] buffer, int length)
        {
            lock (this.syncRoot)
            {
                int index = 0;
                while (index < length)
                {
                    this.queue[this.enqueuePointer & 8191] = buffer[index];
                    ++index;
                    ++this.enqueuePointer;
                }
                this.enqueuedData += length;
            }
        }

        private void reviewPacketSize()
        {
            lock (this.syncRoot)
            {
                if (this.enqueuedData < 2)
                    this.currentPacketSize = -1;
                else
                    this.currentPacketSize = ((int)this.queue[this.dequeuePointer & 8191] | (int)this.queue[this.dequeuePointer + 1 & 8191] << 8) + this.tqPadding;
            }
        }

        public bool CanDequeue()
        {
            this.reviewPacketSize();
            if (this.currentPacketSize == -1)
                return false;
            return this.enqueuedData >= this.currentPacketSize;
        }

        public byte[] Dequeue()
        {
            lock (this.syncRoot)
            {
                if (this.currentPacketSize == -1 || this.enqueuedData < this.currentPacketSize)
                    throw new OperationCanceledException("Before calling Dequeue(), always call CanDequeue()!");
                byte[] numArray = new byte[this.currentPacketSize];
                int index = 0;
                while (index < this.currentPacketSize)
                {
                    numArray[index] = this.queue[this.dequeuePointer & 8191];
                    ++index;
                    ++this.dequeuePointer;
                }
                this.enqueuedData -= this.currentPacketSize;
                this.reviewPacketSize();
                return numArray;
            }
        }
    }
}
