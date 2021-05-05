using System;
using System.Threading;

namespace ReentrantReadWriteLock
{
    class Program
    {
        static void Main(string[] args)
        {
            ReadWriteLock readWriteLock = new ReadWriteLock();
            readWriteLock.WriteLock();
            readWriteLock.WriteUnlock();
            readWriteLock.ReadLock();
            readWriteLock.ReadUnlock();
        }
    }
}
