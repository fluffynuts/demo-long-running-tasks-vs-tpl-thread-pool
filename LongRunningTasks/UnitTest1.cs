using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace LongRunningTasks
{
    public class Tests
    {
        [Test]
        public async Task HowManyConcurrentTasksDoIGet()
        {
            // the answer is: 1 per core (so I get 12 on my 12-core machine)
            // Arrange
            var lockObject = new object();
            var max = 0;
            var current = 0;
            // Act
            var tasks = new List<Task>();
            for (var i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                    {
                        lock (lockObject)
                        {
                            current++;
                            if (current > max)
                            {
                                max = current;
                            }
                        }

                        Thread.Sleep(500);
                        lock (lockObject)
                        {
                            current--;
                        }
                    })
                );
            }

            // Assert
            await Task.WhenAll(tasks.ToArray());
            Console.WriteLine($"max concurrency: {max}");
        }
        
        [Test]
        public async Task HowManyConcurrentTasksDoIGetWhenIAlreadyHaveARunningTask()
        {
            // the answer is 11: the TPL will _not_ magically expand the thread pool
            // Arrange
            var lockObject = new object();
            var max = 0;
            var current = 0;
            var barrier = new Barrier(2);
            // Act
            var tasks = new List<Task>();
            Task.Run(() => barrier.SignalAndWait());
            for (var i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                    {
                        lock (lockObject)
                        {
                            current++;
                            if (current > max)
                            {
                                max = current;
                            }
                        }

                        Thread.Sleep(500);
                        lock (lockObject)
                        {
                            current--;
                        }
                    })
                );
            }

            // Assert
            await Task.WhenAll(tasks.ToArray());
            barrier.SignalAndWait();
            Console.WriteLine($"max concurrency: {max}");
        }
        
        [Test]
        public async Task DoesAutoResetEventGiveUpControl()
        {
            // the answer is yes: I get max concurrency of 11 on my 12-core machine
            // Arrange
            var lockObject = new object();
            var max = 0;
            var current = 0;
            var done = false;
            var doneLock = new object();
            // Act
            var tasks = new List<Task>();
            Task.Run(() =>
            {
                var ev = new AutoResetEvent(false);
                while (true)
                {
                    lock (doneLock)
                    {
                        if (done)
                        {
                            return;
                        }
                    }

                    ev.WaitOne(100);
                }
            });
            
            for (var i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                    {
                        lock (lockObject)
                        {
                            current++;
                            if (current > max)
                            {
                                max = current;
                            }
                        }

                        Thread.Sleep(500);
                        lock (lockObject)
                        {
                            current--;
                        }
                    })
                );
            }

            // Assert
            await Task.WhenAll(tasks.ToArray());
            lock (doneLock)
            {
                done = true;
            }

            Console.WriteLine($"max concurrency: {max}");
        }
        
        [Test]
        public async Task DoesALongRunningTaskInterfereWithTheTPLPool()
        {
            // the answer is no: a long-running task is not assigned to the default thread pool
            // Arrange
            var lockObject = new object();
            var max = 0;
            var current = 0;
            var done = false;
            var doneLock = new object();
            // Act
            var tasks = new List<Task>();
            Task.Factory.StartNew(() =>
            {
                var ev = new AutoResetEvent(false);
                while (true)
                {
                    lock (doneLock)
                    {
                        if (done)
                        {
                            return;
                        }
                    }

                    ev.WaitOne(100);
                }
            }, TaskCreationOptions.LongRunning);
            
            for (var i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                    {
                        lock (lockObject)
                        {
                            current++;
                            if (current > max)
                            {
                                max = current;
                            }
                        }

                        Thread.Sleep(500);
                        lock (lockObject)
                        {
                            current--;
                        }
                    })
                );
            }

            // Assert
            await Task.WhenAll(tasks.ToArray());
            lock (doneLock)
            {
                done = true;
            }

            Console.WriteLine($"max concurrency: {max}");
        }
    }
}