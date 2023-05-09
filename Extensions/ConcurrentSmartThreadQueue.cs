using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Collections.Concurrent;

namespace GameServer
{
     public abstract class ConcurrentSmartThreadQueue<T>
    {
        public ConcurrentQueue<T> Queues;
        public System.ThreadGroup.ThreadItem thread;

        public ConcurrentSmartThreadQueue(int Processors)
        {
            Queues = new ConcurrentQueue<T>();
        }
        protected abstract void OnDequeue(T Value, int time);

        public bool Finish()
        {
            while (Queues.Count > 0)
            {
                System.Threading.Thread.Sleep(1);
            }
            System.Threading.Thread.Sleep(500);
            return true;
        }
        public void Start(int period)
        {
            thread = new System.ThreadGroup.ThreadItem(period, "ConquerThreadQueue", Work);
            thread.Open();
        }

        public void Work()
        {
            try
            {
                DateTime timer= DateTime.Now;
                T obj;
                while (Queues.TryDequeue(out obj))
                {
                    OnDequeue(obj, timer.AllMilliseconds());
                }
            }
            catch (Exception e) { MyConsole.WriteException(e); }
        }

        public virtual void Enqueue(T obj)
        {
            Queues.Enqueue(obj);
        }
        public int Count { get { return Queues.Count; } }
    }
}
