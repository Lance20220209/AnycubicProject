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

namespace AnyCubicK3cloudProject
{
    [Description("获取胜途所有调拨出库单保存至中间表")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_GetTranferOutToMiddle : IScheduleService
    {
        /// <summary>
        /// 定时任务
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="schedule"></param>
        public void Run(Kingdee.BOS.Context ctx, Schedule schedule)
        {
            try
            {
                Context context = ctx;
                Msg msg = new Msg();
                //配置表
                string sql = string.Format(@"/*dialect*/ select top 1  app_key,AppSecret,date_type,end_date,page_no,page_size,
                                     username,password,url,wh_code ,method,start_date,access_token
                                    from ZWLF_T_Configuration where id=32 and IsDisable=0");
                DataSet ds = DBServiceHelper.ExecuteDataSet(ctx, sql);
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
                        DateTime Btime = DateTime.Now.AddDays(-2);
                        parameters.start_date = dt.Rows[i]["start_date"].ToString();
                        DateTime Etime = DateTime.Now;
                        parameters.end_date = dt.Rows[i]["end_date"].ToString();
                        parameters.page_no = Convert.ToInt32(dt.Rows[i]["page_no"].ToString());
                        parameters.url = dt.Rows[i]["url"].ToString();
                        parameters.method = dt.Rows[i]["method"].ToString();
                        parameters.id = "";
                        //获取token
                        parameters.access_token = dt.Rows[i]["access_token"].ToString();
                        msg = GetTranferOutToMiddle(parameters, context);
                        if (msg.status)
                        {
                            for (int a = 0; a < 1000; a++)
                            {
                                if (msg.sum == Convert.ToInt32(dt.Rows[i]["page_size"].ToString()))
                                {
                                    parameters.page_no = parameters.page_no + 1;
                                    msg = GetTranferOutToMiddle(parameters, context);
                                }
                                else
                                {
                                    a = 1000;
                                    break;
                                }
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
        /// 调用胜途调拨出库单
        /// </summary>
        /// <param name="param"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Msg GetTranferOutToMiddle(Parameters param, Context context)
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
                // 0 审核出库时间，1：操作审核出库时间）
                @params["date_type"] = "0";
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
                    int total_count = Convert.ToInt32(json["data"]["total_count"].ToString());
                    if (total_count > 0)
                    {

                        string sql = "";
                        JArray array = json["data"]["data"] as JArray;
                        msg.status = true;
                        msg.sum = array.Count;
                        for (int i = 0; i < array.Count; i++)
                        {
                            string id = array[i]["id"].ToString();
                            string bil_date = array[i]["bil_date"].ToString();
                            string bil_no = array[i]["bil_no"].ToString();
                            string wh_code_out = array[i]["wh_code_out"].ToString();
                            string wh_name_out = array[i]["wh_name_out"].ToString();
                            string wh_code_in = array[i]["wh_code_in"].ToString();
                            string wh_name_in = array[i]["wh_name_in"].ToString();
                            string dept_out = "";
                            if (array[i].ToString().Contains("dept_out"))
                            {
                                dept_out = array[i]["dept_out"].ToString();
                            }
                            string expected_date = "";
                            if (array[i].ToString().Contains("expected_date"))
                            {
                                expected_date = array[i]["expected_date"].ToString();
                            }
                            string memo_info = "";
                            //if (array[i].ToString().Contains("memo_info"))
                            //{
                            //    memo_info = array[i]["memo_info"].ToString();
                            //}
                            string chk_date_out = array[i]["chk_date_out"].ToString();
                            string chk_user = "";
                            if (array[i].ToString().Contains("chk_user"))
                            {
                                chk_user = array[i]["chk_user"].ToString();
                            }

                            string chk_user_name = "";
                            if (array[i].ToString().Contains("chk_user_name"))
                            {
                                chk_user_name = array[i]["chk_user_name"].ToString();
                            }
                            string created = "";
                            if (array[i].ToString().Contains("created"))
                            {
                                created = array[i]["created"].ToString();
                            }
                            string user_name = "";
                            if (array[i].ToString().Contains("created"))
                            {
                                user_name = array[i]["user_name"].ToString();
                            }
                            ////获取仓库不是本地仓库内部调拨就要生成
                            //DataRow[] rowCK_in = dt_ck.Select("F_ZWLF_WAREHOUSECODE='" + wh_code_in + "'");
                            //if (rowCK_in.Length > 0)
                            //{
                            //    foreach (DataRow dr in rowCK_in)
                            //    {
                            //        InFstockNumber = dr["FNUMBER"].ToString();
                            //    }
                            //}
                            //DataRow[] rowCK_out = dt_ck.Select("F_ZWLF_WAREHOUSECODE='" + wh_code_out + "'");
                            //if (rowCK_out.Length > 0)
                            //{
                            //    foreach (DataRow dr in rowCK_out)
                            //    {
                            //        OutFstockNumber = dr["FNUMBER"].ToString();
                            //    }
                            //}
                            string[] ckcode = { "004", "008", "009", "010", "012", "CK002", "011" };


                            //表头
                            sql += string.Format(@" /*dialect*/ if not exists (select  *  from ZWLF_T_TranferOut where id ='{0}') begin INSERT INTO ZWLF_T_TranferOut
                                                ([id]
                                                ,[chk_user]
                                                ,[created]
                                                ,[wh_code_in]
                                                ,[expected_date]
                                                ,[wh_code_out]
                                                ,[bil_date]
                                                ,[chk_date_out]
                                                ,[user_name]
                                                ,[wh_name_in]
                                                ,[wh_name_out]
                                                ,[dept_out]
                                                ,[bil_no]
                                                ,[chk_user_name])
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
                                                ,'{12}'
                                                ,'{13}') end ;", id, chk_user, created, wh_code_in, expected_date,
                                            wh_code_out, bil_date, chk_date_out, user_name,
                                            wh_name_in, wh_name_out, dept_out, bil_no, chk_user_name);
                            //表体
                            JArray details = array[i]["details"] as JArray;
                            for (int j = 0; j < details.Count; j++)
                            {
                                string Fid = details[j]["id"].ToString();
                                string prdt_id = "";
                                if (details[j].ToString().Contains("prdt_id"))
                                {
                                    prdt_id = details[j]["prdt_id"].ToString();
                                }
                                string prdt_code = details[j]["prdt_code"].ToString();
                                string prdt_name = details[j]["prdt_name"].ToString();
                                string unit_name = "";
                                if (details[j].ToString().Contains("unit_name"))
                                {
                                    unit_name = details[j]["unit_name"].ToString();
                                }
                                string spc = "";
                                if (details[j].ToString().Contains("spc"))
                                {
                                    spc = details[j]["spc"].ToString(); ;
                                }
                                int qty_out = Convert.ToInt32(details[j]["qty_out"].ToString());
                                string Fmemo_info = "";
                                if (details[j].ToString().Contains("memo_info"))
                                {
                                    Fmemo_info = details[j]["memo_info"].ToString();
                                }
                                string parent_id = details[j]["parent_id"].ToString();
                                sql += string.Format(@"/*dialect*/ if not exists (select  *  from ZWLF_T_TranferOutEntry where id ='{0}') begin INSERT INTO ZWLF_T_TranferOutEntry
                                                       ([id]
                                                       ,[parent_id]
                                                       ,[prdt_code]
                                                       ,[prdt_name]
                                                       ,[spc]
                                                       ,[memo_info]
                                                       ,[qty_out]
                                                       ,[unit_name]
                                                       ,[prdt_id])
                                                 VALUES
                                                       ('{0}'
                                                       ,'{1}'
                                                       ,'{2}'
                                                       ,'{3}'
                                                       ,'{4}'
                                                       ,'{5}'
                                                       ,{6}
                                                       ,'{7}'
                                                       ,'{8}') end ;", Fid, parent_id, prdt_code, prdt_name,
                                                         spc, memo_info, qty_out, unit_name, prdt_id);

                            }
                        }

                        //插入数据库
                        if (sql != "")
                        {
                            DBServiceHelper.Execute(context, sql);
                        }
                    }
                }
                else
                {
                    //记录每一页获取情况
                    msg.status = false;
                    msg.result = "获取所有调拨出库单报错：" + json.ToString().Substring(0, 500).Replace("'", "");
                    string Insql = string.Format(@"INSERT INTO ZWLF_T_GetOrderCondition
                                                 ([Fmethod]
                                                 ,[Fdate_type]
                                                 ,[Fstart_date]
                                                 ,[Fend_date]
                                                 ,[Fpage_size]
                                                 ,[Fpage_no]
                                                 ,[FIsSuccess]
                                                 ,[FNote]
                                                 ,FStock)
                                           VALUES
                                                 ('{0}'
                                                 ,'1'
                                                 ,'{1}'
                                                 ,'{2}'
                                                 ,{3}
                                                 ,{4}
                                                 ,'0'
                                                 ,'{5}'
                                                 ,'{6}')",
                                                     param.method, param.start_date, param.end_date, Convert.ToInt32(param.page_size), param.page_no, msg.result, param.wh_code);
                    DBServiceHelper.Execute(context, Insql);
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
