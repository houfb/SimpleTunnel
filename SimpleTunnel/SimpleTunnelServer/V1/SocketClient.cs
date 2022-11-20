using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static SimpleTunnelServer.V1.Common;

namespace SimpleTunnelServer.V1
{
    internal class SocketClient : Socket
    {
        //public SocketClient(SocketType socketType, ProtocolType protocolType)  : base(socketType, protocolType)
        //{
        //    //base(base.AddressFamily, socketType);
        //    this(); 
        //}
        //private SocketClient(SocketType socketType, ProtocolType protocolType) : base(socketType, protocolType)
        //{ 
        //}
        //public SocketClient New( ) //: base(SocketType socketType, ProtocolType protocolType)
        //{
        // return  new SocketClient(SocketType.Stream, ProtocolType.Tcp); 
        //}
        public SocketClient() : base(SocketType.Stream, ProtocolType.Tcp)
        {

        }



        /// <summary>
        /// 最后一次发送请求时，所耗费的时间,ms
        /// </summary>
        public int lastRequestCostTime = 0;


        ///// <summary>
        ///// 实例对象的ID
        ///// </summary>
        //public int id = 0;


        /// <summary>
        /// 客户端状态：0表示已连接成功，1表示已通过登录认证，2表示已断开连接
        /// </summary>
        public short status = 0;




        /// <summary>
        /// 读取一段TCP消息,以\0分隔，将剩余未读的部分放入right中 (这不会很浪费时间，因为只在需要读取新内容 且 有可读内容时才会执行读取操作)
        /// </summary>
        /// <param name="soc"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="array"></param>
        /// <returns></returns>
        public byte[] ReadTcpPack(byte[] left, out byte[] right)
        {
            //byte ch = 0;// var list = new List<byte>(); 


            //先从上次剩余的集left中读取
            left ??= Array.Empty<byte>();
            if (left.Length > 0)//left != null && 
            {
                var idx = Array.BinarySearch(left, _o);
                if (idx >= 0)
                {
                    var buffA = new byte[idx];
                    Array.Copy(left, 0, buffA, 0, buffA.Length);
                    var buffB = new byte[left.Length - idx];
                    Array.Copy(left, idx + 1, buffB, 0, buffB.Length);

                    right = buffB;
                    return buffA;
                }
            }


            //再尝试从远端socket中读取
            var soc = this;
            if (soc != null)
            {
                using var done = Pool.NewManualResetEventSlim();
                using var e = Pool.NewSocketAsyncEventArgs();
                e.Completed += (obj, arg) => { done.Set(); };

                try
                {
                    var avaLen = soc.Available;
                    if (avaLen > 0)
                    {
                        e.SetBuffer(new byte[avaLen]);
                        if (soc.ReceiveAsync(e)) { done.Wait(TimeSpan.FromMinutes(1)); }

                        if (e.SocketError == SocketError.Success)
                        {
                            var reaLen = e.BytesTransferred;
                            var buffer = e.Buffer;

                            var idx = Array.BinarySearch(buffer, _o);
                            if (idx >= 0)
                            {
                                var buffA = new byte[left.Length + idx];
                                Array.Copy(left, 0, buffA, 0, left.Length);
                                Array.Copy(buffer, 0, buffA, left.Length, idx);
                                var buffB = new byte[reaLen - idx - 1];
                                Array.Copy(buffer, idx + 1, buffB, 0, buffB.Length);

                                right = buffB;
                                return buffA;
                            }
                            else
                            {
                                var buffB = new byte[left.Length + reaLen];
                                Array.Copy(left, 0, buffB, 0, left.Length);
                                Array.Copy(buffer, 0, buffB, left.Length, reaLen);

                                right = buffB;
                                return null;
                            }
                        }
                        else
                        {
                            AddRich($"M112008,从套接字读取字节出错,{e.SocketError}");
                        }
                    }
                }
                catch (ObjectDisposedException) //Socket 已关闭。(soc.Available)
                {
                    this.status = 2;
                    AddRich($"M112010,客户端连接已释放！");

                }
                catch (SocketException ex)//尝试访问套接字时出错。(soc.Available)
                {
                    //注意：这里的错误可能要分情况，有些错误是能被忽略的，但有些错误就要被认为是连接断开，有些错误是写的程序有问题导致，需要进一步调试后再细分，处理上，可以使用ErrorCode来区分。
                    AddRich($"M112009,从套接字读取字节出错,SocketException,错误码：{ex.ErrorCode}，描述：{ex.Message}");

                }
                //注意：其他错误故意直接抛出，尤其是InvalidOperationException(已经在使用 e 参数中指定的 SocketAsyncEventArgs 对象执行套接字操作。),因为，这些错误就属于人为写的程离有问题了
            }


            right = null;
            return null;
        }
        /// <summary>
        /// 读取一段TCP消息,以\0分隔，将剩余未读的部分放入right中 (这不会很浪费时间，因为只在需要读取新内容 且 有可读内容时才会执行读取操作)
        /// </summary>
        /// <param name="soc"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="array"></param>
        /// <returns></returns>
        public byte[] ReadTcpPack()//byte[] left, out byte[] right
        {
            return ReadTcpPack(__left, out __right);
        }
        byte[] __left = null; byte[] __right = null;





        /// <summary>
        /// 读取一段HTTP报文消息,将剩余未读的部分放入right中 (这不会很浪费时间，因为只在需要读取新内容 且 有可读内容时才会执行读取操作)
        /// </summary>
        /// <param name="soc"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="array"></param>
        /// <returns></returns>
        public byte[] ReadHttpPack_v0(byte[] left, out byte[] right)
        {
            var pack = new HttpPack(); var bodyLen = 0;


            //先从上次剩余的集left中读取
            left ??= Array.Empty<byte>();
            if (left.Length > 0)//left != null && 
            {
                int startIdx = 0, endIdx = 0;
                //if (true)
                //{
                //    var cur = 0; 
                //    var ir = Array.BinarySearch(left, cur, left.Length, _r); 
                //}



                for (var i = 0; i < left.Length; i++)
                {
                    //
                    if (bodyLen > 0)
                    {


                    }


                    var c = left[i];
                    if (c == 'G' || c == 'g')
                    {
                        if (left[i + 1] == 'E' || left[i + 1] == 'e')
                        {
                            if (left[i + 2] == 'T' || left[i + 2] == 't')
                            {
                                if (left[i + 3] == ' ')
                                {

                                }
                            }
                        }
                    }



                }







                var idx = Array.BinarySearch(left, _r);
                if (idx >= 0)
                {
                    var buffA = new byte[idx];
                    Array.Copy(left, 0, buffA, 0, buffA.Length);
                    var buffB = new byte[left.Length - idx];
                    Array.Copy(left, idx + 1, buffB, 0, buffB.Length);

                    right = buffB;
                    return buffA;
                }
            }


            //再尝试从远端socket中读取
            var soc = this;
            if (soc != null)
            {
                using var done = Pool.NewManualResetEventSlim();
                using var e = Pool.NewSocketAsyncEventArgs();
                e.Completed += (obj, arg) => { done.Set(); };

                try
                {
                    var avaLen = soc.Available;
                    if (avaLen > 0)
                    {
                        e.SetBuffer(new byte[avaLen]);
                        if (soc.ReceiveAsync(e)) { done.Wait(TimeSpan.FromMinutes(1)); }

                        if (e.SocketError == SocketError.Success)
                        {
                            var reaLen = e.BytesTransferred;
                            var buffer = e.Buffer;

                            var idx = Array.BinarySearch(buffer, _o);
                            if (idx >= 0)
                            {
                                var buffA = new byte[left.Length + idx];
                                Array.Copy(left, 0, buffA, 0, left.Length);
                                Array.Copy(buffer, 0, buffA, left.Length, idx);
                                var buffB = new byte[reaLen - idx - 1];
                                Array.Copy(buffer, idx + 1, buffB, 0, buffB.Length);

                                right = buffB;
                                return buffA;
                            }
                            else
                            {
                                var buffB = new byte[left.Length + reaLen];
                                Array.Copy(left, 0, buffB, 0, left.Length);
                                Array.Copy(buffer, 0, buffB, left.Length, reaLen);

                                right = buffB;
                                return null;
                            }
                        }
                        else
                        {
                            AddRich($"M112008,从套接字读取字节出错,{e.SocketError}");
                        }
                    }
                }
                catch (ObjectDisposedException) //Socket 已关闭。(soc.Available)
                {
                    this.status = 2;
                    AddRich($"M112010,客户端连接已释放！");

                }
                catch (SocketException ex)//尝试访问套接字时出错。(soc.Available)
                {
                    //注意：这里的错误可能要分情况，有些错误是能被忽略的，但有些错误就要被认为是连接断开，有些错误是写的程序有问题导致，需要进一步调试后再细分，处理上，可以使用ErrorCode来区分。
                    AddRich($"M112009,从套接字读取字节出错,SocketException,错误码：{ex.ErrorCode}，描述：{ex.Message}");

                }
                //注意：其他错误故意直接抛出，尤其是InvalidOperationException(已经在使用 e 参数中指定的 SocketAsyncEventArgs 对象执行套接字操作。),因为，这些错误就属于人为写的程离有问题了
            }


            right = null;
            return null;
        }
        /// <summary>
        /// 读取一段HTTP报文消息,将剩余未读的部分放入right中 (这不会很浪费时间，因为只在需要读取新内容 且 有可读内容时才会执行读取操作)
        /// </summary>
        /// <param name="soc"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="array"></param>
        /// <returns></returns>
        public byte[] ReadHttpPack_v1(byte[] left, out byte[] right)
        {
            var pack = new HttpPack(); var bodyLen = 0; var hadHead = false;


            //先从上次剩余的集left中读取
            left ??= Array.Empty<byte>();
            if (left.Length > 0)//left != null && 
            {
                int startIdx = 0, endIdx = 0;
                if (true)
                {
                    var start = 0; var cursor = 0; var max = left.Length; var pro = string.Empty;

                    var i = Array.BinarySearch(left, cursor, max, _r);
                    if (i > 10 && left[i - 1] == '1' && left[i - 2] == '.' && left[i - 3] == '1' && left[i - 4] == '/' && left[i - 9] == ' ')
                    {
                        var txt = Encoding.UTF8.GetString(left, start, i - start);
                        var reg = new Regex(@"\S+ \S+ HTTP/1.1$", RegexOptions.IgnoreCase);
                        var mat = reg.Match(txt);
                        var val = mat.Success ? mat.Value : string.Empty;
                        pro = val;

                        if (!string.IsNullOrEmpty(val))
                        {


                        }




                    }


                    if (left[i + 1] == 10 && left[i + 2] == 13 && left[i + 3] == 10)
                    {

                    }



                }



                for (var i = 0; i < left.Length; i++)
                {
                    //
                    if (bodyLen > 0)
                    {


                    }


                    var c = left[i];
                    if (c == 'G' || c == 'g')
                    {
                        if (left[i + 1] == 'E' || left[i + 1] == 'e')
                        {
                            if (left[i + 2] == 'T' || left[i + 2] == 't')
                            {
                                if (left[i + 3] == ' ')
                                {

                                }
                            }
                        }
                    }



                }







                var idx = Array.BinarySearch(left, _r);
                if (idx >= 0)
                {
                    var buffA = new byte[idx];
                    Array.Copy(left, 0, buffA, 0, buffA.Length);
                    var buffB = new byte[left.Length - idx];
                    Array.Copy(left, idx + 1, buffB, 0, buffB.Length);

                    right = buffB;
                    return buffA;
                }
            }


            //再尝试从远端socket中读取
            var soc = this;
            if (soc != null)
            {
                using var done = Pool.NewManualResetEventSlim();
                using var e = Pool.NewSocketAsyncEventArgs();
                e.Completed += (obj, arg) => { done.Set(); };

                try
                {
                    var avaLen = soc.Available;
                    if (avaLen > 0)
                    {
                        e.SetBuffer(new byte[avaLen]);
                        if (soc.ReceiveAsync(e)) { done.Wait(TimeSpan.FromMinutes(1)); }

                        if (e.SocketError == SocketError.Success)
                        {
                            var reaLen = e.BytesTransferred;
                            var buffer = e.Buffer;

                            var idx = Array.BinarySearch(buffer, _o);
                            if (idx >= 0)
                            {
                                var buffA = new byte[left.Length + idx];
                                Array.Copy(left, 0, buffA, 0, left.Length);
                                Array.Copy(buffer, 0, buffA, left.Length, idx);
                                var buffB = new byte[reaLen - idx - 1];
                                Array.Copy(buffer, idx + 1, buffB, 0, buffB.Length);

                                right = buffB;
                                return buffA;
                            }
                            else
                            {
                                var buffB = new byte[left.Length + reaLen];
                                Array.Copy(left, 0, buffB, 0, left.Length);
                                Array.Copy(buffer, 0, buffB, left.Length, reaLen);

                                right = buffB;
                                return null;
                            }
                        }
                        else
                        {
                            AddRich($"M112008,从套接字读取字节出错,{e.SocketError}");
                        }
                    }
                }
                catch (ObjectDisposedException) //Socket 已关闭。(soc.Available)
                {
                    this.status = 2;
                    AddRich($"M112010,客户端连接已释放！");

                }
                catch (SocketException ex)//尝试访问套接字时出错。(soc.Available)
                {
                    //注意：这里的错误可能要分情况，有些错误是能被忽略的，但有些错误就要被认为是连接断开，有些错误是写的程序有问题导致，需要进一步调试后再细分，处理上，可以使用ErrorCode来区分。
                    AddRich($"M112009,从套接字读取字节出错,SocketException,错误码：{ex.ErrorCode}，描述：{ex.Message}");

                }
                //注意：其他错误故意直接抛出，尤其是InvalidOperationException(已经在使用 e 参数中指定的 SocketAsyncEventArgs 对象执行套接字操作。),因为，这些错误就属于人为写的程离有问题了
            }


            right = null;
            return null;
        }
        /// <summary>
        /// 读取一段HTTP报文消息,将剩余未读的部分放入right中 (这不会很浪费时间，因为只在需要读取新内容 且 有可读内容时才会执行读取操作)
        /// * 粘包问题实在是难以有效解决，这里规定，不允许粘包，一个HTTP请求，必须要等上个HTTP请求收到结果后，才允许发出下一个，否则，一律把后续内容全部当作请求体处理。
        /// </summary>
        /// <param name="soc"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="array"></param>
        /// <returns></returns>
        public byte[] ReadHttpPack(int waitMs=0)//byte[] left, out byte[] right, ref int bodyLen, ref DateTime startReadTime, ref string headText
        {
            var soc = this;
            if (soc != null)
            {
                using var done = Pool.NewManualResetEventSlim();
                using var e = Pool.NewSocketAsyncEventArgs();
                e.Completed += (obj, arg) => { done.Set(); };


                var list = new List<BytesSegment>();//缓冲接收到的内容
                var waitMsHad = 0;
                while (true)
                {
                    try
                    {
                        var avaLen = soc.Available;
                        if (avaLen > 0)
                        {
                            e.SetBuffer(new byte[avaLen]);
                            if (soc.ReceiveAsync(e)) { done.Wait(TimeSpan.FromMinutes(1)); }

                            if (e.SocketError == SocketError.Success)
                            {
                                var reaLen = e.BytesTransferred;
                                var buffer = e.Buffer;

                                list.Add(new BytesSegment { bytes = e.Buffer, offset = e.Offset, length = e.BytesTransferred });

                            }
                            else
                            {
                                AddRich($"M112008,从套接字读取字节出错,{e.SocketError}");
                                throw new Exception($"M112008,从套接字读取字节出错,{e.SocketError}");
                            }
                        }
                        else
                        {
                            if (waitMsHad <= waitMs) { Thread.Sleep(1); waitMsHad += 1; }
                            else
                            {
                                var buff = ArrayCompine(list);
                                return buff;
                            }

                            //var total = list.Sum(x => x.length);
                            //var buffe = new byte[total]; var curIdx = 0;
                            //foreach (var x in list)
                            //{
                            //    Array.Copy(x.bytes, x.offset, buffe, curIdx, x.length);
                            //    curIdx += x.length;
                            //}
                             
                        }
                    }
                    catch (ObjectDisposedException) //Socket 已关闭。(soc.Available)
                    {
                        this.status = 2;
                        AddRich($"M112010,客户端连接已释放！");

                    }
                    catch (SocketException ex)//尝试访问套接字时出错。(soc.Available)
                    {
                        //注意：这里的错误可能要分情况，有些错误是能被忽略的，但有些错误就要被认为是连接断开，有些错误是写的程序有问题导致，需要进一步调试后再细分，处理上，可以使用ErrorCode来区分。
                        AddRich($"M112009,从套接字读取字节出错,SocketException,错误码：{ex.ErrorCode}，描述：{ex.Message}");

                    }
                    //注意：其他错误故意直接抛出，尤其是InvalidOperationException(已经在使用 e 参数中指定的 SocketAsyncEventArgs 对象执行套接字操作。),因为，这些错误就属于人为写的程离有问题了
                }


                //var buff = ArrayCompine(list);
                //return buff;
            }

            return null;
        }
        /// <summary>
        /// 读取一段HTTP报文消息,将剩余未读的部分放入right中 (这不会很浪费时间，因为只在需要读取新内容 且 有可读内容时才会执行读取操作)
        /// </summary>
        /// <param name="soc"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="array"></param>
        /// <returns></returns>
        public byte[] ReadHttpPack_vn()//byte[] left, out byte[] right
        {
            return ReadHttpPack_v1(___left, out ___right);
        }
        byte[] ___left = null; byte[] ___right = null;


        public struct BytesSegment
        {
            public byte[] bytes;
            public int offset;
            public int length;
        }

        public byte[] ArrayCompine(IEnumerable<BytesSegment> list)
        {
            var total = list.Sum(x => x.length);
            var buffe = new byte[total]; var curIdx = 0;
            foreach (var x in list)
            {
                Array.Copy(x.bytes, x.offset, buffe, curIdx, x.length);
                curIdx += x.length;
            }
            return buffe;
        }




        /// <summary>
        /// \0符
        /// </summary>
        const byte _o = 0;
        const byte _r = 13;
        /// <summary>
        /// http报文中的头体分隔符"\r\n\r\n"
        /// </summary>
        static readonly byte[] http_spt = new byte[] { 13, 10, 13, 10 };


        /// <summary>
        /// 输出日志信息到控制台
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="type"></param>
        void AddRich(string txt, int type = 0)
        {
#if DEBUG
            Console.WriteLine(txt);
#else
            if (type == 0) Console.WriteLine(txt);
#endif 
        }
    }
}
