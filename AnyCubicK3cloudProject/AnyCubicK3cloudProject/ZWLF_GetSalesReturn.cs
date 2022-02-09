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
    public class ZWLF_GetSalesReturn
    {
        /// <summary>
        /// 获取销售退货单数据
        /// </summary>
        /// <param name="param"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Msg SaleReturnGet(Parameters param, Context context)
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
                @params["id"] = param.id; //售后退单唯一标识，默认为空，
                @params["site_id"] = "";// 店铺ID
                @params["wh_code"] = param.wh_code; //仓库字段
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
                        //销售订单
                        string sql = string.Format(@"/*dialect*/  select  Fbillno,F_ZWLF_ID,F_ZWLF_sal_bilno,F_ZWLF_SITE_TRADE_ID 
                                                        from T_SAL_ORDER where  FDOCUMENTSTATUS='C' and F_ZWLF_ID!=''");
                        //币别
                        sql += string.Format(@"/*dialect*/ select  FNUMBER,FCODE from  T_BD_CURRENCY ;");
                        //仓库映射
                        sql += string.Format(@"/*dialect*/ select a.FStockId, FNUMBER,F_ZWLF_WAREHOUSECODE,FUSEORGID from  ZWLF_t_Cust_StockEntry a 
                                           inner join t_BD_Stock b on a.FStockId=b.FSTOCKID where F_ZWLF_DISABLE=0 ;");
                        //税率表
                        sql += string.Format(@"/*dialect*/  select F_ZWLF_STORETYPE ,F_ZWLF_COUNTRYNAME,F_ZWLF_COUNTRIES,F_ZWLF_TAXRATE 
                                               from ZWLF_t_PlatformTaxRate where  FFORBIDSTATUS='A' and FDOCUMENTSTATUS='C' ;");
                        //获取网店对应客户
                        sql += string.Format(@"/*dialect*/ select a.FID, F_ZWLF_DEPARTMENT, c.fnumber as F_ZWLF_CUSTOMER,F_ZWLF_ONLINEACCOUNT,d.FNUMBER as F_ZWLF_SALERID,
                                                 FNAME,F_ZWLF_ISDomesticStore,F_ZWLF_STORETYPE ,F_ZWLF_ORGANIZATION,F_ZWLF_TAXRATE from  ZWLF_t_OnlineStore a 
                                                 inner  join ZWLF_t_OnlineStore_L  b on a.FID=b.FID 
                                                 inner join T_BD_CUSTOMER c on c.FCUSTID=a.F_ZWLF_CUSTOMER
                                                 inner join V_BD_SALESMAN d on d.fid=a.F_ZWLF_SALERID
                                                 where  a.FFORBIDSTATUS='A' and a.FDOCUMENTSTATUS='C';");
                        sql += string.Format(@"/*dialect*/ select  FID,F_ZWLF_SAL_BILNO,F_ZWLF_ID  from T_STK_MISCELLANEOUS where F_ZWLF_SAL_BILNO!='' and F_ZWLF_ID!=''");
                        //物料表		
                        sql += string.Format(@"/*dialect*/ select a.FNUMBER ,a.FOLDNUMBER ,FISBATCHMANAGE from T_BD_MATERIAL a
                                                           inner join t_BD_MaterialStock b on a.FMATERIALID=b.FMATERIALID
                                                           where a.FUSEORGID='1'  ");
                        DataSet ds = DBServiceHelper.ExecuteDataSet(context, sql);
                        //下推退货单
                        msg = PushSalseReturn(OrderJson, context, ds, param);
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
        /// 销售订单下推销售退货单
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public Msg PushSalseReturn(JObject json, Context context, DataSet ds_s, Parameters param)
        {
            Msg msg = new Msg();
            DataTable Sodt = ds_s.Tables[0];
            DataTable Bbdt = ds_s.Tables[1];
            DataTable CKdt = ds_s.Tables[2];
            DataTable Sldt = ds_s.Tables[3];
            DataTable WDdt = ds_s.Tables[4];
            DataTable QTdt = ds_s.Tables[5];
            DataTable WLdt = ds_s.Tables[6];
            int total_count = Convert.ToInt32(json["data"]["total_count"].ToString()); //本次返回的订单数
            JArray array = json["data"]["data"] as JArray;
            #region 循环下推退货单
            for (int i = 0; i < array.Count; i++)
            {
                //胜途订单id
                string sql = "";
                string Fbillno = "";
                string id = ""; //售后退单唯一标识，
                string bil_no = ""; //售后登记ID
                string F_ZWLF_SOURCETYPE = "04";
                //客户编码
                string FCUSTIDFNumer = "";
                string F_ZWLF_SALERID = "";
                string F_ZWLF_site_name = "";
                string F_ZWLF_DEPARTMENT = "";
                bool checkstatus = true;
                string checkMsg = "";
                //销售组织
                string FSaleOrgId = "";
                //税率
                decimal F_ZWLF_TAXRATE = 0;
                //库存组织
                string FStockOrgId = "";
                //是否国内网店
                string F_ZWLF_ISDomesticStore = "0";
                //其他入库的库存组织
                string QFStockOrgId = "";
                //币别
                string cur_code = "";
                //仓库编码
                string FstockNumber = "";
                //仓库id
                long FStockId = 0;
                try
                {
                    string back_time = array[i]["back_time"].ToString();
                    id = array[i]["id"].ToString();
                    bil_no = array[i]["bil_no"].ToString();
                    //网上订单号
                    string site_trade_id = "";
                    if (array[i].ToString().Contains("site_trade_id"))
                    {
                        site_trade_id = array[i]["site_trade_id"].ToString();
                    }
                    string back_wh_code = array[i]["back_wh_code"].ToString();
                    //平台账号
                    string site_type_id = "";
                    if (array[i].ToString().Contains("site_type_id"))
                    {
                        site_type_id = array[i]["site_type_id"].ToString();
                    }
                    //网店账号
                    string site_user = "";
                    if (array[i].ToString().Contains("site_user"))
                    {
                        site_user = array[i]["site_user"].ToString();
                    }
                    DataRow[] SoTHRow = Sodt.Select("F_ZWLF_ID='" + id + "' and F_ZWLF_sal_bilno='" + bil_no + "'");
                    //判断是否生成其他入库
                    DataRow[] QTRow = QTdt.Select("F_ZWLF_ID='" + id + "' and F_ZWLF_sal_bilno='" + bil_no + "'");
                    //获取仓库
                    DataRow[] rowCK = CKdt.Select("F_ZWLF_WAREHOUSECODE='" + array[i]["back_wh_code"].ToString() + "'");
                    if (rowCK.Length > 0)
                    {
                        foreach (DataRow dr in rowCK)
                        {
                            //获取仓库
                            FstockNumber = dr["FNUMBER"].ToString();
                            FStockOrgId = dr["FUSEORGID"].ToString();
                            FStockId = Convert.ToInt64(dr["FStockId"].ToString());
                            QFStockOrgId = dr["FUSEORGID"].ToString();
                        }
                    }
                    else
                    {
                        checkMsg += "找不到对应仓库信息";
                        checkstatus = false;
                    }
                    //获取网店客户
                    //DataRow[] rowWd = Bbdt.Select("FCODE='" + array[i]["cur_code"].ToString() + "'");
                    //通过网店账号和平台类型对应客户ID
                    DataRow[] rows = WDdt.Select("F_ZWLF_ONLINEACCOUNT='" + site_user + "' and F_ZWLF_STORETYPE='" + site_type_id + "' ");
                    //获取客户网店
                    if (rows.Length > 0)
                    {
                        foreach (DataRow drN in rows)
                        {
                            FCUSTIDFNumer = drN["F_ZWLF_CUSTOMER"].ToString(); //获取对照表的客户
                            F_ZWLF_SALERID = drN["F_ZWLF_SALERID"].ToString();//获取销售员
                            F_ZWLF_site_name = drN["FID"].ToString();//获取网店
                            F_ZWLF_DEPARTMENT = drN["F_ZWLF_DEPARTMENT"].ToString();//部门
                            F_ZWLF_ISDomesticStore = drN["F_ZWLF_ISDomesticStore"].ToString();//是否国内网店
                            FSaleOrgId = drN["F_ZWLF_ORGANIZATION"].ToString(); //销售组织
                            F_ZWLF_TAXRATE = Convert.ToDecimal(drN["F_ZWLF_TAXRATE"].ToString());//税率
                        }
                    }
                    else
                    {
                        checkMsg += "找不到对应网店信息";
                        checkstatus = false;
                    }
                    //获取币别
                    if (array[i].ToString().Contains("cur_code"))
                    {
                        DataRow[] rowCY = Bbdt.Select("FCODE='" + array[i]["cur_code"].ToString() + "'");
                        if (rowCY.Length > 0)
                        {
                            foreach (DataRow dr in rowCY)
                            {
                                cur_code = dr["FNUMBER"].ToString(); //获取币别
                            }
                        }
                        else
                        {
                            checkMsg += "找不到对应币别信息";
                            checkstatus = false;
                        }
                    }
                    else
                    {
                        cur_code = "PRE001";
                    }
                    
                    //国内订单的税率,其他币别为0
                    if (cur_code == "PRE001")
                    {
                        F_ZWLF_TAXRATE = 13;
                    }
                    if (param.method == "frdas.erp.refunds.getcloud")
                    {
                        F_ZWLF_SOURCETYPE = "07";
                    }
                    //只有阿里国际
                    if (array[i]["site_name"].ToString().Contains("阿里") && array[i]["site_name"].ToString() != "阿里德瑞")
                    {
                        if (FStockOrgId == "100027")
                        {
                            FSaleOrgId = "100027";
                        }
                        else
                        {
                            FSaleOrgId = "165223";
                        }
                    }
                    //海外仓发货的库存组织和销售组织都是香港
                    if (FStockOrgId == "165223")
                    {
                        FSaleOrgId = "165223";
                    }
                    //速卖通的税率F_ZWLF_STORETYPE
                    if (array[i]["site_type_id"].ToString() == "100" || array[i]["site_type_id"].ToString() == "101")
                    {
                        if (array[i].ToString().Contains("receiver_country"))
                        {
                            //获取税率
                            DataRow[] rowSL = Sldt.Select("F_ZWLF_COUNTRIES='" + array[i]["receiver_country"].ToString() + "' and F_ZWLF_STORETYPE='" + site_type_id + "'");
                            if (rowSL.Length > 0)
                            {
                                foreach (DataRow dr in rowSL)
                                {
                                    F_ZWLF_TAXRATE = Convert.ToDecimal(dr["F_ZWLF_TAXRATE"].ToString()); //获取税率
                                }
                            }
                        }
                    }
                    if (array[i]["site_type_id"].ToString() == "102")
                    {
                        F_ZWLF_TAXRATE = 4;
                    }
                    //存在对应订单
                    if (SoTHRow.Length == 0)
                    {
                        //校验通过生成退货订单
                        if (checkstatus)
                        {
                            //把前订单记录到日志里面
                            sql = string.Format(@"/*dialect*/ INSERT INTO ZWLF_T_OrderLog
                                                  ([FID]
                                                  ,[FBILLNO]
                                                  ,[F_ZWLF_ID]
                                                  ,[F_ZWLF_SITE_TRADE_ID]
                                                  ,[F_ZWLF_TIME]
                                                  ,F_ZWLF_SOURCETYPE
                                                  ,F_ZWLF_Fbillno)
                                                VALUES
                                                 ((select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                                 ,(select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                                 ,'{0}'
                                                 ,'{1}'
                                                 ,GETDATE(),'{3}','{2}')", id, bil_no, Fbillno, F_ZWLF_SOURCETYPE);
                            DBServiceHelper.Execute(context, sql);
                            #region 生成销售订单
                            FormMetadata meta = MetaDataServiceHelper.Load(context, "SAL_SaleOrder") as FormMetadata;
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

                            #region  表头
                            //组织
                            bill_view.Model.SetItemValueByID("FSaleOrgId", FSaleOrgId, 0);
                            bill_view.Model.SetItemValueByID("FPESettleOrgId", FSaleOrgId, 0);
                            // 单据类型 
                            bill_view.Model.SetItemValueByNumber("FBillTypeID", "XSDD05_SYS", 0);
                            //单据业务日期//发货日期
                            bill_view.Model.SetValue("FDate", array[i]["back_time"].ToString());
                            //客户
                            bill_view.Model.SetItemValueByNumber("FCustId", FCUSTIDFNumer, 0);
                            bill_view.InvokeFieldUpdateService("FCustId", 0);
                            //结算币别
                            bill_view.Model.SetItemValueByNumber("FSettleCurrId", cur_code, 0);
                            //源单类型
                            bill_view.Model.SetValue("F_ZWLF_SourceType", "04", 0);
                            //销售员
                            bill_view.Model.SetItemValueByNumber("FSalerId", F_ZWLF_SALERID, 0);
                            bill_view.InvokeFieldUpdateService("FSalerId", 0);
                            //收款条件
                            bill_view.Model.SetValue("FRecConditionId", 101823, 0);
                            //所属网店 
                            bill_view.Model.SetItemValueByID("F_ZWLF_site_name", F_ZWLF_site_name, 0);
                            //网上订单号
                            bill_view.Model.SetValue("F_ZWLF_site_trade_id", site_trade_id, 0);
                            //胜途订单id
                            bill_view.Model.SetValue("F_ZWLF_id", array[i]["id"].ToString(), 0);
                            //胜途单据编号
                            bill_view.Model.SetValue("F_ZWLF_sal_bilno", bil_no, 0);
                            //是否预收FNeedRecAdvance
                            bill_view.Model.SetValue("FNeedRecAdvance", false, 0);
                            //订单跟踪号
                            if (array[i].ToString().Contains("logistics_code"))
                            {
                                bill_view.Model.SetValue("F_ZWLF_logistics_no", array[i]["logistics_code"].ToString(), 0);
                            }
                            else
                            {
                                bill_view.Model.SetValue("F_ZWLF_logistics_no", "找不到退货跟踪单号", 0);
                            }
                            //收货国中文
                            if (array[i].ToString().Contains("receiver_country_cn"))
                            {
                                bill_view.Model.SetValue("F_ZWLF_receiver_country_cn", array[i]["receiver_country_cn"].ToString(), 0);
                            }
                            if (array[i].ToString().Contains("receiver_country"))
                            {
                                bill_view.Model.SetValue("F_ZWLF_Receiver_country", array[i]["receiver_country"].ToString(), 0);
                            }
                            #endregion
                            #region  表体
                            //循环表体
                            JArray details = array[i]["details"] as JArray;
                            for (int j = 0; j < details.Count; j++)
                            {
                                bill_view.Model.CreateNewEntryRow("FSaleOrderEntry");
                                string WLFNUMBER = details[j]["prdt_code"].ToString();
                                //新旧编码
                                DataRow[] rowWL = WLdt.Select("FNUMBER='" + details[j]["prdt_code"].ToString() + "'");
                                if (rowWL.Length == 0)
                                {
                                    //新旧编码
                                    DataRow[] rowWL2 = WLdt.Select("FOLDNUMBER='" + details[j]["prdt_code"].ToString() + "'");
                                    if (rowWL2.Length > 0)
                                    {
                                        foreach (DataRow dr in rowWL2)
                                        {
                                            WLFNUMBER = dr["FNUMBER"].ToString();
                                        }
                                    }
                                }
                                //小包直邮退货
                                if (FSaleOrgId == "165223")
                                {

                                    FStockOrgId = "165223";
                                    //这里生成费用的退货
                                    if (FstockNumber == "CK002" || FstockNumber == "CK005"|| FstockNumber == "CK011")
                                    {
                                        WLFNUMBER = "W0006";
                                    }
                                }
                                bill_view.Model.SetItemValueByNumber("FMaterialId", WLFNUMBER, j);
                                bill_view.InvokeFieldUpdateService("FMATERIALID", j);
                                bill_view.InvokeFieldUpdateService("FUNITID", j);
                                bill_view.InvokeFieldUpdateService("FBASEUNITID", j);
                                //税率FEntryTaxRate
                                bill_view.Model.SetValue("FEntryTaxRate", F_ZWLF_TAXRATE, j);
                                bill_view.InvokeFieldUpdateService("FEntryTaxRate", j);
                                // 销售数量
                                bill_view.Model.SetValue("FQty", details[j]["qty"].ToString(), j);
                                bill_view.InvokeFieldUpdateService("FQty", j);
                                bill_view.InvokeFieldUpdateService("FPriceUnitQty", j);
                                //FIsFree 是否赠品
                                if (details[j].ToString().Contains("is_free"))
                                {
                                    if (details[j]["is_free"].ToString() == "True")
                                    {
                                        bill_view.Model.SetValue("FIsFree", true, j);
                                    }
                                    else
                                    {
                                        bill_view.Model.SetValue("FIsFree", false, j);
                                    }
                                }
                                ////获取含税单价 
                                decimal FTaxPrice = 0;
                                if (details[j].ToString().Contains("amt"))
                                {
                                    FTaxPrice = Convert.ToDecimal(details[j]["amt"].ToString()) / Convert.ToDecimal(details[j]["qty"].ToString());
                                }
                                //含税单价
                                bill_view.Model.SetValue("FTaxPrice", FTaxPrice, j);
                                bill_view.InvokeFieldUpdateService("FTaxPrice", j);
                                bill_view.InvokeFieldUpdateService("FPrice", j);
                                bill_view.InvokeFieldUpdateService("FAmount", j);
                                bill_view.InvokeFieldUpdateService("FAllAmount", j);
                                bill_view.InvokeFieldUpdateService("FAllAmount", j);
                                //库存组织 
                                bill_view.Model.SetItemValueByID("FStockOrgId", FStockOrgId, j);
                                bill_view.InvokeFieldUpdateService("FStockOrgId", j);
                                //仓库编码
                                bill_view.Model.SetItemValueByNumber("FSOStockId", FstockNumber, j);
                                bill_view.InvokeFieldUpdateService("FSOStockId", j);
                                if(WLFNUMBER!= "W0006")
                                {
                                    if (FstockNumber == "CK002")
                                    {
                                        //仓位
                                        bill_view.Model.SetItemValueByID("FSOStockLocalId", 104632, j);
                                        bill_view.Model.SetItemValueByNumber("$$FSOStockLocalId__FF100001", "退货仓位", j);
                                    }
                                }
                                //要货日期DateTime.Now.AddDays(10
                                bill_view.Model.SetValue("FDeliveryDate", array[i]["back_time"].ToString(), j);
                                //计划交货日期
                                bill_view.Model.SetValue("FMinPlanDeliveryDate", array[i]["back_time"].ToString(), j);
                                //结算组织
                                bill_view.Model.SetItemValueByID("FSettleOrgIds", FSaleOrgId, j);
                                bill_view.InvokeFieldUpdateService("FSettleOrgIds", j);
                                //供应组织
                                bill_view.Model.SetItemValueByID("FSupplyOrgId", FStockOrgId, j);
                                bill_view.InvokeFieldUpdateService("FSupplyOrgId", j);
                                //货主
                                bill_view.Model.SetItemValueByID("FOwnerId", FStockOrgId, j);
                                bill_view.InvokeFieldUpdateService("FOwnerId", j);
                            }
                            #endregion
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
                                //记录生成订单成功
                                sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_ORDERSTATE='1',F_ZWLF_FBILLNO='{0}' 
                                                                   where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                   F_ZWLF_Fbillno, array[i]["id"].ToString(), bil_no);
                                DBServiceHelper.Execute(context, sql);
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
                                        msg.result = mssg;
                                        sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_SaleReturnState='0',F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}' 
                                                                where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                                mssg, array[i]["id"].ToString(), bil_no);
                                        DBServiceHelper.Execute(context, sql);
                                    }
                                    else
                                    {
                                        mssg = "生成销售退货单成功";
                                        msg.result = mssg;
                                        sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_SaleReturnState='1',F_ZWLF_NOTE='{0}' 
                                                                  where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                  mssg, array[i]["id"].ToString(), bil_no);
                                    }
                                    DBServiceHelper.Execute(context, sql);
                                }
                                catch (KDException ex)
                                {
                                    msg.result = ex.ToString().Substring(0, 500);
                                    sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_SaleReturnState='0',F_ZWLF_NOTE='{0}'
                                                            where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                            msg.result, array[i]["id"].ToString(), bil_no);
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
                                msg.result = "胜途的售后手工生成金蝶的销售退货订单失败：" + mssg;
                                sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_ORDERSTATE='0',F_ZWLF_NOTE='{0}' 
                                                                where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                                    mssg, array[i]["id"].ToString(), bil_no);
                                DBServiceHelper.Execute(context, sql);
                            }
                            #endregion
                        }
                        else //找不到对应客户生成订单失败
                        {
                            //把前订单记录到日志里面
                            msg.result = "售后手工退货单找不到对应的客户，生成销售退货订单失败:" + checkMsg;
                            sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_SALERETURNSTATE='0',F_ZWLF_NOTE='{0}' 
                                                where F_ZWLF_ID='{1}' and F_ZWLF_SITE_TRADE_ID='{2}';", msg.result, id, bil_no);
                            DBServiceHelper.Execute(context, sql);
                        }

                    }
                    //不存在入库单
                    if (QTRow.Length == 0)
                    {
                        //小包直邮原来其他出库出去的目前要其他入库进来
                        if (FSaleOrgId == "165223")
                        {
                            if (FstockNumber == "CK002" || FstockNumber == "CK005" || FstockNumber == "CK016"
                                || FstockNumber == "CK011" || FstockNumber == "CK026")
                            {
                                #region 生成其他入库
                                FormMetadata meta = MetaDataServiceHelper.Load(context, "STK_MISCELLANEOUS") as FormMetadata;
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

                                #region  表头
                                //库存组织
                                bill_view.Model.SetItemValueByID("FStockOrgId", QFStockOrgId, 0);
                                bill_view.InvokeFieldUpdateService("FStockOrgId", 0);
                                //货主
                                bill_view.Model.SetItemValueByID("FOwnerIdHead", QFStockOrgId, 0);
                                bill_view.InvokeFieldUpdateService("FOwnerIdHead", 0);
                                // 单据类型 
                                bill_view.Model.SetItemValueByNumber("FBillTypeID", "QTRKD03_SYS", 0);
                                //单据业务日期//发货日期
                                bill_view.Model.SetValue("FDate", back_time);
                                //部门
                                bill_view.Model.SetItemValueByNumber("FDEPTID", "D06", 0);
                                bill_view.InvokeFieldUpdateService("FDEPTID", 0);
                                //网上订单号
                                bill_view.Model.SetValue("F_ZWLF_site_trade_id", site_trade_id, 0);
                                //退货id
                                bill_view.Model.SetValue("F_ZWLF_id", array[i]["id"].ToString(), 0);
                                //退货单编号
                                bill_view.Model.SetValue("F_ZWLF_sal_bilno", bil_no, 0);
                                //设置成功
                                bill_view.Model.SetValue("F_ZWLF_PushState", "1", 0);
                                #endregion

                                #region  表体
                                //循环表体
                                JArray details = array[i]["details"] as JArray;
                                for (int j = 0; j < details.Count; j++)
                                {
                                    string WLFNUMBER = details[j]["prdt_code"].ToString();
                                    //新旧编码
                                    DataRow[] rowWL = WLdt.Select("FNUMBER='" + details[j]["prdt_code"].ToString() + "'");
                                    if (rowWL.Length == 0)
                                    {
                                        //新旧编码
                                        DataRow[] rowWL2 = WLdt.Select("FOLDNUMBER='" + details[j]["prdt_code"].ToString() + "'");
                                        if (rowWL2.Length > 0)
                                        {
                                            foreach (DataRow dr in rowWL2)
                                            {
                                                WLFNUMBER = dr["FNUMBER"].ToString();
                                            }
                                        }
                                    }
                                    bill_view.Model.CreateNewEntryRow("FEntity");
                                    bill_view.Model.SetItemValueByNumber("FMATERIALID", WLFNUMBER, j);
                                    bill_view.InvokeFieldUpdateService("FMATERIALID", j);
                                    bill_view.InvokeFieldUpdateService("FUnitID", j);
                                    // 销售数量
                                    bill_view.Model.SetValue("FQty", details[j]["qty"].ToString(), j);
                                    bill_view.InvokeFieldUpdateService("FQty", j);
                                    //仓库
                                    bill_view.Model.SetItemValueByNumber("FSTOCKID", FstockNumber, j);
                                    bill_view.InvokeFieldUpdateService("FSTOCKID", j);
                                    if (FstockNumber == "CK002")
                                    {
                                        //仓位
                                        // bill_view.Model.SetItemValueByID("FSOStockLocalId", 104632, j);
                                        //bill_view.Model.SetItemValueByNumber("$$FSOStockLocalId__FF100001", "退货仓位", j);
                                        bill_view.Model.SetItemValueByID("FStockLocId", 104632, j);
                                        bill_view.Model.SetItemValueByNumber("$$FStockLocId__FF100001", "退货仓位", j);
                                    }
                                }
                                #endregion
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
                                    //记录生成订单成功
                                    sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_QRState='1',F_ZWLF_INFBILLNO='{0}' 
                                                                   where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                       F_ZWLF_Fbillno, array[i]["id"].ToString(), bil_no);
                                    DBServiceHelper.Execute(context, sql);
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
                                            mssg = "生成其他入库退货失败：";
                                            sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_QRState='0',F_ZWLF_NOTE='{0}' 
                                                                where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                                    mssg, array[i]["id"].ToString(), bil_no);
                                            DBServiceHelper.Execute(context, sql);
                                        }
                                        else
                                        {
                                            mssg = "生成其他入库退货成功：";
                                            sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_QRState='1',F_ZWLF_NOTE='{0}' 
                                                                  where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                      mssg, array[i]["id"].ToString(), bil_no);
                                            DBServiceHelper.Execute(context, sql);
                                        }

                                    }
                                    catch (KDException ex)
                                    {
                                        msg.result = ex.ToString().Substring(0, 500);
                                        sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_QRState='0',F_ZWLF_NOTE='{0}'
                                                            where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                msg.result, array[i]["id"].ToString(), bil_no);
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
                                    msg.result = "胜途的售后手工生成金蝶的其他入库单失败：" + mssg;
                                    sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_QRState='0',F_ZWLF_NOTE='{0}' 
                                                                where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                                        mssg, array[i]["id"].ToString(), bil_no);
                                    DBServiceHelper.Execute(context, sql);
                                }
                                #endregion
                            }
                        }
                    }
                }
                catch (KDException ex)
                {
                    //把前订单记录到日志里面
                    string Insql = string.Format(@"/*dialect*/ INSERT INTO ZWLF_T_OrderLog
                                                ([FID]
                                                ,[FBILLNO]
                                                ,[F_ZWLF_ID]
                                                ,[F_ZWLF_SITE_TRADE_ID]
                                                ,[F_ZWLF_TIME]
                                                ,F_ZWLF_NOTE
                                                ,F_ZWLF_SOURCETYPE
                                                ,F_ZWLF_Fbillno)
                                              VALUES
                                               ((select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                               ,(select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                               ,'{0}'
                                               ,'{1}'
                                               ,GETDATE(),'{2}','{4}','{3}')", id, bil_no, ex.ToString().Substring(0, 500), Fbillno, F_ZWLF_SOURCETYPE);
                    msg.result = ex.ToString().Substring(0, 500).Replace("'", "");
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
