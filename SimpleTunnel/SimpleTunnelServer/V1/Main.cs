using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SimpleTunnelServer.V1
{
    internal class Main
    {
        public void Start()
        {
            Task.Run(() =>
            {
                Pool.Init();
                StartInnerAccept();

            });
        }

        /// <summary>
        /// 输出日志信息到控制台
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="type"></param>
        void AddRich(string txt, int type = 0) { if (type == 0) Console.WriteLine(txt); }
        /// <summary>
        /// 服务端的对外监听端口号，用于对外监听接收来自互联网的信息包
        /// </summary>
        int _outerPort = 80;
        /// <summary>
        /// 服务端的对内监听端口号，用于客户端长连接专用
        /// </summary>
        int _innerPort = 10011;
        /// <summary>
        /// 密钥集，用来验证客户端的准入
        /// </summary>
        string[] _secrets = { "5B68ECD5-E552-491B-AA23-6B37163A9FB5", "ABE334B0-059D-4EC4-B65E-BB53398AAE6D" };
        System.Collections.Concurrent.ConcurrentBag<STClient> _STClientArray = new System.Collections.Concurrent.ConcurrentBag<STClient>();

        /// <summary>
        /// 客户端实例对象模型
        /// </summary>
        class STClient
        {
            /// <summary>
            /// 客户端连接套接字
            /// </summary>
            public Socket socket;
            /// <summary>
            /// 请求信息包队列
            /// </summary>
            public System.Collections.Concurrent.ConcurrentQueue<RequestInfoPack> requestQueue = new System.Collections.Concurrent.ConcurrentQueue<RequestInfoPack>();

            /// <summary>
            /// 客户端状态：0表示已连接成功，1表示已通过登录认证，2表示已断开连接
            /// </summary>
            public short status = 0;
            /// <summary>
            /// 最后一次收到请求的时间
            /// </summary>
            public DateTime lastRequestTime = DateTime.Now;

            /// <summary>
            /// 客户端指定要监听的域名
            /// </summary>
            public string domain;
            /// <summary>
            /// 客户端指定要监听的域名
            /// </summary>
            public string connection;

        }
        /// <summary>
        /// 请求信息包模型
        /// </summary>
        class RequestInfoPack
        {
            public HttpRequestMessage request;
            public string headerText;
            public string url;
            public string method;
            public string host;

            /// <summary>
            /// 请求信息包的处理状态：0表示未处理，1表示已发送到客户端，2表示客户端已回致响应包，3表示已将响应包响应出去
            /// </summary>
            public short status;

            public HttpResponseMessage response;

        }



        /// <summary>
        /// 开启对内监听
        /// </summary>
        void StartInnerAccept()
        {
            using (Socket soc = new Socket(SocketType.Stream, ProtocolType.Tcp))
            {
                soc.Bind(new IPEndPoint(IPAddress.Any, _innerPort));
                soc.Listen(int.MaxValue);

                var done = new ManualResetEventSlim(false);
                var e = new SocketAsyncEventArgs(); //e.DisconnectReuseSocket = true;//true表示断开后可以重用（在服务端，这似乎是一个鸡肋的设置，因为服务端不会主动发数据，在接受新连接时，这个值为true是否会让监听内部重用曾经的那个socket，还真得有待实验，但即使是可以重用，也除非是服务端监听的连接就始终是就那几台有限的客户机，且客户端socket指定了固定的端口，这个设置才会好使，否则，只会让服务端大量堆积起来一堆早已无用的连接，却又不知何时释放，因不能及时释放产生的堆积将迅速耗光服务端机器的内存）
                e.Completed += new EventHandler<SocketAsyncEventArgs>(OnInnerAcceptCompleted);
                e.UserToken = done; //new UserTokenReceiving { done = done };

                while (true)
                {
                    try
                    {
                        done.Reset(); e.AcceptSocket = null;
                        var b = soc.AcceptAsync(e);//注意：MSDN上说，这个方法返回false时，表示同步完成，此时，Completed不会被调用。
                        if (b)//异步完成
                        {
                            done.Wait();//连接监听，需永久性等待 //connDone.Wait();//这个等待，与Accept的实际效果是一样的，表现上的区别无非就是将 行进信号 改成了，在 收到连接的完成事件中了（这个接受连接的完成事件中，也就同时表明了连接已建立成功）。

                        }
                        else//已同步完成 ，据MSDN说是，这里不会触发 Completed 事件，有待验证
                        {
                            if (e.SocketError == SocketError.Success && e.AcceptSocket != null && e.AcceptSocket.Connected)
                            {
                                new EventHandler<SocketAsyncEventArgs>(OnInnerAcceptCompleted).Invoke(soc, e); //借用一下异步完成事件，反正逻辑是一样的。
                                //OnAcceptCompleted(soc, e);
                                done.Wait();
                            }
                            else
                            {
                                AddRich($"接受连接，同步返回，出错：SocketError=[{e.SocketError}]\t AcceptSocket=[{e.AcceptSocket}]\t Connected=[{(e.AcceptSocket == null ? "" : e.AcceptSocket.Connected.ToString())}]");
                                Thread.Sleep(1);//避免万一服务端出问题时就会出现死循环，进而导致CPU100%界面卡死的问题。
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AddRich($"连接监听，循环内，出错：{ex.Message}");
                    }
                }

            }
        }


        #region OnAcceptCompleted   接受连接成功后，将 Socket 写入管道集合 (异步和同步都调用了此方法) 
        void OnInnerAcceptCompleted(object? obj, SocketAsyncEventArgs arg) //=>  //连接完成事件  SocketAsync
        {
            //connDone.Set(); //通知连接完成   
            //UserTokenReceiving token = null;//ManualResetEventSlim done = null;SocketAsyncEventArgs e = null;
            Socket? soc = null; SocketError err;
            try
            {
                //token = arg.UserToken as UserTokenReceiving;
                soc = arg.AcceptSocket!;//注意，连接完成事件中，要提取这个 Socket对象才是我们想要的那个Socket。
                err = arg.SocketError;
                (obj as ManualResetEventSlim)?.Set();  //token.done.Set();//一旦结束占用，arg就会被重用，上述信息就会被重置成别人的，但为了不过长时间占用，又需要尽快结束占用

                Task.Run(() =>  //包裹到一个独立线程中，以使监听尽快处理下一个连接
                {
                    try
                    {
                        //using (soc)
                        //{
                        if (err == SocketError.Success && soc != null && soc.Connected)
                        {
                            AddRich($"接受连接，成功：{soc.RemoteEndPoint}");

                            //var pip = AddPiping(soc);//注意，连接完成事件中，要提取这个 Socket对象才是我们想要的那个Socket。

                            //e = new SocketAsyncEventArgs();done = new ManualResetEventSlim(false);
                            //e.Buffer=
                            //GoReceive(pip);


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
                                    done.Wait(1000);//首次连接后，要立即发生登录请求，间隔时间与网络延时都不得大于1000ms，这是为了防止恶意请求。
                                }
                                else { } //同步完成

                                //判断是否成功，若成功就放入内部连接池，若失败就释放掉
                                if (e.SocketError == SocketError.Success && e.Buffer?.Length > 0)
                                {
                                    //var txt = Encoding.UTF8.GetString(e.Buffer);
                                    //var origin = GetHeaderValue(txt, "origin");//Origin: https://9s9s.com
                                    //var connection = GetHeaderValue(txt, "connection");//Connection: keep-alive
                                    //var stc = new STClient() { socket = soc,domain=origin, connection=connection };
                                    //if(_STClientArray)

                                    if (e.Buffer.Length == 0) { throw new Exception("接收长度是0，判定为非法请求！"); }
                                    else if (e.Buffer[e.Buffer.Length - 1] == '\0') //以\0结束表示主动结束
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
                            var text = Encoding.UTF8.GetString(buffer);
                            AddRich("收到信息：\n" + text);



                        }
                        else
                        {
                            AddRich($"M111715,接受连接，出错：SocketError=[{err}]\t AcceptSocket=[{soc}]\t Connected=[{(soc == null ? "" : soc.Connected.ToString())}]");
                            //Thread.Sleep(1);//避免万一服务端出问题时就会出现死循环，进而导致CPU100%界面卡死的问题。
                        }
                        //}
                    }
                    catch (Exception ex) { AddRich($"M111713,出错：{ex.Message}"); using (soc) { } }
                });
            }
            catch (Exception ex)
            {
                AddRich($"M111714,接受连接，出错：{ex.Message}"); using (soc) { }
            }
            finally
            {
                //try { arg.Dispose(); } catch { }
                //try { token.done.Set(); } catch { }
            }
        }
        #endregion


        string GetHeaderValue(string txt, string headerName, string df = "")
        {
            //示例：origin: https://www.cnblogs.com
            //Regex reg = new Regex(@"(?<=\sorigin: *)\S+", RegexOptions.IgnoreCase);
            var reg = new Regex(@"(?<=\s" + headerName + @": *)\S+", RegexOptions.IgnoreCase);
            var m = reg.Match(txt);
            if (m?.Success ?? false) { return m.Value; }
            else { return df; }
        }

        byte[] ArrayCombine(IEnumerable<byte[]> arr)
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


    }
}
