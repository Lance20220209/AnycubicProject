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
    [Description("获取胜途所有退货订单定时任务插件")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_GetSaleRefundsIScheduleService : IScheduleService
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
                ZWLF_GetSaleRefunds zWLF_GetSaleRefunds = new ZWLF_GetSaleRefunds();
                STLib sTLib = new STLib();
                Context context = ctx;
                Msg msg = new Msg();
                string sql = string.Format(@"/*dialect*/ select Id, app_key,AppSecret,date_type,end_date,page_no,page_size,
                                     username,password,url,wh_code ,method,start_date,access_token
                                    from ZWLF_T_Configuration where  method in('frdas.erp.refunds.get','frdas.erp.refunds.getcloud')  and IsDisable=0");
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
                        DateTime Btime = DateTime.Now.AddDays(-5);
                        parameters.start_date = Btime.ToString();
                        DateTime Etime = DateTime.Now;
                        parameters.end_date = Etime.ToString();
                        //parameters.wh_code = dt.Rows[i]["wh_code"].ToString();
                        parameters.page_no = Convert.ToInt32(dt.Rows[i]["page_no"].ToString());
                        parameters.url = dt.Rows[i]["url"].ToString();
                        parameters.method = dt.Rows[i]["method"].ToString();
                        parameters.id = "";
                        //获取token
                        parameters.access_token = dt.Rows[i]["access_token"].ToString();
                        string Id = dt.Rows[i]["Id"].ToString();
                        msg = zWLF_GetSaleRefunds.GetSaleRefunds(parameters, context);
                        if (msg.status)
                        {
                            for (int a = 0; a < 1000; a++)
                            {
                                if (msg.sum == Convert.ToInt32(dt.Rows[i]["page_size"].ToString()))
                                {
                                    parameters.page_no = parameters.page_no + 1;
                                    msg = zWLF_GetSaleRefunds.GetSaleRefunds(parameters, context);
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
    }
}
