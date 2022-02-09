using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace AnyCubicK3cloudProject
{
    public class Httpclient
    {
        /// <summary>
        /// 开始请求
        /// </summary>
        /// <param name="params"> 参数(不要包含appscret和token)</param>
        /// <returns>服务回应</returns>
        public string RequstPost(Dictionary<string, string> @params, string url)
        {
            StringBuilder builder = new StringBuilder();
            foreach (string key in @params.Keys)
            {
                string val = key.ToLower() == "xmlvalues" ? HttpUtility.UrlEncode(@params[key], Encoding.UTF8) : @params[key];
                builder.AppendFormat("{0}={1}&", key, HttpUtility.UrlEncode(val, Encoding.UTF8));
            }
            byte[] bytesToPost = Encoding.UTF8.GetBytes(builder.ToString());
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.ContentType = "application/x-www-form-urlencoded;charset=utf-8";
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentLength = bytesToPost.Length;
            httpWebRequest.GetRequestStream().Write(bytesToPost, 0, bytesToPost.Length);
            try
            {
                HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                string response = string.Empty;
                using (StreamReader reader = new StreamReader(httpWebResponse.GetResponseStream())) response = reader.ReadToEnd();
                return response;
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }

        /// <summary>
        /// 接口请求
        /// </summary>
        /// <param name="params"> 参数(不要包含appscret和token)</param>
        /// <returns>服务回应</returns>
        public string ZWLFRequstPost(Dictionary<string, string> @params,string url,string AppSecret)
        {
            STLib sTLib = new STLib();
            StringBuilder builder = new StringBuilder();
            foreach (String key in @params.Keys)
            {
                string val = key.ToLower() == "xmlvalues" ? HttpUtility.UrlEncode(@params[key], Encoding.UTF8) : @params[key];
                builder.AppendFormat("{0}={1}&", key, HttpUtility.UrlEncode(val, Encoding.UTF8));
            }
            builder.Append("sign=" + sTLib.GetSignContent(@params, AppSecret));
            byte[] bytesToPost = Encoding.UTF8.GetBytes(builder.ToString());
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.ContentType = "application/x-www-form-urlencoded";
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentLength = bytesToPost.Length;
            httpWebRequest.GetRequestStream().Write(bytesToPost, 0, bytesToPost.Length);
            try
            {
                HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                string response = string.Empty;
                using (StreamReader reader = new StreamReader(httpWebResponse.GetResponseStream())) response = reader.ReadToEnd();
                return response;
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }
        /// <summary>
        /// Post数据接口
        /// </summary>
        /// <param name="postUrl">接口地址</param>
        /// <param name="paramData">提交json数据</param>
        /// <param name="dataEncode">编码方式(Encoding.UTF8)</param>
        /// <returns></returns>
        public string PostWebRequest(string paramData, string postUrl)
        {
            string responseContent = string.Empty;
            try
            {
                StringBuilder builder = new StringBuilder();
                builder.Append(paramData);
                byte[] byteArray = Encoding.UTF8.GetBytes(builder.ToString()); //转化
                HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(new Uri(postUrl));
                webReq.Method = "POST";
                webReq.ContentType = "application/x-www-form-urlencoded;charset=utf-8";
                webReq.ContentLength = byteArray.Length;
                using (Stream reqStream = webReq.GetRequestStream())
                {
                    reqStream.Write(byteArray, 0, byteArray.Length);//写入参数
                    //reqStream.Close();
                }
                using (HttpWebResponse response = (HttpWebResponse)webReq.GetResponse())
                {
                    //在这里对接收到的页面内容进行处理
                    using (StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        responseContent = sr.ReadToEnd().ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            return responseContent;
        }
    }
}
