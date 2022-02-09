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
    [Description("获调用胜途调拨出库的(本地仓调出到海外仓库)")]
    [Kingdee.BOS.Util.HotUpdate]
   public class ZWLF_GetTransferScheduleService : IScheduleService
   {
        /// <summary>
        /// 调用胜途调拨出库的(本地仓调出到海外仓库)
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="schedule"></param>
        public void Run(Kingdee.BOS.Context ctx, Schedule schedule)
        {
            try
            {
                ZWLF_GetTransfer zWLF_Get = new ZWLF_GetTransfer();
                 STLib sTLib = new STLib();
                Context context = ctx;
                Msg msg = new Msg();
                string sql = string.Format(@"/*dialect*/ select  Id,app_key,AppSecret,date_type,end_date,page_no,
                                           page_size,username,password,wh_code,url,method,start_date,access_token 
                                           from ZWLF_T_Configuration where  id=6  and IsDisable=0");
                //仓库表获取配置表的仓库
                sql += string.Format(@"/*dialect*/ select  FNUMBER,F_ZWLF_WAREHOUSECODE  from  ZWLF_t_Cust_StockEntry a 
                                           inner join t_BD_Stock b on a.FStockId=b.FSTOCKID where   b.FNUMBER in ('CK002' ,'CK026','CK016')
                                           and F_ZWLF_DISABLE=0 and FUSEORGID  in (165222,100027) ");
                DataSet ds = DBServiceHelper.ExecuteDataSet(ctx, sql);
                DataTable dt = ds.Tables[0];
                DataTable dt_ck = ds.Tables[1];
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
                        DateTime Btime = Convert.ToDateTime(dt.Rows[i]["start_date"].ToString()).AddHours(-2);
                        parameters.start_date =Btime.ToString();
                        DateTime Etime = DateTime.Now;
                        parameters.end_date = Etime.ToString();
                        //parameters.wh_code = dt.Rows[i]["wh_code"].ToString();
                        parameters.page_no = Convert.ToInt32(dt.Rows[i]["page_no"].ToString());
                        parameters.url = dt.Rows[i]["url"].ToString();
                        parameters.method = dt.Rows[i]["method"].ToString();
                        parameters.id = "";
                        string Id = dt.Rows[i]["Id"].ToString();
                        //获取token
                        parameters.access_token = dt.Rows[i]["access_token"].ToString();
                        //按仓库获取订单
                        if (dt_ck.Rows.Count > 0)
                        {
                            //循环获取仓库
                            for (int j = 0; j < dt_ck.Rows.Count; j++)
                            {
                                parameters.wh_code = dt_ck.Rows[j]["F_ZWLF_WAREHOUSECODE"].ToString();
                                msg = zWLF_Get.GetTransfer(parameters, context);
                                if (msg.status)
                                {
                                    for (int a = 0; a < 1000; a++)
                                    {
                                        if (msg.sum == Convert.ToInt32(parameters.page_size))
                                        {
                                            parameters.page_no = parameters.page_no + 1;
                                            msg = msg = zWLF_Get.GetTransfer(parameters, context);
                                        }
                                        else
                                        {
                                            a = 1000;
                                            break;
                                        }
                                    }
                                    
                                }
                            }
                            sql = string.Format(@"/*dialect*/ update ZWLF_T_Configuration set start_date=end_date,end_date='{1}' 
                                         where  ID='{0}'", Id, Etime);
                            DBServiceHelper.Execute(context, sql);
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
