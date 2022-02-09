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
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;

namespace AnyCubicK3cloudProject
{
    public class ZWLF_GetTransfer
    {
        /// <summary>
        /// 调用胜途调拨出库的(本地仓调出到海外仓库)
        /// </summary>
        /// <param name="app_key"></param>
        /// <param name="AppSecret"></param>
        /// <param name="page_size"></param>
        /// <param name="page_no"></param>
        /// <returns></returns>
        public Msg GetTransfer(Parameters param, Context context)
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
                        //获取所有直接调拨（本地仓调到海外仓库）
                        string sql = string.Format(@"/*dialect*/ select F_ZWLF_Id , F_ZWLF_TransfersNo  from  T_STK_STKTRANSFERIN
                                                 where F_ZWLF_Id !='' and  F_ZWLF_TransfersNo !='' and FTRANSFERBIZTYPE='InnerOrgTransfer' and FBILLTYPEID='ce8f49055c5c4782b65463a3f863bb4a';");
                        //仓库映射本地仓库
                        sql += string.Format(@"/*dialect*/ select  FNUMBER,F_ZWLF_WAREHOUSECODE,FUSEORGID from  ZWLF_t_Cust_StockEntry a 
                                           inner join t_BD_Stock b on a.FStockId=b.FSTOCKID where F_ZWLF_DISABLE=0 ;");
                        DataSet ds = DBServiceHelper.ExecuteDataSet(context, sql);
                        //生成分布式调拨出库单 STK_TRANSFEROUT
                        msg = GenerateK3Transfers(OrderJson, ds, context);
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
        /// 生成金蝶分布式调拨出库单
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public Msg GenerateK3Transfers(JObject json, DataSet ds, Context context)
        {
            Msg msg = new Msg();
            DataTable Trdt = ds.Tables[0];
            DataTable CKdt = ds.Tables[1];
            int total_count = Convert.ToInt32(json["data"]["total_count"].ToString()); //本次返回的订单数
            JArray array = json["data"]["data"] as JArray;
            #region 循环生成调拨出库单
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
                    //校验仓库信息
                    if (checkstatus)
                    {
                        //从本地仓库调出
                        if (OutFstockNumber == "CK002" || OutFstockNumber == "CK026" || OutFstockNumber == "CK016"|| OutFstockNumber == "CK011")
                        {
                            //仓库内部调拨不需要生成直接调拨单
                            if (InFstockNumber != OutFstockNumber)
                            {
                                //胜途订单id
                                string F_ZWLF_id = array[i]["id"].ToString();
                                //调拨单号 bil_no
                                string bil_no = array[i]["bil_no"].ToString();
                                DataRow[] OrderRow = Trdt.Select("F_ZWLF_ID='" + F_ZWLF_id + "' and F_ZWLF_TransfersNo='" + bil_no + "'");
                                //当前ERP不存在才会新增
                                if (OrderRow.Length == 0)
                                {
                                    //把前订单记录到日志里面
                                    string sql = string.Format(@"/*dialect*/ INSERT INTO ZWLF_T_OrderLog
                                                      ([FID]
                                                      ,[FBILLNO]
                                                      ,[F_ZWLF_ID]
                                                      ,[F_ZWLF_TIME]
                                                      ,F_ZWLF_SOURCETYPE
                                                      ,F_ZWLF_SITE_TRADE_ID)
                                                    VALUES
                                                     ((select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                                     ,(select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                                     ,'{0}'
                                                     ,GETDATE(),'02','{1}')", array[i]["id"].ToString(), array[i]["bil_no"].ToString());
                                    DBServiceHelper.Execute(context, sql);
                                    #region 生成直接调拨单
                                    FormMetadata meta = MetaDataServiceHelper.Load(context, "STK_TransferDirect") as FormMetadata;
                                    BusinessInfo info = meta.BusinessInfo;
                                    IResourceServiceProvider formServiceProvider = meta.BusinessInfo.GetForm().GetFormServiceProvider(true);
                                    IBillViewService billViewService = formServiceProvider.GetService(typeof(IDynamicFormView)) as IBillViewService;
                                    /******创建单据打开参数*************/
                                    Form form = meta.BusinessInfo.GetForm();
                                    BillOpenParameter billOpenParameter = new BillOpenParameter(form.Id, meta.GetLayoutInfo().Id);
                                    billOpenParameter = new BillOpenParameter(form.Id, string.Empty);
                                    billOpenParameter.Context = context;
                                    billOpenParameter.ServiceName = form.FormServiceName;
                                    billOpenParameter.PageId = Guid.NewGuid().ToString();
                                    billOpenParameter.FormMetaData = meta;
                                    billOpenParameter.LayoutId = meta.GetLayoutInfo().Id;
                                    billOpenParameter.Status = OperationStatus.ADDNEW;
                                    billOpenParameter.PkValue = null;
                                    billOpenParameter.CreateFrom = CreateFrom.Default;
                                    billOpenParameter.ParentId = 0;
                                    billOpenParameter.GroupId = "";
                                    billOpenParameter.DefaultBillTypeId = null;
                                    billOpenParameter.DefaultBusinessFlowId = null;
                                    billOpenParameter.SetCustomParameter("ShowConfirmDialogWhenChangeOrg", false);
                                    List<AbstractDynamicFormPlugIn> value = form.CreateFormPlugIns();
                                    billOpenParameter.SetCustomParameter(FormConst.PlugIns, value);
                                    ((IDynamicFormViewService)billViewService).Initialize(billOpenParameter, formServiceProvider);
                                    IBillView bill_view = (IBillView)billViewService;
                                    bill_view.CreateNewModelData();
                                    DynamicFormViewPlugInProxy proxy = bill_view.GetService<DynamicFormViewPlugInProxy>();
                                    proxy.FireOnLoad();
                                    //表头
                                    #region  表头
                                    //调出组织
                                    bill_view.Model.SetItemValueByID("FStockOutOrgId", OutFStockOrgId, 0);
                                    //调拨类型 
                                    bill_view.Model.SetValue("FTransferBizType", "InnerOrgTransfer", 0);
                                    bill_view.InvokeFieldUpdateService("FTransferBizType", 0);
                                    //调入组织
                                    bill_view.Model.SetItemValueByID("FStockOrgId", OutFStockOrgId, 0);
                                    //日期 
                                    bill_view.Model.SetItemValueByID("FDate", array[i]["chk_date_out"].ToString(), 0);
                                    //胜途调拨单唯一标识id
                                    bill_view.Model.SetValue("F_ZWLF_Id", F_ZWLF_id, 0);
                                    //胜途调拨单单号
                                    bill_view.Model.SetValue("F_ZWLF_TransfersNo", bil_no, 0);
                                    //推送状态
                                    bill_view.Model.SetValue("F_ZWLF_PushState", "1", 0);
                                    #endregion
                                    #region  表体
                                    //表体（一个订单可能又多个物料）
                                    JArray details = array[i]["details"] as JArray;
                                    for (int j = 0; j < details.Count; j++)
                                    {
                                        //获取库存仓位
                                        string csql = string.Format(@"select top 1  FBASEQTY,FSTOCKLOCID,c.FNUMBER,isnull(d.FNUMBER,0) as PLFnumber from T_STK_INVENTORY  a  
                                                                      inner  join  T_BD_stock b on a.FSTOCKID=b.FSTOCKID 
                                                                      inner  join   T_BD_MATERIAL  w on w.FMATERIALID=a.FMATERIALID
                                                                      left  join T_BAS_FlexValuesDetail loc on a.FSTOCKLOCID = loc.FId
                                                                      left join t_BAS_FlexValuesEntry c on c.FENTRYID=loc.FF100004
                                                                      left join T_BD_LOTMASTER d on d.FLOTID=a.FLOT
                                                                      where w.FNUMBER='{0}' and b.FNUMBER='{1}' and FBASEQTY>0 and FSTOCKORGID='{2}' order by FBASEQTY desc ",
                                                                              details[j]["prdt_code"].ToString(), OutFstockNumber, OutFStockOrgId);
                                        DataSet ds_loc = DBServiceHelper.ExecuteDataSet(context, csql);
                                        DataTable dt_loc = ds_loc.Tables[0];
                                        if (dt_loc.Rows.Count > 0)
                                        {
                                            for (int b = 0; b < dt_loc.Rows.Count; b++)
                                            {
                                                //仓位
                                                long FSTOCKLOCID = Convert.ToInt64(dt_loc.Rows[b]["FSTOCKLOCID"]);
                                                //仓位编码
                                                string FLOCFnumber = dt_loc.Rows[b]["FNUMBER"].ToString();
                                                //批号
                                                string PLFnumber = dt_loc.Rows[b]["PLFnumber"].ToString();
                                                //新增一行
                                                bill_view.Model.CreateNewEntryRow("FBillEntry");
                                                bill_view.Model.SetItemValueByNumber("FMaterialId", details[j]["prdt_code"].ToString(), j);
                                                bill_view.InvokeFieldUpdateService("FMaterialId", j);
                                                bill_view.InvokeFieldUpdateService("FUnitID", j);
                                                bill_view.InvokeFieldUpdateService("FBaseUnitID", j);
                                                //调出仓库 
                                                bill_view.Model.SetItemValueByNumber("FSrcStockId", OutFstockNumber, j);
                                                bill_view.InvokeFieldUpdateService("FSrcStockId", j);
                                                //仓位
                                                bill_view.Model.SetItemValueByID("FSrcStockLocId", FSTOCKLOCID, j);
                                                bill_view.Model.SetItemValueByNumber("$$FSrcStockLocId__FF100001", FLOCFnumber, j);
                                                //批号
                                                if (PLFnumber != "0")
                                                {
                                                    bill_view.Model.SetItemValueByNumber("FLOT", PLFnumber, j);
                                                    bill_view.InvokeFieldUpdateService("FLOT", j);
                                                }
                                                //调入海外仓的默认入到发出商品仓
                                                if (InFStockOrgId == "165223")
                                                {
                                                    //调入仓库 
                                                    bill_view.Model.SetItemValueByNumber("FDestStockId", "CK505", j);
                                                    bill_view.InvokeFieldUpdateService("FDestStockId", j);
                                                }
                                                else
                                                {
                                                    //调入仓库 
                                                    bill_view.Model.SetItemValueByNumber("FDestStockId", InFstockNumber, j);
                                                    bill_view.InvokeFieldUpdateService("FDestStockId", j);
                                                    if (InFStockOrgId == "100027")
                                                    {
                                                        if (InFstockNumber == "CK026")//售后补发仓
                                                        {
                                                            bill_view.Model.SetItemValueByID("FDestStockLocId", 104585, j);
                                                            bill_view.Model.SetItemValueByNumber("$$FDestStockLocId__FF100001", "中转1-1", j);
                                                        }
                                                        if (InFstockNumber == "CK002")//成品仓
                                                        {
                                                            bill_view.Model.SetItemValueByID("FDestStockLocId", 104602, j);
                                                            bill_view.Model.SetItemValueByNumber("$$FDestStockLocId__FF100001", "成品仓区", j);
                                                        }
                                                    }
                                                    //else
                                                    //{
                                                    //    if (InFstockNumber != "CK016")
                                                    //    {
                                                    //        //仓位
                                                    //        bill_view.Model.SetItemValueByID("FDestStockLocId", 103753, j);
                                                    //        bill_view.Model.SetItemValueByNumber("$$FDestStockLocId__FF100001", "半成品仓位", j);
                                                    //    }
                                                    //}
                                                }
                                                //数量
                                                bill_view.Model.SetValue("FQty", details[j]["qty_out"].ToString(), j);
                                                bill_view.InvokeFieldUpdateService("FQty", j);
                                            }
                                        }
                                        else
                                        {
                                            checkMsg += "物料编码：" + details[j]["prdt_code"].ToString() + "库存不足";
                                            checkstatus = false;
                                            break;
                                        }
                                    }

                                    #endregion
                                    if (checkstatus)
                                    {
                                        //保存
                                        IOperationResult save_result = bill_view.Model.Save();
                                        if (save_result.IsSuccess)
                                        {
                                            string mssg = "";
                                            string fid = string.Empty;
                                            string F_ZWLF_Fbillno = string.Empty;
                                            OperateResultCollection Collection = save_result.OperateResult;
                                            foreach (var item in Collection)
                                            {
                                                fid = item.PKValue.ToString();
                                                F_ZWLF_Fbillno = item.Number.ToString();
                                            }
                                            try
                                            {
                                                //提交审核订单
                                                IOperationResult result = Operation(fid, meta, context);
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
                                                    sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_TranferState='0',F_ZWLF_NOTE='{0}' 
                                                                where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}' ;",
                                                                            mssg, array[i]["id"].ToString(), array[i]["bil_no"].ToString());
                                                }
                                                else
                                                {
                                                    //记录生成直接调拨单成功
                                                    mssg = "生成直接调拨单成功";
                                                    sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_TranferState='1',F_ZWLF_TranferSNo='{0}',F_ZWLF_NOTE='{3}' 
                                                                   where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                                       F_ZWLF_Fbillno, array[i]["id"].ToString(), array[i]["bil_no"].ToString(), mssg);
                                                    DBServiceHelper.Execute(context, sql);
                                                }
                                                msg.result = mssg;
                                                DBServiceHelper.Execute(context, sql);
                                            }
                                            catch (KDException ex)
                                            {
                                                msg.result = ex.ToString().Substring(0, 500);
                                                sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_TranferState='0',F_ZWLF_NOTE='{0}' 
                                                                      where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                                  msg.result, array[i]["id"].ToString(), array[i]["bil_no"].ToString());
                                                DBServiceHelper.Execute(context, sql);
                                            }
                                        }
                                        else
                                        {
                                            string mssg = "";
                                            foreach (var item in save_result.ValidationErrors)
                                            {
                                                mssg = mssg + item.Message;
                                            }
                                            if (!save_result.InteractionContext.IsNullOrEmpty())
                                            {
                                                mssg = mssg + save_result.InteractionContext.SimpleMessage;
                                            }
                                            sql += string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_TranferState='0',F_ZWLF_NOTE='{0}'
                                                     where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                                 mssg, array[i]["id"].ToString(), array[i]["bil_no"].ToString());
                                            DBServiceHelper.Execute(context, sql);
                                            msg.result = mssg;
                                        }
                                    }
                                    else
                                    {
                                        sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_TranferState='0',F_ZWLF_NOTE='{0}' 
                                                              where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                   checkMsg, array[i]["id"].ToString(), array[i]["bil_no"].ToString());
                                        DBServiceHelper.Execute(context, sql);
                                    }
                                    #endregion
                                }
                            }
                        }
                    }
                    else
                    {
                        string sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_TranferState='0',F_ZWLF_NOTE='{0}' 
                                                              where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                               checkMsg, array[i]["id"].ToString(), array[i]["bil_no"].ToString());
                        DBServiceHelper.Execute(context, sql);
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
                                                ,F_ZWLF_SOURCETYPE ,F_ZWLF_TranferState)
                                              VALUES
                                               ((select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                               ,(select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                               ,'{0}'
                                               ,'{1}'
                                               ,GETDATE(),'{2}','02','0')", array[i]["id"].ToString(), array[i]["bil_no"].ToString(), msg.result);
                    DBServiceHelper.Execute(context, Insql);
                }
            }
            #endregion
            msg.status = true;
            msg.sum = array.Count;
            return msg;
        }

        /// <summary>
        /// 提交审核
        /// </summary>
        /// <param name="FID"></param>
        /// <param name="materialmeta"></param>
        /// <returns></returns>
        private IOperationResult Operation(string FID, FormMetadata materialmeta, Context context)
        {
            OperateOption option = OperateOption.Create();
            option.SetIgnoreWarning(false);
            option.SetVariableValue("ignoreTransaction", false);
            IOperationResult result = null;
            //提交
            object[] items = { FID };
            ISubmitService submitService = Kingdee.BOS.App.ServiceHelper.GetService<ISubmitService>();
            result = submitService.Submit(context, materialmeta.BusinessInfo, items, "Submit", option);
            //审核
            IAuditService auditService = Kingdee.BOS.App.ServiceHelper.GetService<IAuditService>();
            result = auditService.Audit(context, materialmeta.BusinessInfo, items, option);
            return result;
        }
    }
}
