using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SimpleTunnelServer.V1
{
    internal class Pool
    {
        static System.Collections.Concurrent.ConcurrentStack<ManualResetEventSlim> _ManualResetEventSlimArray = new System.Collections.Concurrent.ConcurrentStack<ManualResetEventSlim>();
        static System.Collections.Concurrent.ConcurrentStack<SocketAsyncEventArgs> _SocketAsyncEventArgsArray = new System.Collections.Concurrent.ConcurrentStack<SocketAsyncEventArgs>();
        static int _lastManualResetEventSlimArrayCount = 0;
        static int _lastSocketAsyncEventArgsArray = 0;
        //static SpinWait _SpinWait = new SpinWait();

        static void BuildManualResetEventSlims()//int count
        {
            var count = _ManualResetEventSlimArray.Count;
            var step = Math.Abs(_lastManualResetEventSlimArrayCount - count);
            step = step == 0 && count == 0 ? 10 : step * 2;

            _lastManualResetEventSlimArrayCount = count + step; //BuildManualResetEventSlims(step);

            for (var i = 0; i < step; i++)
            {
                _ManualResetEventSlimArray.Push(new ManualResetEventSlim(false));
            }
        }
        static void BuildSocketAsyncEventArgss()//int count
        {
            var count = _SocketAsyncEventArgsArray.Count;
            var step = Math.Abs(_lastSocketAsyncEventArgsArray - count);
            step = step == 0 && count == 0 ? 10 : step * 2;

            _lastSocketAsyncEventArgsArray = count + step; //BuildManualResetEventSlims(step);

            for (var i = 0; i < count; i++)
            {
                _SocketAsyncEventArgsArray.Push(new SocketAsyncEventArgs());
            }
        }


        public static void Init()
        {
            Task.Run(() =>
            {
                //每间隔一段时间，向池集合中添加一批元素，这批元素的数量是通过比对上次池集合的长度的变化程度来动态计算出来的
                while (true)
                {
                    try
                    {
                        BuildManualResetEventSlims();

                        BuildSocketAsyncEventArgss();
                    }
                    catch { }
                    Thread.Sleep(1000);
                }
            });
        }


        public static ManualResetEventSlim NewManualResetEventSlim()
        {
            //do
            //{
            //ManualResetEventSlim v = null;
            ////ManualResetEventSlim? v;
            ////ManualResetEventSlim v = null;
            //SpinWait.SpinUntil(() => _ManualResetEventSlimArray.TryPop(out v));
            ////return v ?? new ManualResetEventSlim();
            //if (v == null)
            //{
            //    Interlocked.Increment(ref _lastManualResetEventSlimArrayCount);
            //    return new ManualResetEventSlim();
            //} 
            //}
            //while (true);


            //ManualResetEventSlim? v = null; 
            //SpinWait.SpinUntil(() => _ManualResetEventSlimArray.TryPop(out v));  
            //return v??new ManualResetEventSlim (); 

            //ManualResetEventSlim? v = null;
            //if (_ManualResetEventSlimArray.TryPop(out v))
            //{
            //    if (v == null) { return v!; }
            //    return v ?? new ManualResetEventSlim();
            //}
            //else
            //{
            //    return new ManualResetEventSlim();
            //}


            ManualResetEventSlim v;
            if (_ManualResetEventSlimArray.TryPop(out v!))
            { 
                return v  ;
            }
            else
            {
                Interlocked.Increment(ref _lastManualResetEventSlimArrayCount);
                return new ManualResetEventSlim();
            }

        }
        public static SocketAsyncEventArgs NewSocketAsyncEventArgs()
        {
            SocketAsyncEventArgs v;
            if (_SocketAsyncEventArgsArray.TryPop(out v!))
            {
                return v;
            }
            else
            {
                Interlocked.Increment(ref _lastManualResetEventSlimArrayCount);
                return new SocketAsyncEventArgs();
            }
        }

        //public static int Get()
        //{
        //    //int a;
        //    //SpinWait.SpinUntil(() => int.TryParse("", out a));
        //    //return a;

        //    //int a;
        //    //if (int.TryParse("", out a)) { return a; } else { }
        //    //return a;

        //    int a=null;
        //    //if (int.TryParse("", out a)) { return a; } else { }
        //    return a;
        //}






    }
}
