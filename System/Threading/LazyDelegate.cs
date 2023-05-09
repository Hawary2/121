// * Created by AccServer
// * Copyright © 2020-2021
// * AccServer - Project

namespace System.Threading
{
    public class LazyDelegate : TimerRule
    {
        public LazyDelegate(Action<int> action, int dueTime, ThreadPriority priority = ThreadPriority.Normal)
            : base(action, dueTime, priority)
        {
            this.bool_0 = false;
        }
    }
}