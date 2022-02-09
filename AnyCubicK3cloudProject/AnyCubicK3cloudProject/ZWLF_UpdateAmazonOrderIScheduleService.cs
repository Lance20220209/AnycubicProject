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
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace AnyCubicK3cloudProject
{
    [Description("更新亚马逊销售订单定时任务插件")]
    [Kingdee.BOS.Util.HotUpdate]
    public  class ZWLF_UpdateAmazonOrderIScheduleService : IScheduleService
    {
        /// <summary>
        /// 定时任务（用于校验漏单的查询）
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="schedule"></param>
        public void Run(Kingdee.BOS.Context ctx, Schedule schedule)
        {
            try
            {
                ZWLF_GetSaleout zWLF_GetSale = new ZWLF_GetSaleout();
                STLib sTLib = new STLib();
                Context context = ctx;
                Msg msg = new Msg();
                string sql = string.Format(@"/*dialect*/ select  app_key,AppSecret,date_type,end_date,page_no,page_size,
                                     username,password,url,wh_code ,method,start_date,access_token
                                    from ZWLF_T_Configuration where id=37  and IsDisable=0");
                sql += string.Format(@"/*dialect*/ select b.OrderId from ZWLF_T_SaleOut  a inner join  ZWLF_T_SaleOutEntry b  on a.id=b.orderId
                                      where site_type='Amazon' and receiver_country in('JP','SG','DE','ES','UK','FR','IT')
                                       and b.discount_fee>0 and  b.prdt_tax_fee>0 and b.promotion_rebates_tax=0 and  IsPush =0 ");
                DataSet ds = DBServiceHelper.ExecuteDataSet(context, sql);
                DataTable dt = ds.Tables[0];
                DataTable dt_sl = ds.Tables[1];
                if (dt.Rows.Count > 0)
                {
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        Parameters parameters = new Parameters();
                        parameters.app_key = dt.Rows[i]["app_key"].ToString();
                        parameters.AppSecret = dt.Rows[i]["AppSecret"].ToString();
                        parameters.page_size = dt.Rows[i]["page_size"].ToString();
                        parameters.username = dt.Rows[i]["username"].ToString();
                        parameters.password = dt.Rows[i]["password"].ToString();
                        DateTime Btime = DateTime.Now.AddDays(-30);
                        parameters.start_date = Btime.ToString();
                        DateTime Etime = DateTime.Now;
                        parameters.end_date = Etime.ToString();
                        //parameters.wh_code = dt.Rows[i]["wh_code"].ToString();
                        parameters.page_no = Convert.ToInt32(dt.Rows[i]["page_no"].ToString());
                        parameters.url = dt.Rows[i]["url"].ToString();
                        parameters.method = dt.Rows[i]["method"].ToString();
                        //获取token
                        parameters.access_token = dt.Rows[i]["access_token"].ToString();
                        if (dt_sl.Rows.Count>0)
                        {
                            for (int a = 0; a < dt_sl.Rows.Count; a++)
                            {
                                parameters.id = dt_sl.Rows[a]["OrderId"].ToString();
                                msg = UpdateAmazonOrder(parameters, context);
                            }
                        }
                    }
                }
            }
            catch (KDException ex)
            {
                throw new KDException("", ex.ToString());
            }
        }

        /// <summary>
        /// 调用胜途销售出库接口
        /// </summary>
        /// <param name="param"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Msg UpdateAmazonOrder(Parameters param, Context context)
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
                    //本次返回的订单数
                    int total_count = Convert.ToInt32(json["data"]["total_count"].ToString());
                    #region 解析
                    JArray array = json["data"]["data"] as JArray;
                    for (int i = 0; i < array.Count; i++)
                    {
                        string sql = "";
                        #region 表头数据
                        //胜途订单id
                        string id = array[i]["id"].ToString();
                        
                        #endregion
                        #region  表体
                        //表体（一个订单可能又多个物料）
                        JArray details = array[i]["details"] as JArray;
                        for (int j = 0; j < details.Count; j++)
                        {
                            string detailid = "";
                            if (details[j].ToString().Contains("id"))
                            {
                                detailid = details[j]["id"].ToString();
                            }
                            string OrderId = id;
                            decimal promotion_rebates_tax = 0;
                            if (details[j].ToString().Contains("promotion_rebates_tax"))
                            {
                                var item = details[j]["promotion_rebates_tax"];
                                if (item != null)
                                {
                                    promotion_rebates_tax = Convert.ToDecimal(details[j]["promotion_rebates_tax"].ToString());
                                }
                            }
                            decimal details_discount_fee = 0;
                            if (details[j].ToString().Contains("discount_fee"))
                            {
                                var item = details[j]["discount_fee"];
                                if (item != null)
                                {
                                    details_discount_fee = Convert.ToDecimal(details[j]["discount_fee"].ToString());
                                }
                            }
                            if (promotion_rebates_tax > 0)
                            {
                                sql += string.Format(@"/*dialect*/ update  ZWLF_T_SaleOutEntry set promotion_rebates_tax={0},discount_fee={3}
                                                        where id='{1}' and OrderId='{2}';", promotion_rebates_tax, detailid, OrderId, details_discount_fee);
                            }
                        }
                        #endregion
                        //更新折扣税
                        if (sql != "")
                        {
                            DBServiceHelper.Execute(context, sql);
                        }
                    }

                    #endregion

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
