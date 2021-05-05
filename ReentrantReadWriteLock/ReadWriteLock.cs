using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ReentrantReadWriteLock
{
    public class ReadWriteLock
    {
        enum ThreadStatus
        {
            RUNNING,
            BLOCKING
        }

        enum ThreadType
        {
            READER,
            WRITER
        }

        private class Worker
        {
            //线程类型,写线程/读线程
            public readonly ThreadType Type;

            //线程状态,运行/阻塞
            public ThreadStatus Status;

            //真正持有线程,判定重入时使用
            public Thread thread;

            //构造函数
            public Worker(ThreadType type, Thread thread)
            {
                this.thread = thread;
                this.Type = type;
            }
            public bool isReader()
            {
                return this.Type == ThreadType.READER;
            }
            public bool isWriter()
            {
                return this.Type == ThreadType.WRITER;
            }
        }
        private volatile Queue<Worker> queue;
        private volatile int writerReentrants;
        private volatile int readerReentrants;
        private volatile Thread owner;

        public ReadWriteLock()
        {
            queue = new Queue<Worker>();
            this.writerReentrants = 0;
            this.readerReentrants = 0;
        }

        private void AwakeNextThread()
        {
            bool readflag = false;
            if (queue.Count == 0) return;
            foreach (Worker worker in queue)
            {
                if(worker.Type == ThreadType.READER)
                {
                    readflag = true;
                    worker.Status = ThreadStatus.RUNNING;
                    readerReentrants += 1;
                }
                else
                {
                    if(readflag)
                    {
                        break;
                    }
                    else
                    {
                        worker.Status = ThreadStatus.RUNNING;
                        writerReentrants += 1;
                        break;
                    }
                }
            }

        }

        private void EnqueueRunningWorker(ThreadType type)
        {
            Worker new_worker = null;
            if (type == ThreadType.WRITER)
            {
                new_worker = new Worker(ThreadType.WRITER, Thread.CurrentThread);
                writerReentrants = 1;
            }
            else
            {
                new_worker = new Worker(ThreadType.READER, Thread.CurrentThread);
                readerReentrants = 1;
            }
            owner = Thread.CurrentThread;
            new_worker.Status = ThreadStatus.RUNNING;
            queue.Enqueue(new_worker);
        }

        private Worker EnqueueBlockingWorker(ThreadType type)
        {
            Worker new_worker = null;
            if (type == ThreadType.WRITER)
            {
                new_worker = new Worker(ThreadType.WRITER, Thread.CurrentThread);
            }
            else
            {
                new_worker = new Worker(ThreadType.READER, Thread.CurrentThread);
            }
            new_worker.Status = ThreadStatus.BLOCKING;
            queue.Enqueue(new_worker);
            return new_worker;
        }

        private void EnqueueParallelReader()
        {
            Worker worker = new Worker(ThreadType.READER, Thread.CurrentThread);
            worker.Status = ThreadStatus.RUNNING;
            owner = Thread.CurrentThread;
            readerReentrants += 1;
            queue.Enqueue(worker);
        }

        private bool CanReadersparallel()
        {
            if (queue.Count == 0)
            {
                return true;
            }
            foreach (Worker item in queue)
            {
                if (item.isWriter())
                {
                    return false;
                }
            }
            return true;
        }

        public void WriteLock()
        {
            Worker blockingWorker = null;
            lock (this)
            {
                if (queue.Count == 0)
                {
                    EnqueueRunningWorker(ThreadType.WRITER);
                    return;
                }
            }
            lock (this)
            {
                if (owner == Thread.CurrentThread)
                {
                    writerReentrants += 1;
                    return;
                }
                else
                {
                    if (queue.Count == 0)
                    {
                        EnqueueRunningWorker(ThreadType.WRITER);
                        return;
                    }
                    else
                    {
                        blockingWorker = EnqueueBlockingWorker(ThreadType.WRITER);
                    }
                }
            }
            while (blockingWorker.Status != ThreadStatus.RUNNING) { }
        }

        public void WriteUnlock()
        {
            lock (this)
            {
                if (--writerReentrants == 0)
                {
                    queue.Dequeue();
                    AwakeNextThread();
                    ConsoleLog();
                }
            }
        }

        public void ReadLock()
        {
            Worker blockingWorker = null;
            lock (this)
            {
                if (queue.Count == 0)
                {
                    EnqueueRunningWorker(ThreadType.READER);
                    return;
                }
            }
            lock (this)
            {
                if (CanReadersparallel())
                {
                    EnqueueParallelReader();
                    return;
                }
                else
                {
                    if (queue.Count == 0)
                    {
                        EnqueueRunningWorker(ThreadType.READER);
                        return;
                    }
                    else
                    {
                        if (CanReadersparallel())
                        {
                            EnqueueParallelReader();
                            return;
                        }
                        else
                        {
                            blockingWorker = EnqueueBlockingWorker(ThreadType.READER);
                        }
                    }
                }
            }
            while (blockingWorker.Status != ThreadStatus.RUNNING) { }
        }

        public void ReadUnlock()
        {
            lock (this)
            {
                if (--readerReentrants == 0)
                {
                    while (true)
                    {
                        if (queue.Count == 0)
                        {
                            break;
                        }
                        else
                        {
                            Worker head = queue.Peek();
                            if (head.isReader())
                            {
                                queue.Dequeue();
                            }
                            else
                            {
                                AwakeNextThread();
                                ConsoleLog();
                                break;
                            }
                        }
                    }
                }
            }
        } 

        private void ConsoleLog()
        {
            if (queue.Count == 0)
            {
                Console.WriteLine("队列为空");
            }
            else
            {
                foreach (Worker item in queue)
                {
                    Console.WriteLine("{0}[状态:{1},持有线程ID:{2}]",
                        item.Type.ToString().ToLower(),
                        item.Status,
                        item.thread.ManagedThreadId);
                }

                Console.WriteLine("-----------------------------------");
            }
        }
    }
}
