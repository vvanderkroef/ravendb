﻿using System.Diagnostics;

namespace Raven.Database.Util
{
    internal class NoOpPerformanceCounter : IPerformanceCounter
    {
        public string CounterName
        {
            get
            {
                return GetType().Name;
            }
        }

        public long Decrement()
        {
            return 0;
        }

        public long Increment()
        {
            return 0;
        }

        public long IncrementBy(long value)
        {
            return 0;
        }

        public float NextValue()
        {
            return -1;
        }

        public void Close()
        {

        }

        public void RemoveInstance()
        {

        }
    }
}