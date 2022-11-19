using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
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
        /// ，若当前没有可读内容，会阻塞当前线程（maxWaitMs），直到有可读内容为止
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
                if (e.SocketError == SocketError.Success && e.BytesTransferred > 0)//e.Buffer?.Length > 0
                {
                    //if (e.Buffer.Length == 0) { throw new Exception("接收长度是0，判定为非法请求！"); }
                    //else 
                    if (e.BytesTransferred < e.Count) //小于buffer长度，一定是发送完了
                    {
                        var buf = new byte[e.BytesTransferred];
                        Array.Copy(e.Buffer, 0, buf, 0, e.BytesTransferred);
                        arr.Add(buf); //arr.Add(e.Buffer);
                        break;
                    }
                    else if (e.Buffer[e.BytesTransferred - 1] == '\0') //以\0结束表示主动结束
                    {
                        var buf = new byte[e.BytesTransferred - 1];
                        Array.Copy(e.Buffer, 0, buf, 0, e.BytesTransferred - 1);
                        arr.Add(buf); //arr.Add(e.Buffer); arr.Add(e.Buffer);
                        break;
                    }
                    else //若至此，判定为还有内容没接收完呢
                    {
                        var buf = new byte[e.BytesTransferred];
                        Array.Copy(e.Buffer, 0, buf, 0, e.BytesTransferred);
                        arr.Add(buf);   //arr.Add(e.Buffer);

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

            var val = Interlocked.Increment(ref __iseqnumber);//保证所有累计结果都是使用Interlocked生成的，才能保证累计结果不会因多线程而重复。
            if (__iseqnumber > __iseqnumbermax) { __iseqnumber = 0; }
            return val;
        }


        /// <summary>
        /// 将可能的粘包的HTTP报文字节数组拆分成多个独白的报文
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static List<HttpPack> HttpSplitPack(byte[] array)
        {
            List<HttpPack> list = new List<HttpPack>();
            if (array != null && array.Length > 0)
            {
                //foreach方案：（传说效率高，但写起来不方便，稍后再说）
                //byte last = 0; byte next = 0;//13=回车,10=换行
                //foreach (var x in array)
                //{
                //    if (x == 13) { last = x; continue; }
                //    else if (x == 10 && last == 13)
                //    { 
                //    }
                //    if (x == 0)
                //    { 
                //    } 
                //}

                var pack = new HttpPack();
                var arrA = new List<byte>();//Array.BinarySearch(,,,)
                var contentLen = 0;
                for (var i = 0; i < array.Length; i++)
                {
                    //请求体
                    if (contentLen > 0)
                    {
                        var endIdx = i + contentLen - 1;
                        if (endIdx >= array.Length)
                        {
                            endIdx = array.Length - 1;
                            contentLen = array.Length - i + 1;
                        }

                        var buff = new byte[contentLen];
                        Array.Copy(array, i, buff, 0, contentLen);
                        pack.BodyBytes = buff;

                        //
                        if (pack.HeaderBytes != null && pack.BodyBytes != null)
                        {
                            list.Add(pack);
                        }


                        i = endIdx; continue;
                    }


                    //请求头
                    var x = array[i];
                    if (x == 13)
                    {
                        if (i + 3 < array.Length)//后边须至少有3个字符，才能组成\r\n\r\n这个连接串
                        {
                            if (array[i + 1] == 10 && array[i + 2] == 13 && array[i + 3] == 10)//组成\r\n\r\n这个连接串
                            {
                                //var htxt = Encoding.UTF8.GetString(lst.ToArray());
                                pack.HeaderBytes = arrA.ToArray(); arrA.Clear();
                                contentLen = pack.ContentLength ?? (array.Length - 1 - (i + 3));//没有值的,把后续全部都当作请求体

                                i = i + 3; continue;
                            }
                        }
                    }
                    arrA.Add(x);
                }
            }
            return list;
        }
        public static List<HttpPack> HttpSplitPack2(byte[] array)
        {
            List<HttpPack> list = new List<HttpPack>();
            if (array.Length > 0)
            {
                //Array.BinarySearch(array, 0, array.Length, "\r\n");

                var pack = new HttpPack();
                var arrA = new List<byte>();//Array.BinarySearch(,,,)
                for (var i = 0; i < array.Length; i++)
                {
                    var x = array[i];
                    if (x == 13)
                    {
                        if (i + 3 < array.Length)//后边须至少有3个字符，才能组成\r\n\r\n这个连接串
                        {
                            if (array[i + 1] == 10 && array[i + 2] == 13 && array[i + 3] == 10)//组成\r\n\r\n这个连接串
                            {
                                //var htxt = Encoding.UTF8.GetString(lst.ToArray());
                                pack.HeaderBytes = arrA.ToArray(); arrA.Clear();

                                i = i + 3; continue;
                            }
                        }
                    }
                    else if (x == 0)
                    {
                        if (arrA.Count > 0)
                        {
                            pack.BodyBytes = arrA.ToArray(); arrA.Clear();
                        }
                    }
                    else
                    {




                    }



                    //else if (x == 10 && last == 13)
                    //{
                    //}
                    //if (x == 0)
                    //{
                    //}
                }
            }
            return list;
        }


        #region HttpPack
        public class HttpPack
        {
            static readonly Regex regHeader = new Regex(@"(?<=\sContent\-Length: *)\S+", RegexOptions.IgnoreCase);

            /// <summary>
            /// 头部段的字节，包括头末尾的\r\n\r\n
            /// </summary>
            public byte[] HeaderBytes;
            public byte[] BodyBytes;

            //public string HeaderText;
            public string HeaderText
            {
                get
                {
                    if (_HeaderText == null)
                    {
                        _HeaderText = Encoding.UTF8.GetString(HeaderBytes);
                    }
                    return _HeaderText;
                }
            }
            string _HeaderText;

            public int? ContentLength
            {
                get
                {
                    if (_ContentLength == -1)
                    {
                        var htxt = this.HeaderText;
                        var slen = regHeader.Match(htxt);
                        var ilen = slen.Success ? Convert.ToInt32(slen.Value) : -2;
                        _ContentLength = ilen;
                    }
                    if (_ContentLength == -2) return null;
                    else return _ContentLength;
                }
            }
            int _ContentLength = -1;

            /// <summary>
            /// 暂不使用
            /// </summary>
            public string Origin
            {
                get
                {
                    if (_Origin == null)
                    {
                        var htxt = this.HeaderText;
                        var valu = GetHeaderValue(htxt, "Origin", null);
                        _Origin = valu;
                    }
                    return _Origin;
                }
            }
            string _Origin;

            public string Host
            {
                get
                {
                    if (_Host == null)
                    {
                        var htxt = this.HeaderText;
                        var valu = GetHeaderValue(htxt, "Host", null);
                        _Host = valu;
                    }
                    return _Host;
                }
                set
                {
                    _Host = value;
                }
            }
            string _Host;

            public Socket OuterSocket
            {
                get;set;
            } 
        }
        #endregion

        public static string GetHeaderValue(string txt, string headerName, string df = "")
        {
            //示例：origin: https://www.cnblogs.com
            //Regex reg = new Regex(@"(?<=\sorigin: *)\S+", RegexOptions.IgnoreCase);
            var reg = new Regex(@"(?<=\s" + headerName + @": *)\S+", RegexOptions.IgnoreCase);
            var m = reg.Match(txt);
            if (m?.Success ?? false) { return m.Value; }
            else { return df; }
        }


        public static byte[] MakeHttpRequestMessageText(string link = null, string method = "GET", byte[] body = null, Dictionary<string, string> headers = null, string protocol = "HTTP/1.1")
        {
            link ??= "/";// link = link ?? "/";
            method = (method ?? "").ToUpper() == "POST" ? "POST" : "GET";// sb.Append("POST"); } 
            body ??= Array.Empty<byte>();
            headers ??= new Dictionary<string, string>();
            protocol ??= "HTTP/1.1"; //var type = "HTTP/1.1";




            var sb = new StringBuilder();
            sb.AppendLine($"{method} {link} {protocol}");
            foreach (var kv in headers)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                var key = (kv.Key ?? "").Replace("\r", "").Replace("\n", "").Trim();
                var val = (kv.Value ?? "").Replace("\r", "").Replace("\n", "").Trim();
                if (!string.IsNullOrEmpty(key)) sb.AppendLine($"{key}:{val}");
            }
            sb.AppendLine();

            var pre = sb.ToString();
            var buf = Encoding.UTF8.GetBytes(pre);

            var buffer = new byte[buf.Length + body.Length];
            Array.Copy(buf, 0, buffer, 0, buf.Length);
            Array.Copy(body, 0, buffer, buf.Length, body.Length);
            return buffer;




            //HTTP请求报文示例：
            //POST /framework/cmn_params HTTP/1.1
            //Accept: application/json, text/javascript, */*; q=0.01
            //Accept-Encoding: gzip, deflate, br
            //Accept-Language: zh-CN,zh-TW;q=0.9,zh;q=0.8,en;q=0.7
            //Connection: keep-alive
            //Content-Length: 0
            //Cookie: _ga_34B604LFFQ=GS1.1.1661999395.6.0.1661999395.60.0.0; _ga=GA1.1.1287016976.1661323859; ASP.NET_SessionId=24gx1py14zeqhekdjfbx1xn0; FFF=E7E914055C16369B7DBA549FE8FDF189E5002C9E9D9D9B753402CE4108236EF51FBDAC9A7326F80DFEA62DBA44240E61E63D1024D38EFB5A16CA7F7470E9CDC81FFAC06C6C8439D145B06AD8665AA6DAA73ABE668C1F666D9F16F219D0300F01BA07A95FF07E27C9618D1E6991FC4602FCA9C1BAACA5860705386F5AD6D312D85694F02528A53C928E40DF01810579659F84253E16D37FBB456DBA150F30823A
            //DNT: 1
            //Host: 9s9s.com
            //Origin: https://9s9s.com
            //Referer: https://9s9s.com/main_left.html
            //Sec-Fetch-Dest: empty
            //Sec-Fetch-Mode: cors
            //Sec-Fetch-Site: same-origin
            //User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/107.0.0.0 Safari/537.36
            //X-Requested-With: XMLHttpRequest
            //sec-ch-ua: "Google Chrome";v="107", "Chromium";v="107", "Not=A?Brand";v="24"
            //sec-ch-ua-mobile: ?0
            //sec-ch-ua-platform: "Windows"
            //
            //正文部分的内容，它可以是文本字串、二进制字节串等。 
        }


        public static byte[] MakeHttpResponseMessageText(byte[] body, Dictionary<string, string> headers = null, string code = "200", string phrase = "OK", string protocol = "HTTP/1.1")
        {
            body ??= Array.Empty<byte>();
            headers ??= new Dictionary<string, string>();
            if (!headers.ContainsKey("Content-Length")) { headers.Add("Content-Length", body.Length.ToString()); }


            var sb = new StringBuilder();
            sb.AppendLine($"{protocol} {code} {phrase}");
            foreach (var kv in headers)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                var key = (kv.Key ?? "").Replace("\r", "").Replace("\n", "").Trim();
                var val = (kv.Value ?? "").Replace("\r", "").Replace("\n", "").Trim();
                if (!string.IsNullOrEmpty(key)) sb.AppendLine($"{key}:{val}");
            }
            sb.AppendLine();

            var pre = sb.ToString();
            var buf = Encoding.UTF8.GetBytes(pre);

            var buffer = new byte[buf.Length + body.Length];
            Array.Copy(buf, 0, buffer, 0, buf.Length);
            Array.Copy(body, 0, buffer, buf.Length, body.Length);
            return buffer;



            //HTTP请求报文示例：
            //HTTP/1.1 200 OK
            //Cache-Control: private
            //Content-Type: application/json; charset=utf-8
            //X-AspNetMvc-Version: 5.2
            //X-AspNet-Version: 4.0.30319
            //X-Powered-By: ASP.NET
            //Date: Sat, 19 Nov 2022 03:10:45 GMT
            //Content-Length: 318
            //
            //正文部分的内容，它可以是文本字串、二进制字节串等。 
        }


        /// <summary>
        /// 从IPEndPoint中获取一个主机名，IP:端口
        /// </summary>
        /// <param name="ep"></param>
        /// <returns></returns>
        public static string GetHost(EndPoint ep)
        {
            if (ep == null) return null;
            var iep = ep as IPEndPoint;
            if (iep == null) return null;
            if (iep.AddressFamily == AddressFamily.InterNetwork) return iep.Address.ToString() + ":" + iep.Port;
            else if (iep.AddressFamily == AddressFamily.InterNetworkV6 && iep.Address.IsIPv4MappedToIPv6)
            {
                return iep.Address.MapToIPv4() + ":" + iep.Port;
            }
            else return iep.ToString();
            //return null;
        }


    }
}
