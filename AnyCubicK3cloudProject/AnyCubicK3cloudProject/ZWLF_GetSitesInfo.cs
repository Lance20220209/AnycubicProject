using Kingdee.BOS;
using Kingdee.BOS.Core.List;
using Kingdee.BOS.Core.List.PlugIn;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;

namespace AnyCubicK3cloudProject
{
    [Description("获取店铺信息")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_GetSitesInfo : AbstractListPlugIn
    {
        public override void BarItemClick(Kingdee.BOS.Core.DynamicForm.PlugIn.Args.BarItemClickEventArgs e)
        {
            base.BarItemClick(e);
            if (e.BarItemKey.Equals("ZWLF_tbUpdateSite"))
            {
                try
                {
                    Msg msg = new Msg();
                    //配置表
                    string sql = string.Format(@"/*dialect*/ select  top 1 app_key,AppSecret,date_type,end_date,page_no,page_size,
                                     username,password,url,wh_code ,method,start_date,access_token
                                    from ZWLF_T_Configuration where IsDisable=0");
                    DataSet ds = DBServiceHelper.ExecuteDataSet(Context, sql);
                    DataTable dt = ds.Tables[0];
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
                            parameters.page_no = Convert.ToInt32(dt.Rows[i]["page_no"].ToString());
                            parameters.url = dt.Rows[i]["url"].ToString();
                            parameters.method = "frdas.base.sites.get";
                            parameters.id = "";
                            //获取token
                            parameters.access_token = dt.Rows[i]["access_token"].ToString();
                            msg = SitesTradeGet(parameters, Context);
                            if (msg.status)
                            {
                                for (int a = 0; a < 1000; a++)
                                {
                                    if (msg.sum == Convert.ToInt32(dt.Rows[i]["page_size"].ToString()))
                                    {
                                        parameters.page_no = parameters.page_no + 1;
                                        msg = SitesTradeGet(parameters, Context);
                                    }
                                    else
                                    {
                                        a = 1000;
                                        break;
                                    }
                                }
                            }
                            this.View.ShowMessage("更新完成");
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.View.ShowErrMessage(ex.ToString());
                }
            }
        }
        /// <summary>
        /// 获取胜途店铺信息
        /// </summary>
        /// <param name="param"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private Msg SitesTradeGet(Parameters param, Context context)
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
                @params["page_size"] = param.page_size;
                @params["page_no"] = param.page_no.ToString();
                @params["site_type"] = ""; //订单唯一标识，默认为空
                Httpclient httpclient = new Httpclient();
                string json = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
                if (OrderJson["code"].ToString() == "200")
                {
                    JArray jArray = OrderJson["data"]["data"] as JArray;
                    string sql = "";
                    for (int i = 0; i < jArray.Count; i++)
                    {
                        //店铺id
                        string id = jArray[i]["id"].ToString();
                        //店铺账号
                        string site_user = jArray[i]["site_user"].ToString();
                        //店铺类型
                        string site_type = jArray[i]["site_type"].ToString();
                        //站点
                        string site_area = jArray[i]["site_area"].ToString();
                        sql += string.Format(@"/*dialect*/ update ZWLF_t_OnlineStore set F_ZWLF_SITEID='{0}',F_ZWLF_SITEAREA='{1}'
                                            where F_ZWLF_ONLINEACCOUNT='{2}' and F_ZWLF_STORETYPE='{3}'", 
                                            id, site_area, site_user, site_type);
                    }
                    if (!string.IsNullOrEmpty(sql))
                    {
                        DBServiceHelper.Execute(context,sql);
                    }
                    msg.status = true;
                    msg.sum = jArray.Count;
                    return msg;
                }
                else
                {
                    msg.result = "获取失败：" + OrderJson["msg"].ToString();
                    msg.status = false;
                }
            }
            catch (Exception ex)
            {
                msg.result = "获取失败：" + ex.ToString();
                msg.status = false;
            }
            return msg;
        }
    }
}
