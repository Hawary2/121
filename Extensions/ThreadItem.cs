using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;

namespace System.ThreadGroup
{
    public class ThreadItem
    {
        private int Max_ProcessInterval;
        private Mutex ThreadMutex;
        private Action Event;
        private string Name;
        public Action<int> EventSleep;
        private Thread _thread;
        private bool Alive = false;

        public ThreadItem(int interval, string MutexName, Action _Event, Action<int> Sleep = null)
        {
            _thread = new Thread(new ThreadStart(ThreadProc));
          //  _thread.Priority = ThreadPriority.Highest;
            Max_ProcessInterval = interval;
            Name = MutexName;
            ThreadMutex = new Mutex(false, MutexName);
            Event = _Event;
            EventSleep = Sleep;
        }

        public void Open()
        {
            if (Alive == false)
            {
                Alive = true;
                _thread.Start();
            }
        }

        public void ThreadProc()
        {
            while (Alive)
            {
                try
                {
                    if (!OnProcess())
                    {
                        Close();
                        break;
                    }
                }
                catch { }
            }
        }

        public void Close()
        {
            if (Alive)
            {
                Alive = false;
                _thread.Abort();
                GameServer.MyConsole.WriteLine("[WARNING] Thread " + Name + " has been aported .", ConsoleColor.DarkYellow);
            }
        }
        protected bool OnProcess()
        {
            try
            {
                DateTime clockStart = DateTime.Now;
                try
                {
                    if (Event != null)
                        Event.Invoke();
                }
                catch (Exception e) { Console.WriteLine(e.ToString()); }
                finally
                {
                    DateTime clocknow = DateTime.Now;
                    int next_stamp = Max_ProcessInterval - (int)(clocknow - clockStart).TotalMilliseconds;

                    if (next_stamp > 0 && next_stamp <= Max_ProcessInterval)
                    {

                        if (EventSleep != null)
                        {
                            EventSleep.Invoke(next_stamp);
                        }
                        else
                        {
                            if (next_stamp > 10000)
                                GameServer.MyConsole.WriteLine("[WARNING] Thread " + Name + " is gonna sleep for " + (next_stamp / 1000) + " secoonds .", ConsoleColor.DarkYellow);

                            Thread.Sleep(next_stamp);
                        }
                    }
                }
            }
            catch (Exception e) { Console.WriteLine(e.ToString()); }

            return true;
        }
    }
}
