using System;
using System.Collections.Generic;
using System.Linq;
using Kingdee.BOS;
using Kingdee.BOS.App;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Core;
using Kingdee.BOS.Core.Bill;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.Operation;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.Interaction;
using Kingdee.BOS.Core.List;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.ConvertElement.ServiceArgs;
using Kingdee.BOS.Core.Metadata.FormElement;
using Kingdee.BOS.Orm;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using Kingdee.BOS.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Data;
using Kingdee.BOS.Core.Metadata.FieldElement;

namespace AnyCubicK3cloudProject
{
    public class ZWLF_GetMovein
    {
        /// <summary>
        /// 调用胜途海外仓调拨入库（海外总仓调入海外总仓）
        /// </summary>
        /// <param name="app_key"></param>
        /// <param name="AppSecret"></param>
        /// <param name="page_size"></param>
        /// <param name="page_no"></param>
        /// <returns></returns>
        public Msg GetMovein(Parameters param, Context context)
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
                DateTime Btime = Convert.ToDateTime(param.start_date);
                @params["start_date"] = Btime.ToString("yyyy-MM-dd HH:mm:ss");
                DateTime Etime = Convert.ToDateTime(param.end_date);
                @params["end_date"] = Etime.ToString("yyyy-MM-dd HH:mm:ss");
                @params["page_size"] = param.page_size;
                @params["page_no"] = param.page_no.ToString();
                @params["wh_code"] = param.wh_code;
                @params["id"] = param.id;  //库存调拨单id
                Httpclient httpclient = new Httpclient();
                string json = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
                string code = OrderJson["code"].ToString();
                if (code == "200")
                {
                    int total_count = Convert.ToInt32(OrderJson["data"]["total_count"].ToString()); //本次返回的订单数
                    if (total_count > 0)
                    {
                        //记录每一页获取情况
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
                                                 ,'1'
                                                 ,'{5}' 
                                                 ,'{6}' )", param.method, Btime, Etime, Convert.ToInt32(param.page_size), param.page_no, json.Replace("'", ""), param.wh_code);
                        DBServiceHelper.Execute(context, Insql);
                        //获取所有分布式调拨出库单
                        string sql = string.Format(@"/*dialect*/ select F_ZWLF_Id , F_ZWLF_TransfersNo,FID  from  T_STK_STKTRANSFEROUT where F_ZWLF_Id !='' and  F_ZWLF_TransfersNo !='';");
                        //仓库映射本地仓库
                        sql += string.Format(@"/*dialect*/ select  FNUMBER,F_ZWLF_WAREHOUSECODE,FUSEORGID from  ZWLF_t_Cust_StockEntry a 
                                           inner join t_BD_Stock b on a.FStockId=b.FSTOCKID where F_ZWLF_DISABLE=0 ;");
                        //获取分布式调入单
                        sql += string.Format(@"/*dialect*/ select F_ZWLF_Id , F_ZWLF_TransfersNo,FID  from  T_STK_STKTRANSFERIN where F_ZWLF_Id !='' and  F_ZWLF_TransfersNo !='';");
                        //物料
                        sql += string.Format(@"select FMATERIALID, FNUMBER  from  T_BD_MATERIAL");

                        DataSet ds = DBServiceHelper.ExecuteDataSet(context, sql);
                        //生成分布式调入单
                        msg = GetMoveinPush(OrderJson, ds, context);
                    }
                }
                else
                {
                    //记录每一页获取情况
                    string Insql = string.Format(@"INSERT INTO ZWLF_T_GetOrderCondition
                                                 ([Fmethod]
                                                 ,[Fdate_type]
                                                 ,[Fstart_date]
                                                 ,[Fend_date]
                                                 ,[Fpage_size]
                                                 ,[Fpage_no]
                                                 ,[FIsSuccess]
                                                  ,FNote
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
                                                 ,'{6}' )", param.method, param.end_date, Etime, Convert.ToInt32(param.page_size), param.page_no, OrderJson.ToString().Replace("'", ""), param.wh_code);
                    DBServiceHelper.Execute(context, Insql);
                    msg.status = false;
                }
                return msg;
            }
            catch (KDException ex)
            {
                //记录每一页获取情况
                msg.status = false;
                msg.result = ex.ToString().Substring(0, 500).Replace("'", "");
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
                return msg;
            }
        }


        /// <summary>
        /// 生成金蝶分步式调入单
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public Msg GetMoveinPush(JObject json, DataSet ds, Context context)
        {
            Msg msg = new Msg();
            DataTable dt_out = ds.Tables[0];
            DataTable CKdt = ds.Tables[1];
            DataTable dt_In = ds.Tables[2];
            DataTable dt_wl = ds.Tables[3];
            int total_count = Convert.ToInt32(json["data"]["total_count"].ToString()); //本次返回的订单数
            JArray array = json["data"]["data"] as JArray;
            #region 循环生成
            for (int i = 0; i < array.Count; i++)
            {
                try
                {
                    //调入仓库编码
                    string InFstockNumber = "";
                    //调入组织
                    string InFStockOrgId = "";
                    //调出仓库编码
                    string OutFstockNumber = "";
                    //调出组织
                    string OutFStockOrgId = "";
                    string checkMsg = "";
                    bool checkstatus = true;
                    string sql = "";
                    //胜途调拨单号
                    string bil_no = array[i]["bil_no"].ToString();
                    //调拨单id
                    string id = array[i]["id"].ToString();
                    //获取仓库不是本地仓库内部调拨就要生成
                    DataRow[] rowCK_in = CKdt.Select("F_ZWLF_WAREHOUSECODE='" + array[i]["wh_code_in"].ToString() + "'");
                    if (rowCK_in.Length > 0)
                    {
                        foreach (DataRow dr in rowCK_in)
                        {
                            //获取仓库
                            InFstockNumber = dr["FNUMBER"].ToString();
                            InFStockOrgId = dr["FUSEORGID"].ToString();
                            
                        }
                    }
                    else
                    {
                        checkMsg += "找不到对应仓库信息";
                        checkstatus = false;
                    }
                    //获取调出的仓库
                    DataRow[] rowCK_out = CKdt.Select("F_ZWLF_WAREHOUSECODE='" + array[i]["wh_code_out"].ToString() + "'");
                    if (rowCK_out.Length > 0)
                    {
                        foreach (DataRow dr in rowCK_out)
                        {
                            //获取仓库
                            OutFstockNumber = dr["FNUMBER"].ToString();
                            OutFStockOrgId = dr["FUSEORGID"].ToString();
                        }
                    }
                    else
                    {
                        checkMsg += "找不到对应仓库信息";
                        checkstatus = false;
                    }
                    //非仓库内调拨才同步100027 
                    if (checkstatus)
                    {
                        //不是本地仓库调出的
                        if (OutFStockOrgId !="100027" && OutFStockOrgId != "165222")
                        {
                            if (OutFstockNumber != InFstockNumber && OutFStockOrgId == InFStockOrgId)
                            {
                                int num = bil_no.Split('-').Length - 1;
                                string Sourcebil_no = bil_no; //源单调出出库单
                                if (num >= 3)
                                {
                                    string[] sArray = bil_no.Split('-');
                                    Sourcebil_no = sArray[0] + "-" + sArray[1] + "-" + sArray[2];
                                }
                                //调出仓库不是本地的才需要生成
                                DataRow[] row_out = dt_out.Select("F_ZWLF_TransfersNo='" + Sourcebil_no + "'");
                                //未入库才可以生成
                                DataRow[] row_In = dt_In.Select("F_ZWLF_Id='" + id + "' and F_ZWLF_TransfersNo='" + Sourcebil_no + "'");
                                if (row_out.Length > 0)
                                {
                                    string FID = "";
                                    foreach (var dr in row_out)
                                    {
                                        FID = dr["FID"].ToString();
                                    }
                                    if (row_In.Length == 0)
                                    {
                                        List<Orders> orderList = new List<Orders>();
                                        JArray details = array[i]["details"] as JArray;
                                        for (int j = 0; j < details.Count; j++)
                                        {
                                            if (Convert.ToInt32(details[j]["qty_in"].ToString()) > 0)
                                            {
                                                Orders order = new Orders();
                                                order.prdt_code = details[j]["prdt_code"].ToString();
                                                order.qty = Convert.ToInt32(details[j]["qty_in"].ToString());
                                                order.back_time = array[i]["chk_date_in"].ToString();
                                                order.order_id = bil_no;
                                                order.id = id;
                                                orderList.Add(order);
                                            }
                                        }
                                        if (orderList.Count > 0)
                                        {
                                            //调拨出库下推调拨入库
                                            Msg re = TranferOutPush(FID, context, orderList, dt_wl);
                                            if (!re.status)
                                            {
                                                //记录到日志里面
                                                msg.result = "下推分布式调入单失败";
                                                sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_TRANFERINSTATE='0',F_ZWLF_NOTE='{0}'
                                                     where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                                msg.result, array[i]["id"].ToString(), array[i]["bil_no"].ToString());
                                                DBServiceHelper.Execute(context, sql);
                                            }
                                            else
                                            {
                                                string mssg = "下推分布式调入单成功";
                                                sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_TRANFERSINNO='{3}', F_ZWLF_TRANFERINSTATE='1',F_ZWLF_NOTE='{0}' 
                                                              where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                      mssg, array[i]["id"].ToString(), array[i]["bil_no"].ToString(), re.result);
                                                DBServiceHelper.Execute(context, sql);
                                                msg.result = mssg;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    //日志记录
                                    msg.result = "找不到对应的分布式调出单";
                                    string Insql = string.Format(@"/*dialect*/ INSERT INTO ZWLF_T_OrderLog
                                                ([FID]
                                                ,[FBILLNO]
                                                ,[F_ZWLF_ID]
                                                ,[F_ZWLF_SITE_TRADE_ID]
                                                ,[F_ZWLF_TIME]
                                                ,F_ZWLF_NOTE 
                                                ,F_ZWLF_SOURCETYPE,F_ZWLF_TranferInState)
                                              VALUES
                                               ((select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                               ,(select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                               ,'{0}'
                                               ,'{1}'
                                               ,GETDATE(),'{2}','02','0')", array[i]["id"].ToString(), array[i]["bil_no"].ToString(), msg.result);
                                    DBServiceHelper.Execute(context, Insql);
                                }
                            }
                        }
                    }
                    else
                    {
                        string Insql = string.Format(@"/*dialect*/ INSERT INTO ZWLF_T_OrderLog
                                                ([FID]
                                                ,[FBILLNO]
                                                ,[F_ZWLF_ID]
                                                ,[F_ZWLF_SITE_TRADE_ID]
                                                ,[F_ZWLF_TIME]
                                                ,F_ZWLF_NOTE 
                                                ,F_ZWLF_SOURCETYPE,F_ZWLF_TranferInState,F_ZWLF_IsthirdStock)
                                              VALUES
                                               ((select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                               ,(select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                               ,'{0}'
                                               ,'{1}'
                                               ,GETDATE(),'{2}','02','0','1')", array[i]["id"].ToString(), array[i]["bil_no"].ToString(), checkMsg);
                        DBServiceHelper.Execute(context, Insql);
                    }
                }
                catch (KDException ex)
                {
                    //把前订单记录到日志里面
                    msg.result = ex.ToString().Substring(0, 500).Replace("'", "");
                    string Insql = string.Format(@"/*dialect*/ INSERT INTO ZWLF_T_OrderLog
                                                ([FID]
                                                ,[FBILLNO]
                                                ,[F_ZWLF_ID]
                                                ,[F_ZWLF_SITE_TRADE_ID]
                                                ,[F_ZWLF_TIME]
                                                ,F_ZWLF_NOTE 
                                                ,F_ZWLF_SOURCETYPE ,F_ZWLF_TranferInState,F_ZWLF_IsthirdStock)
                                              VALUES
                                               ((select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                               ,(select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                               ,'{0}'
                                               ,'{1}'
                                               ,GETDATE(),'{2}','02','0','1')", array[i]["id"].ToString(), array[i]["bil_no"].ToString(), msg.result);
                    DBServiceHelper.Execute(context, Insql);
                }
            }
            #endregion
            msg.status = true;
            msg.sum = array.Count;
            return msg;
        }


        /// <summary>
        /// 下推分步式调入单
        /// </summary>
        /// <param name="FID"></param>
        /// <param name="orderList"></param>
        /// <param name="contex"></param>
        /// <returns></returns>
        private Msg TranferOutPush(string FID, Context context, List<Orders> orderList,DataTable dt_wl)
        {
            Msg msg = new Msg();
            try
            {
                //单据转换
                string srcFormId = "STK_TRANSFEROUT"; //分布式调出单
                string destFormId = "STK_TRANSFERIN"; //分布式调入单
                IMetaDataService mService = Kingdee.BOS.App.ServiceHelper.GetService<IMetaDataService>();
                IViewService vService = Kingdee.BOS.App.ServiceHelper.GetService<IViewService>();
                FormMetadata destmeta = mService.Load(context, destFormId) as FormMetadata;
                //转换规则的唯一标识
                //string ruleKey = "OutStock_InStock";
                var rules = ConvertServiceHelper.GetConvertRules(context, srcFormId, destFormId);
                var rule = rules.FirstOrDefault(t => t.IsDefault);
                // ConvertRuleElement rule = GetDefaultConvertRule(ctx, srcFormId, destFormId, ruleKey);
                List<ListSelectedRow> lstRows = new List<ListSelectedRow>();
                string sql = string.Format(@"/*dialect*/ select a.FID,b.FENTRYID  from T_STK_STKTRANSFEROUT a
                                   inner join T_STK_STKTRANSFEROUTENTRY b on a.FID=b.FID where a.FID='{0}'", FID);
                DataSet ds_tr = DBServiceHelper.ExecuteDataSet(context, sql);
                for (int j = 0; j < ds_tr.Tables[0].Rows.Count; j++)
                {
                    long entryId = Convert.ToInt64(ds_tr.Tables[0].Rows[j]["FENTRYID"]);
                    string SoFID = ds_tr.Tables[0].Rows[j]["FID"].ToString();
                    //单据标识
                    ListSelectedRow row = new ListSelectedRow(SoFID, entryId.ToString(), 0, "STK_TRANSFEROUT");
                    //源单单据体标识
                    row.EntryEntityKey = "FSTKTRSOUTENTRY";
                    lstRows.Add(row);
                }
                PushArgs pargs = new PushArgs(rule, lstRows.ToArray());
                IConvertService cvtService = Kingdee.BOS.App.ServiceHelper.GetService<IConvertService>();
                OperateOption option = OperateOption.Create();
                option.SetIgnoreWarning(true);
                option.SetVariableValue("ignoreTransaction", false);
                option.SetIgnoreInteractionFlag(true);
                ConvertOperationResult cvtResult = cvtService.Push(context, pargs, option, false);
                if (cvtResult.IsSuccess)
                {
                    string mssg = "";
                    string Fdate = DateTime.Now.ToString();
                    string bil_no = "";
                    string F_ZWLF_Id = "";
                    List<int> listRE = new List<int>();
                    DynamicObject[] dylist = (from p in cvtResult.TargetDataEntities select p.DataEntity).ToArray();
                    //生成调入单
                    for (int K = 0; K < dylist.Length; K++)
                    {
                        //明细信息
                        DynamicObjectCollection EntryList = dylist[K]["STK_STKTRANSFERINENTRY"] as DynamicObjectCollection;
                        for (int a = EntryList.Count; a > 0; a--)
                        {
                            string FMATERIALID = EntryList[a - 1]["MaterialID_Id"].ToString();
                            //获取物料编码
                            DataRow[] Wlrow = dt_wl.Select("FMATERIALID='" + FMATERIALID + "'");
                            string fnumber = "";
                            if (Wlrow.Length > 0)
                            {
                                foreach (DataRow dr in Wlrow)
                                {
                                    fnumber = dr["FNUMBER"].ToString();
                                }
                            }
                            var list = orderList.Where(x => x.prdt_code == fnumber).ToList();
                            if (list.Count == 0)
                            {
                                EntryList.RemoveAt(a - 1);
                            }
                            else
                            {
                                bil_no = list[0].order_id;
                                F_ZWLF_Id = list[0].id;
                                int qty = list[0].qty;
                                Fdate = list[0].back_time;
                                //调入数量
                                EntryList[a - 1]["FQty"] = qty;
                                ////调入数量(库存辅单位)
                                //Entry["SecQty"] = qty;
                                //基本单位调入数量
                                EntryList[a - 1]["TranferQty"] = qty;
                                //调入数量
                                EntryList[a - 1]["BaseTransferQty"] = qty;
                                //计划入库数量
                                EntryList[a - 1]["PlanTransferQty"] = qty;
                                //计划入库数量
                                EntryList[a - 1]["BasePlanTransQty"] = qty;
                                // 基本数量
                                EntryList[a - 1]["BaseQty"] = qty; 

                            }
                        }
                        //单据日期
                        dylist[K]["Date"] = Fdate;
                        //调调单号
                        dylist[K]["F_ZWLF_TransfersNo"] = bil_no;
                        dylist[K]["F_ZWLF_Id"] = F_ZWLF_Id;
                        if (EntryList.Count > 0)
                        {
                            //保存提交审核
                            IOperationResult result = Operation(dylist, destmeta, context);
                            if (!result.IsSuccess)
                            {
                                if (!result.IsSuccess)
                                {
                                    foreach (var item in result.ValidationErrors)
                                    {
                                        mssg = mssg + item.Message;
                                    }
                                    if (!result.InteractionContext.IsNullOrEmpty())
                                    {
                                        mssg = mssg + result.InteractionContext.SimpleMessage;
                                    }
                                }
                                msg.result = mssg;
                                msg.status = false;
                            }
                            else
                            {
                                OperateResultCollection operateResults = result.OperateResult;
                                msg.result = operateResults[0].Number;
                                msg.status = true;
                            }
                        }
                        else
                        {
                            msg.result = "该调拨单已经下推";
                            msg.status = false;
                        }
                    }
                }
            }
            catch (KDException ex)
            {
                msg.status = false;
                msg.result = "生成分布式调入单失败:" + ex.ToString().Substring(0, 300);
            }
            return msg;
        }
        /// <summary>
        ///保存
        /// </summary>
        /// <param name="newdlm"></param>
        /// <param name="materialmeta"></param>
        /// <returns></returns>
        private IOperationResult Operation(DynamicObject[] dylist, FormMetadata materialmeta, Context context)
        {
            OperateOption option = OperateOption.Create();
            option.SetIgnoreWarning(true);
            option.SetVariableValue("ignoreTransaction", true);
            //保存
            ISaveService saveService = Kingdee.BOS.App.ServiceHelper.GetService<ISaveService>();
            IOperationResult saveresult = saveService.Save(context, materialmeta.BusinessInfo, dylist, option);
            //提交
            object[] items = dylist.Select(p => p["Id"]).ToArray();
            ISubmitService submitService = Kingdee.BOS.App.ServiceHelper.GetService<ISubmitService>();
            IOperationResult submitresult = submitService.Submit(context, materialmeta.BusinessInfo, items, "Submit", option);
            //审核
            IAuditService auditService = Kingdee.BOS.App.ServiceHelper.GetService<IAuditService>();
            IOperationResult auditresult = auditService.Audit(context, materialmeta.BusinessInfo, items, option);
            return auditresult;
        }

    }
}
