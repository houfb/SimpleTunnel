﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace SimpleTunnelServer.V1
{
    internal class Server
    {
        public void Start()
        {
            //Task.Run(() =>
            //{
            //    Pool.Init();
            //    StartInnerAccept();
            //    StartOuterAccept();
            //});

            Pool.Init();
            StartInnerAccept();
            StartOuterAccept();
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
        ///// <summary>
        ///// 
        ///// </summary>
        //System.Collections.Concurrent.ConcurrentBag<STClient> _STClientArray = new System.Collections.Concurrent.ConcurrentBag<STClient>();
        //STClient[] _STClientArray = Array.Empty<STClient>();
        /// <summary>
        /// 监听的域名对象集合
        /// </summary>
        LSDomain[] _LSDomainArray = Array.Empty<LSDomain>();
        /// <summary>
        /// 操作锁
        /// </summary>
        readonly object _LSDomainArrayLock = new object();


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
            /// 最后一次发送请求时，所耗费的时间,ms
            /// </summary>
            public int lastRequestCostTime = 0;


            /// <summary>
            /// 实例对象的ID
            /// </summary>
            public int id = 0;


            /*
            /// <summary>
            /// 客户端指定要监听的域名
            /// </summary>
            public string domain;


            /// <summary>
            /// 请求信息包队列
            /// </summary>
            //public System.Collections.Concurrent.ConcurrentQueue<RequestInfoPack> requestQueue = new System.Collections.Concurrent.ConcurrentQueue<RequestInfoPack>();
            public System.Collections.Concurrent.ConcurrentQueue<RequestInfoPack> requestQueue = new System.Collections.Concurrent.ConcurrentQueue<RequestInfoPack>();


            /// <summary>
            /// 客户端状态：0表示已连接成功，1表示已通过登录认证，2表示已断开连接
            /// </summary>
            public short status = 0;
            /// <summary>
            /// 最后一次收到请求的时间
            /// </summary>
            public DateTime lastRequestTime = DateTime.Now;
            */

            ///// <summary>
            ///// 客户端指定要监听的域名
            ///// </summary>
            //public string connection;

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
        /// 监听的域名实例对象模型
        /// </summary>
        class LSDomain
        {
            /// <summary>
            /// 客户端指定要监听的域名
            /// </summary>
            public string domain;
            /// <summary>
            /// 客户端连接套接字(故意使用了普通的数组类型，以保证绝大多数的读场景下效率达到最高,同时，借用赋值操作的原子性来保证读写互不影响)
            /// </summary>
            public STClient[] sockets;
            //public List<STClient> sockets = new();


            /// <summary>
            /// 请求信息包队列(入站队列)
            /// </summary>
            public System.Collections.Concurrent.ConcurrentQueue<RequestInfoPack> requestQueue = new System.Collections.Concurrent.ConcurrentQueue<RequestInfoPack>();
            /// <summary>
            /// 响应信息包队列(出站队列)
            /// </summary>
            public System.Collections.Concurrent.ConcurrentQueue<RequestInfoPack> responseQueue = new System.Collections.Concurrent.ConcurrentQueue<RequestInfoPack>();


            ///// <summary>
            ///// 客户端状态：0表示已连接成功，1表示已通过登录认证，2表示已断开连接
            ///// </summary>
            //public short status = 0;
            ///// <summary>
            ///// 最后一次收到请求的时间
            ///// </summary>
            //public DateTime lastRequestTime = DateTime.Now;


            ///// <summary>
            ///// 客户端指定要监听的域名
            ///// </summary>
            //public string connection;

        }





        /// <summary>
        /// 开启对内监听
        /// </summary>
        void StartInnerAccept()
        {
            Task.Run(() =>
            {
                try
                {
                    using (Socket soc = new Socket(SocketType.Stream, ProtocolType.Tcp))
                    {
                        soc.Bind(new IPEndPoint(IPAddress.Any, _innerPort));
                        soc.Listen(int.MaxValue);
                        AddRich($"客户端监听已开启：{soc.LocalEndPoint}");

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
                                        AddRich($"M111810,接受连接，同步返回，出错：SocketError=[{e.SocketError}]\t AcceptSocket=[{e.AcceptSocket}]\t Connected=[{(e.AcceptSocket == null ? "" : e.AcceptSocket.Connected.ToString())}]");
                                        Thread.Sleep(1);//避免万一服务端出问题时就会出现死循环，进而导致CPU100%界面卡死的问题。
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                AddRich($"M111811,连接监听，循环内，出错：{ex.Message}");
                            }
                        }

                    }
                }
                catch (Exception ex) { AddRich($"M111812,对内监听：{ex.Message}"); }
            });
        }
        /// <summary>
        /// 接受连接成功后，将 Socket 写入管道集合 (异步和同步都调用了此方法) 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="arg"></param>
        void OnInnerAcceptCompleted(object obj, SocketAsyncEventArgs arg) //=>  //连接完成事件  SocketAsync
        {
            Socket soc = null!; SocketError err;
            try
            {
                soc = arg.AcceptSocket!;//注意，连接完成事件中，要提取这个 Socket对象才是我们想要的那个Socket。
                err = arg.SocketError;
                (obj as ManualResetEventSlim)?.Set();  //token.done.Set();//一旦结束占用，arg就会被重用，上述信息就会被重置成别人的，但为了不过长时间占用，又需要尽快结束占用

                try
                {
                    if (err == SocketError.Success && soc != null && soc.Connected)
                    {
                        AddRich($"已接受客户端连接：{soc.RemoteEndPoint}");


                        var buffer = Common.ReadAllBytes(soc, 1024 * 1, 1000); //ArrayCombine(arr);
                        var text = Encoding.UTF8.GetString(buffer);
                        AddRich("收到信息：\n" + text);


                        var text_sps = (text ?? "").Split('\r', '\n').Where(x => x.Length > 0).ToArray();
                        var cmd = text_sps.Length > 0 ? text_sps[0].Trim() : "";
                        var secret = text_sps.Length > 1 ? text_sps[1].Trim() : "";
                        var domain = text_sps.Length > 2 ? text_sps[2].Trim().ToLower() : "";


                        if (cmd == "login" && _secrets.Contains(secret) && domain.Length > 0)
                        {
                            AddRich("客户端登录已准许：" + soc.RemoteEndPoint);


                            //回致一个OK，通知客户端连接成功
                            if (true)
                            {
                                using var e = Pool.NewSocketAsyncEventArgs();
                                e.SetBuffer(Encoding.UTF8.GetBytes("OK"));//其实，发不发此消息，都是可以的，发一个只是表示一下友好而已
                                soc.SendAsync(e);
                            }


                            //将domain和socket添加到集合中
                            var stc = new STClient() { socket = soc, lastRequestCostTime = 0, id = Common.GetSeqNumber() };
                            lock (_LSDomainArrayLock) //把过程全部锁在内部，以保证线程同步，此过程理论上不会过度争抢，也不会耗费太多时间
                            {
                                var existed = _LSDomainArray.FirstOrDefault(x => x.domain == domain);
                                if (existed == null)
                                {
                                    //var arr = new List<LSDomain>(_LSDomainArray);
                                    //arr.Add(new LSDomain { domain = domain });// arr.Add(new LSDomain { domain = domain, sockets = new STClient[] { stc } });
                                    //_LSDomainArray = arr.ToArray();

                                    //var lsd = new LSDomain { domain = domain }; lsd.sockets.Add(stc);
                                    //var arr = new List<LSDomain>(_LSDomainArray) { lsd };
                                    //_LSDomainArray = arr.ToArray();

                                    var lsd = new LSDomain { domain = domain, sockets = new[] { stc } };
                                    var arr = new List<LSDomain>(_LSDomainArray) { lsd };
                                    _LSDomainArray = arr.ToArray();
                                }
                                else //已存在域名，则直接添加到其客户端集合中
                                {
                                    //var arr = new List<STClient>(existed.sockets);

                                    //if (existed.sockets.FirstOrDefault(x => x.socket == soc) == null)
                                    //{
                                    //    existed.sockets.Add(stc);
                                    //}
                                    //else
                                    //{
                                    //    //AddRich($"客户端实例已存在于集合中了，无需重复执行");
                                    //}

                                    
                                    if (existed.sockets.FirstOrDefault(x => x.socket == soc) == null)
                                    {
                                        var arr = new List<STClient>(existed.sockets) { stc  };
                                        existed.sockets = arr.ToArray();
                                    }
                                    else
                                    {
                                        //AddRich($"客户端实例已存在于集合中了，无需重复执行");
                                    }
                                }
                            }
                            AddRich($"客户端实例已进入集合：当前监听域名数: {_LSDomainArray.Length}，当前连接客户端总数：{_LSDomainArray.Sum(x => x.sockets.Length)}");



                            //给socket设置数据异步接收事件
                            if (true)
                            {
                                //while (true)
                                //{
                                //    var done = Pool.NewManualResetEventSlim();
                                //    var e = Pool.NewSocketAsyncEventArgs();
                                //    e.SetBuffer(new byte[1024]); 
                                //}
                            }





                        }
                        else
                        {
                            try
                            {
                                using (soc)
                                {
                                    using var e = Pool.NewSocketAsyncEventArgs();
                                    e.SetBuffer(Encoding.UTF8.GetBytes("DENY"));//其实，发不发此消息，都是可以的，发一个只是表示一下友好而已
                                    soc.SendAsync(e);
                                    soc.Shutdown(SocketShutdown.Both);
                                    soc.Close();
                                }
                            }
                            catch { }//注：这里不再监控日志，因为客户端可能只是发起了一个连接操作和发送操作，并没有进行接收操作，就直接关掉了连接。
                        }
                    }
                    else
                    {
                        AddRich($"M111715,接受连接，出错：SocketError=[{err}]\t AcceptSocket=[{soc}]\t Connected=[{(soc == null ? "" : soc.Connected.ToString())}]");
                        //Thread.Sleep(1);//避免万一服务端出问题时就会出现死循环，进而导致CPU100%界面卡死的问题。
                        using (soc) { }
                    }
                    //}
                }
                catch (Exception ex)
                {
                    AddRich($"M111713,出错：{ex.Message}"); using (soc) { }
                }
                //});
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




        /// <summary>
        /// 开启对外监听
        /// </summary>
        void StartOuterAccept()
        {
            Task.Run(() =>
            {
                try
                {
                    using (Socket soc = new Socket(SocketType.Stream, ProtocolType.Tcp))
                    {
                        soc.Bind(new IPEndPoint(IPAddress.Any, _outerPort));
                        soc.Listen(int.MaxValue);
                        AddRich($"对外监听已开启：{soc.LocalEndPoint}");

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
                                        AddRich($"M111810,接受连接，同步返回，出错：SocketError=[{e.SocketError}]\t AcceptSocket=[{e.AcceptSocket}]\t Connected=[{(e.AcceptSocket == null ? "" : e.AcceptSocket.Connected.ToString())}]");
                                        Thread.Sleep(1);//避免万一服务端出问题时就会出现死循环，进而导致CPU100%界面卡死的问题。
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                AddRich($"M111811,连接监听，循环内，出错：{ex.Message}");
                            }
                        }

                    }
                }
                catch (Exception ex) { AddRich($"M111812,对内监听：{ex.Message}"); }
            });
        }
        /// <summary>
        /// 接受连接成功后，将 Socket 写入管道集合 (异步和同步都调用了此方法) 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="arg"></param>
        void OnOuterAcceptCompleted(object obj, SocketAsyncEventArgs arg) //=>  //连接完成事件  SocketAsync
        {
            Socket soc = null!; SocketError err;
            try
            {
                soc = arg.AcceptSocket!;//注意，连接完成事件中，要提取这个 Socket对象才是我们想要的那个Socket。
                err = arg.SocketError;
                (obj as ManualResetEventSlim)?.Set();  //token.done.Set();//一旦结束占用，arg就会被重用，上述信息就会被重置成别人的，但为了不过长时间占用，又需要尽快结束占用

                try
                {
                    if (err == SocketError.Success && soc != null && soc.Connected)
                    {
                        AddRich($"已接受客户端连接：{soc.RemoteEndPoint}");


                        var buffer = Common.ReadAllBytes(soc, 1024 * 1, 1000); //ArrayCombine(arr);
                        var text = Encoding.UTF8.GetString(buffer);
                        AddRich("收到信息：\n" + text);


                        var text_sps = (text ?? "").Split('\r', '\n').Where(x => x.Length > 0).ToArray();
                        var cmd = text_sps.Length > 0 ? text_sps[0].Trim() : "";
                        var secret = text_sps.Length > 1 ? text_sps[1].Trim() : "";
                        var domain = text_sps.Length > 2 ? text_sps[2].Trim().ToLower() : "";


                        if (cmd == "login" && _secrets.Contains(secret) && domain.Length > 0)
                        {
                            AddRich("客户端登录已准许：" + soc.RemoteEndPoint);


                            //回致一个OK，通知客户端连接成功
                            if (true)
                            {
                                using var e = Pool.NewSocketAsyncEventArgs();
                                e.SetBuffer(Encoding.UTF8.GetBytes("OK"));//其实，发不发此消息，都是可以的，发一个只是表示一下友好而已
                                soc.SendAsync(e);
                            }


                            //将domain和socket添加到集合中
                            var stc = new STClient() { socket = soc, lastRequestCostTime = 0, id = Common.GetSeqNumber() };
                            lock (_LSDomainArrayLock) //把过程全部锁在内部，以保证线程同步，此过程理论上不会过度争抢，也不会耗费太多时间
                            {
                                var existed = _LSDomainArray.FirstOrDefault(x => x.domain == domain);
                                if (existed == null)
                                {
                                    //var arr = new List<LSDomain>(_LSDomainArray);
                                    //arr.Add(new LSDomain { domain = domain });// arr.Add(new LSDomain { domain = domain, sockets = new STClient[] { stc } });
                                    //_LSDomainArray = arr.ToArray();

                                    //var lsd = new LSDomain { domain = domain }; lsd.sockets.Add(stc);
                                    //var arr = new List<LSDomain>(_LSDomainArray) { lsd };
                                    //_LSDomainArray = arr.ToArray();

                                    var lsd = new LSDomain { domain = domain, sockets = new[] { stc } };
                                    var arr = new List<LSDomain>(_LSDomainArray) { lsd };
                                    _LSDomainArray = arr.ToArray();
                                }
                                else //已存在域名，则直接添加到其客户端集合中
                                {
                                    //var arr = new List<STClient>(existed.sockets);

                                    //if (existed.sockets.FirstOrDefault(x => x.socket == soc) == null)
                                    //{
                                    //    existed.sockets.Add(stc);
                                    //}
                                    //else
                                    //{
                                    //    //AddRich($"客户端实例已存在于集合中了，无需重复执行");
                                    //}


                                    if (existed.sockets.FirstOrDefault(x => x.socket == soc) == null)
                                    {
                                        var arr = new List<STClient>(existed.sockets) { stc };
                                        existed.sockets = arr.ToArray();
                                    }
                                    else
                                    {
                                        //AddRich($"客户端实例已存在于集合中了，无需重复执行");
                                    }
                                }
                            }
                            AddRich($"客户端实例已进入集合：当前监听域名数: {_LSDomainArray.Length}，当前连接客户端总数：{_LSDomainArray.Sum(x => x.sockets.Length)}");



                            //给socket设置数据异步接收事件
                            if (true)
                            {
                                //while (true)
                                //{
                                //    var done = Pool.NewManualResetEventSlim();
                                //    var e = Pool.NewSocketAsyncEventArgs();
                                //    e.SetBuffer(new byte[1024]); 
                                //}
                            }





                        }
                        else
                        {
                            try
                            {
                                using (soc)
                                {
                                    using var e = Pool.NewSocketAsyncEventArgs();
                                    e.SetBuffer(Encoding.UTF8.GetBytes("DENY"));//其实，发不发此消息，都是可以的，发一个只是表示一下友好而已
                                    soc.SendAsync(e);
                                    soc.Shutdown(SocketShutdown.Both);
                                    soc.Close();
                                }
                            }
                            catch { }//注：这里不再监控日志，因为客户端可能只是发起了一个连接操作和发送操作，并没有进行接收操作，就直接关掉了连接。
                        }
                    }
                    else
                    {
                        AddRich($"M111715,接受连接，出错：SocketError=[{err}]\t AcceptSocket=[{soc}]\t Connected=[{(soc == null ? "" : soc.Connected.ToString())}]");
                        //Thread.Sleep(1);//避免万一服务端出问题时就会出现死循环，进而导致CPU100%界面卡死的问题。
                        using (soc) { }
                    }
                    //}
                }
                catch (Exception ex)
                {
                    AddRich($"M111713,出错：{ex.Message}"); using (soc) { }
                }
                //});
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




    }
}
