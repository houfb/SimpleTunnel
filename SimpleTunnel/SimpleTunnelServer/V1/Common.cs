using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SimpleTunnelServer.V1
{
    internal class Common
    {
        /// <summary>
        /// 输出日志信息到控制台
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="type"></param>
        public static void AddRich(string txt, int type = 0) { if (type == 0) Console.WriteLine(txt); }


        /// <summary>
        /// 从给定的Socket实例中读取字节，直到不能读为止，返回读取的所有字节
        /// </summary>
        /// <param name="soc"></param>
        /// <param name="blockSize"></param>
        /// <param name="maxWaitMs"></param>
        /// <returns></returns>
        public static byte[] ReadAllBytes(Socket soc, int blockSize = 1024 * 1, int maxWaitMs = 1000)//SocketAsyncEventArgs arg,
        {
            using var done = Pool.NewManualResetEventSlim();
            using var e = Pool.NewSocketAsyncEventArgs(); //e.UserToken = done; 
            e.Completed += (obj, arg) =>
            {
                try
                {
                    done.Set();
                }
                catch (Exception ex) { AddRich("M111514," + ex.Message); }
            };
            e.SetBuffer(new byte[1024 * 1], 0, 1024 * 1);


            var arr = new List<byte[]>();
            while (true)
            {
                done.Reset();

                //发起接收，并等待到完成
                if (soc.ReceiveAsync(e))//异步完成
                {
                    done.Wait(maxWaitMs);//首次连接后，要立即发生登录请求，间隔时间与网络延时都不得大于1000ms，这是为了防止恶意请求。
                }
                //else { } //同步完成


                //判断是否成功，若成功就放入内部连接池，若失败就释放掉
                if (e.SocketError == SocketError.Success && e.Buffer?.Length > 0)
                {
                    //if (e.Buffer.Length == 0) { throw new Exception("接收长度是0，判定为非法请求！"); }
                    //else 
                    if (e.Buffer[e.Buffer.Length - 1] == '\0') //以\0结束表示主动结束
                    {
                        arr.Add(e.Buffer);
                        break;
                    }
                    else if (e.Buffer.Length < e.Count) //小于buffer长度，一定是发送完了
                    {
                        arr.Add(e.Buffer);
                        break;
                    }
                    else //若至此，判定为还有内容没接收完呢
                    {
                        arr.Add(e.Buffer);
                    }
                }
            }


            var buffer = ArrayCombine(arr);
            return buffer;
        }

        /// <summary>
        /// 将多个数组拼接起来组成一个数组
        /// </summary>
        /// <param name="arr"></param>
        /// <returns></returns>
        public static byte[] ArrayCombine(IEnumerable<byte[]> arr)
        {
            //for (var i = 0; i < arr.Count(); i++) { 
            //arr[i].
            //}

            var len = 0;
            foreach (var x in arr)
            {
                len += x.Length;
            }

            var bts = new byte[len]; var cu = 0;
            foreach (var x in arr)
            {
                Array.Copy(x, 0, bts, cu, x.Length); cu += x.Length;
            }

            return bts;
        }




        static int __iseqnumber = 0; static int __iseqnumbermax = int.MaxValue - 1000;
        /// <summary>
        /// 获取一个序号，它从程序启动时的0开始累计，第一个获取出去的数为1，它仅对于程序的生存周期内是唯一的，但它是32位的，在达到最大值时，会自动归0并重新计数，这在大多数场景下，并不影响它作为一个简短的id号存在，因为即使是面对大量请求，也基本不可能有1亿个以上的请求存在于服务器上还没有给出响应。
        /// </summary>
        /// <returns></returns>
        public static int GetSeqNumber()
        {
            //if (__iseqnumber < __iseqnumbermax) return Interlocked.Increment(ref __iseqnumber);
            //else { __iseqnumber = 1; return 1; } 

            var val= Interlocked.Increment(ref __iseqnumber);//保证所有累计结果都是使用Interlocked生成的，才能保证累计结果不会因多线程而重复。
            if (__iseqnumber > __iseqnumbermax) { __iseqnumber = 0; }
            return val;
        }



    }
}
