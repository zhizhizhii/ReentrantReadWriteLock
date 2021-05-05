using System;
using System.Collections.Generic;
using System.Threading;

namespace ReentrantReadWriteLock
{
    public class ReadWriteLock
    {
        //线程类型
        enum ThreadType
        {
            READER, //读者
            WRITER  //写者
        }

        //线程状态
        enum ThreadStatus
        {
            RUNNING, //执行状态
            BLOCKING //阻塞状态
        }

        //线程
        private class ThreadWorker
        {
            //线程类型,写线程/读线程
            public readonly ThreadType Type;

            //线程状态,运行/阻塞
            public ThreadStatus Status;

            //真正持有线程,判定重入时使用
            public Thread thread;

            //构造函数
            public ThreadWorker(ThreadType type, Thread thread)
            {
                this.thread = thread;
                this.Type = type;
            }

            //判断是否为读者线程
            public bool isReader()
            {
                return this.Type == ThreadType.READER;
            }

            //判断是否为写者线程
            public bool isWriter()
            {
                return this.Type == ThreadType.WRITER;
            }
        }
        
        //维护线程工作队列
        private volatile Queue<ThreadWorker> queue;
        
        //写者重入量
        private volatile int writerReentrants;
        
        //读者重入量
        private volatile int readerReentrants;
        
        //拥有锁的线程
        private volatile Thread owner;

        /// <summary>
        /// 读写锁构造函数
        /// </summary>
        public ReadWriteLock()
        {
            queue = new Queue<ThreadWorker>();//初始化队列
            this.readerReentrants = 0;//初始化读者重入量为0
            this.writerReentrants = 0;//初始化写者重入量为0
        }

        /// <summary>
        /// 获取写锁
        /// </summary>
        public void WriteLock()
        {
            ConsoleLog();
            ThreadWorker blockingThreadWorker = null;
            lock (this)
            {
                //首先判断队列是否为空，如果为空，直接执行该写者线程
                if (queue.Count == 0)
                {
                    EnqueueRunningThread(ThreadType.WRITER);//向队尾放入执行的写者
                    return;
                }
            }
            lock (this)
            {
                //判断重入，如果重入，重入量加一后返回，无需其他操作
                if (owner == Thread.CurrentThread)
                {
                    writerReentrants += 1;//写者重入量加一
                    return;
                }
                else
                {
                    //不是重入，需要重新判定队列是否为空
                    if (queue.Count == 0)
                    {
                        EnqueueRunningThread(ThreadType.WRITER);
                        return;
                    }
                    //队列此时不为空，证明有其他线程正在执行，将当前写者线程放入队尾等待执行
                    else
                    {
                        blockingThreadWorker = EnqueueBlockingThread(ThreadType.WRITER);
                    }
                }
            }
            //阻塞线程，循环判定线程是否执行
            while (blockingThreadWorker.Status != ThreadStatus.RUNNING) { }
        }

        /// <summary>
        /// 写锁解锁
        /// </summary>
        public void WriteUnlock()
        {
            lock (this)
            {
                ConsoleLog();//打印队列状态
                /* 写锁解锁时，应首先将重入量减一，之后判断重入量是否为0
                 * 如果重入量为0，证明当前线程执行完毕，且需要唤醒线程
                 */
                if (--writerReentrants == 0)//重入量减一后判断是否为0
                {
                    queue.Dequeue();//当前线程执行完毕，出队列
                    ConsoleLog();//打印状态
                    AwakeNextThread();//唤醒接下来的线程
                }
            }
        }

        /// <summary>
        /// 获取读锁
        /// </summary>
        public void ReadLock()
        {
            ConsoleLog();//打印状态
            ThreadWorker blockingThreadWorker = null;
            lock (this)
            {
                //首先判断队列是否为空，如果为空，直接执行该读者线程
                if (queue.Count == 0)
                {
                    EnqueueRunningThread(ThreadType.READER);//向队列末尾加入执行的读者
                    return;
                }
            }
            lock (this)
            {
                /* 判定当前队列是否允许该读者与其余读者并行执行
                 * 判定的主要依据是队列中是否有写者，如果有写者，则不能执行
                 */
                if (CanReadersparallel())
                {
                    EnqueueParallelReader();//向队列末尾加入并行执行的读者
                    return;
                }
                else
                {
                    //不能并行执行，但在判定时间段后队列变为空，依旧可以执行
                    if (queue.Count == 0)
                    {
                        EnqueueRunningThread(ThreadType.READER);//向队列末尾加入执行的读者
                        return;
                    }
                    else
                    {
                        //在判定时间段后队列无写者，依旧可以并行执行
                        if (CanReadersparallel())
                        {
                            EnqueueParallelReader();//向队列末尾加入并行执行的读者
                            return;
                        }
                        else
                        {
                            //不能并行执行，那么在队列末尾放入阻塞的线程
                            blockingThreadWorker = EnqueueBlockingThread(ThreadType.READER);
                        }
                    }
                }
            }
            //阻塞线程，循环判定线程是否执行
            while (blockingThreadWorker.Status != ThreadStatus.RUNNING) { }
        }

        /// <summary>
        /// 读锁解锁
        /// </summary>
        public void ReadUnlock()
        {
            lock (this)
            {
                ConsoleLog();//打印状态
                /* 读锁解锁时，应首先将重入量减一，之后判断重入量是否为0
                 * 如果重入量为0，证明一批读者（可能并行）线程执行完毕
                 * 将它们弹出队列，并唤醒接下来的线程
                 */
                if (--readerReentrants == 0)//重入量减一后判断是否为0
                {
                    while (true)//循环判断是否是并行执行的读者线程执行完毕
                    {
                        if (queue.Count == 0)//队列为空，结束
                        {
                            break;
                        }
                        else
                        {
                            ThreadWorker head = queue.Peek();//取出队头
                            if (head.isReader())
                            {
                                queue.Dequeue();//将写者线程前所有并行执行的读者出队
                            }
                            else
                            {
                                AwakeNextThread();//所有并行执行的读者出队后，唤醒接下来的线程
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 线程执行完毕，唤醒队列中的线程
        /// </summary>
        private void AwakeNextThread()
        {
            //该标志判断是否已经有读者在本次唤醒中被执行
            bool readflag = false;
            
            //如果队列已空，无需唤醒，直接返回
            if (queue.Count == 0) return;

            /* 队列不空，需要唤醒，判断队头线程类型：
             * 1.如果队头为写线程，唤醒该线程，执行结束
             * 2.如果队头为读线程，唤醒该线程及其后连续的所有读者线程,直到遇到写线程，执行结束
             */
            foreach (ThreadWorker ThreadWorker in queue)
            {
                //队头遇到的是读者线程，唤醒，若之后连续遇到，则一直唤醒，直到遇到写者线程
                if (ThreadWorker.Type == ThreadType.READER)
                {
                    readflag = true;//标志置为true，用于遇到写者线程时的判断
                    ThreadWorker.Status = ThreadStatus.RUNNING;//唤醒
                    readerReentrants += 1;//重入量加一
                }
                //遇到的是写者线程
                else
                {
                    //如果是已经遇到读者线程后遇到的写者线程，停止读者线程的唤醒，结束执行
                    if (readflag)
                    {
                        break;
                    }
                    //如果队头遇到写者线程，仅唤醒队头的写者线程，结束执行
                    else
                    {
                        ThreadWorker.Status = ThreadStatus.RUNNING;//唤醒
                        writerReentrants += 1;//写者重入量加一
                        break;//结束执行
                    }
                }
            }

        }

        /// <summary>
        /// 向队列尾部增加一个工作状态的线程
        /// </summary>
        /// <param name="type">线程类型(READER/WRITER)</param>
        private void EnqueueRunningThread(ThreadType type)
        {
            ThreadWorker new_ThreadWorker = null;//新建线程
            if (type == ThreadType.WRITER) //创建写者线程
            {
                new_ThreadWorker = new ThreadWorker(ThreadType.WRITER, Thread.CurrentThread);
                writerReentrants = 1;
            }
            else //创建读者线程
            {
                new_ThreadWorker = new ThreadWorker(ThreadType.READER, Thread.CurrentThread);
                readerReentrants = 1;
            }
            owner = Thread.CurrentThread;//当前线程拥有锁
            new_ThreadWorker.Status = ThreadStatus.RUNNING;//线程状态置为运行
            queue.Enqueue(new_ThreadWorker);//将线程加入队列尾部
        }

        /// <summary>
        /// 向队列尾部增加一个阻塞状态的线程
        /// </summary>
        /// <param name="type">线程类型(READER/WRITER)</param>
        /// <returns>创建的读者线程</returns>
        private ThreadWorker EnqueueBlockingThread(ThreadType type)
        {
            ThreadWorker new_ThreadWorker = null;//新建线程
            if (type == ThreadType.WRITER)//创建写者线程
            {
                new_ThreadWorker = new ThreadWorker(ThreadType.WRITER, Thread.CurrentThread);
            }
            else //创建读者线程
            {
                new_ThreadWorker = new ThreadWorker(ThreadType.READER, Thread.CurrentThread);
            }
            new_ThreadWorker.Status = ThreadStatus.BLOCKING;//状态置为阻塞
            queue.Enqueue(new_ThreadWorker);//将线程放入队尾
            return new_ThreadWorker;//将新建的线程返回以便后续状态判断
        }

        /// <summary>
        /// 向队列尾部增加一个并行工作的读者线程
        /// </summary>
        private void EnqueueParallelReader()
        {
            //创建一个并行工作的读者线程
            ThreadWorker ThreadWorker = new ThreadWorker(ThreadType.READER, Thread.CurrentThread);
            ThreadWorker.Status = ThreadStatus.RUNNING;
            owner = Thread.CurrentThread;
            readerReentrants += 1;//重入量加一
            queue.Enqueue(ThreadWorker);
        }

        /// <summary>
        /// 判断此时的队列是否允许读者并行执行
        /// </summary>
        private bool CanReadersparallel()
        {
            /*
             * 如果队列中存在写者，那读者不能并行执行，
             * 如果不存在，那么可以与已执行的读者一起执行
             */
            if (queue.Count == 0)
            {
                return true;//如果队列为空，读者可以并行执行
            }
            foreach (ThreadWorker item in queue)
            {
                if (item.isWriter())
                {
                    return false;//如果队列中存在写者，需要等待写者执行完毕后才能执行该读者。
                }
            }
            return true;
        }

        private void ConsoleLog()
        {
            Console.WriteLine("--------------队列状态-----------------");
            if (queue.Count == 0)
            {
                Console.WriteLine("队列为空");
            }
            else
            {
                ThreadWorker[] q = queue.ToArray();
                for(int i = 0; i < q.Length; i++)
                {
                    ThreadWorker item = q[i];
                    Console.WriteLine("{0}[状态:{1},持有线程ID:{2},重入量{3}]",
                        item.Type.ToString().ToLower(),
                        item.Status,
                        item.thread.ManagedThreadId,
                        item.Type == ThreadType.WRITER ? writerReentrants : readerReentrants);
                }
                /*foreach (ThreadWorker item in queue)
                {
                    
                }*/
            }
            Console.WriteLine("---------------------------------------");
            Console.WriteLine();
        }
    }
}
