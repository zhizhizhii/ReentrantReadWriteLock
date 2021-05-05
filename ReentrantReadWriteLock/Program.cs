using System;
using System.Threading;

namespace ReentrantReadWriteLock
{
    class Program
    {
        static void Main(string[] args)
        {
            ReadWriteLock readWriteLock = new ReadWriteLock();
            Console.WriteLine("------------------读写锁已建立------------------");
            Console.WriteLine("----------测试一：单独测试重入写锁---------");
            testReentrantWriter(readWriteLock);
            Console.WriteLine();
            Console.WriteLine("----------测试二：单独测试重入读锁---------");
            testReentrantReader(readWriteLock);
            Console.WriteLine();
            Console.WriteLine("----------测试三：读者、写者混合测试---------");
            Mixedtest(readWriteLock);
            Console.WriteLine();
        }

        public static void testReentrantWriter(ReadWriteLock readWriteLock)
        {
            for (int i = 0; i < 3; i++)
            {
                readWriteLock.WriteLock();
            }
            for (int i = 0; i < 3; i++)
            {
                readWriteLock.WriteUnlock();
            }
        }

        public static void testReentrantReader(ReadWriteLock readWriteLock)
        {
            for (int i = 0; i < 3; i++)
            {
                readWriteLock.ReadLock();
            }
            for (int i = 0; i < 3; i++)
            {
                readWriteLock.ReadUnlock();
            }
        }

        public static void testWriter(ReadWriteLock readWriteLock)
        {
            readWriteLock.WriteLock();
            Thread.Sleep(600);
            readWriteLock.WriteUnlock();
        }
        public static void testReader(ReadWriteLock readWriteLock)
        {
            readWriteLock.ReadLock();
            Thread.Sleep(300);
            readWriteLock.ReadUnlock();
        }

        public static void Mixedtest(ReadWriteLock readWriteLock)
        {
            Random rd = new Random();
            for (int i = 0; i < 10; i++)
            {
                int num = rd.Next(1, 10);
                Thread thread;
                if (num % 2 == 0)
                {
                    thread = new Thread(() => testWriter(readWriteLock));
                }
                else
                {
                    thread = new Thread(() => testReader(readWriteLock));
                }
                thread.Start();
            }
        }
    }
}