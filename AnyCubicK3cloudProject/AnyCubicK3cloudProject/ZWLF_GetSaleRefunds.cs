using System;
using System.Collections.Generic;
using System.Linq;
using Kingdee.BOS;
using Kingdee.BOS.App;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Core;
using Kingdee.BOS.Core.Bill;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.FormElement;
using Kingdee.BOS.Orm;
using Kingdee.BOS.ServiceHelper;
using Kingdee.BOS.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;

namespace AnyCubicK3cloudProject
{
    public class ZWLF_GetSaleRefunds
    {
        /// <summary>
        /// 调用胜途销售退货接口（用于校验漏单的查询）
        /// </summary>
        /// <param name="param"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Msg GetSaleRefunds(Parameters param, Context context)
        {
            Msg msg = new Msg();
            try
            {
                //应用级输入参数：
                Dictionary<string, string> @params = new Dictionary<string, string>();
                @params.Add("method", param.method);
                @params.Add("app_key", param.app_key);
                @params.Add("sign_method", "md5");
                // @params.Add("sign", zWLF.sign); 
                @params.Add("session", param.access_token);
                @params.Add("timestamp", DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                @params.Add("format", "json");
                @params.Add("v", "1.0");
                //业务输入参数：
                // 默认为0（0:平台发货时间，1：系统发货时间）
                @params["date_type"] = "1";
                DateTime Btime = Convert.ToDateTime(param.start_date);
                @params["start_date"] = Btime.ToString("yyyy-MM-dd HH:mm:ss");
                DateTime Etime = Convert.ToDateTime(param.end_date);
                @params["end_date"] = Etime.ToString("yyyy-MM-dd HH:mm:ss");
                @params["page_size"] = param.page_size;
                @params["page_no"] = param.page_no.ToString();
                @params["id"] = param.id; //订单唯一标识，默认为空
                @params["site_id"] = "";// 店铺ID
                @params["wh_code"] = param.wh_code;// 仓库编码
                Httpclient httpclient = new Httpclient();
                string OrderJson = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                JObject json = JsonConvert.DeserializeObject<JObject>(OrderJson);
                string code = json["code"].ToString();
                if (code == "200")
                {
                    string sql = "";
                    //本次返回的订单数
                    int total_count = Convert.ToInt32(json["data"]["total_count"].ToString());
                    JArray array = json["data"]["data"] as JArray;
                    for (int i = 0; i < array.Count; i++)
                    {
                        //退换货处理ID
                        string id = array[i]["id"].ToString();
                        //售后登记ID
                        string regist_id = "";
                        if (array[i].ToString().Contains("regist_id"))
                        {
                            regist_id = array[i]["regist_id"].ToString();
                        }
                        //售后退回单号
                        string bil_no = array[i]["bil_no"].ToString();
                        //发货仓库编码
                        string back_wh_code = array[i]["back_wh_code"].ToString();
                        //仓库名称
                        string back_wh_name = array[i]["back_wh_name"].ToString();
                        //网店名称
                        string site_name = "";
                        if (array[i].ToString().Contains("site_name"))
                        {
                            site_name = array[i]["site_name"].ToString();
                        }
                        //网店账号
                        string site_user = "";
                        if (array[i].ToString().Contains("site_user"))
                        {
                            site_user = array[i]["site_user"].ToString();
                        }
                        //平台类型
                        string site_type = "";
                        if (array[i].ToString().Contains("site_type"))
                        {
                            site_type = array[i]["site_type"].ToString();
                        }
                        //平台类型id
                        string site_type_id = "";
                        if (array[i].ToString().Contains("site_type_id"))
                        {
                            site_type_id = array[i]["site_type_id"].ToString();
                        }
                        //退货时间
                        string back_time = array[i]["back_time"].ToString();
                        //网上订单号
                        string site_trade_id = "";
                        if (array[i].ToString().Contains("site_trade_id"))
                        {
                            site_trade_id = array[i]["site_trade_id"].ToString();
                        }
                        //币别
                        string cur_code = "";
                        if (array[i].ToString().Contains("cur_code"))
                        {
                            cur_code = array[i]["cur_code"].ToString();
                        }
                        string order_id = "";
                        ////网上订单号
                        //if (array[i].ToString().Contains("order_id"))
                        //{
                        //    order_id = array[i]["order_id"].ToString();
                        //}
                        //string 
                        //方法
                        string method = param.method;
                        //推送状态
                        string IsPush = "0";
                        sql += string.Format(@"/*dialect*/ if not exists (select  *  from ZWLF_T_SaleRefunds where id ='{0}' and bil_no='{1}') 
                                                begin  INSERT INTO [dbo].[ZWLF_T_SaleRefunds]
                                               ([id]
                                               ,[bil_no]
                                               ,[back_time]
                                               ,[regist_id]
                                               ,[site_type]
                                               ,[site_type_id]
                                               ,[site_name]
                                               ,[site_user]
                                               ,[site_trade_id]
                                               ,[back_wh_code]
                                               ,[back_wh_name]
                                               ,[cur_code]
                                               ,[order_id],IsPush,method)
                                         VALUES
                                               ('{0}'
                                               ,'{1}'
                                               ,'{2}'
                                               ,'{3}'
                                               ,'{4}'
                                               ,'{5}'
                                               ,'{6}'
                                               ,'{7}'
                                               ,'{8}'
                                               ,'{9}'
                                               ,'{10}'
                                               ,'{11}'
                                               ,'{12}','{13}','{14}') end ;",
                                                 id, bil_no, back_time, regist_id, site_type, site_type_id, site_name
                                                 , site_user, site_trade_id, back_wh_code, back_wh_name, cur_code, order_id, IsPush, method);
                    }
                    //插入数据库
                    if (sql != "")
                    {
                        DBServiceHelper.Execute(context, sql);
                    }
                    msg.status = true;
                    msg.sum = array.Count;
                }
                return msg;
            }
            catch (KDException ex)
            {
                //记录每一页获取情况
                msg.status = false;
                msg.result = ex.ToString().Substring(0, 500).Replace("'", "");
                return msg;
            }
        }
    }
}
