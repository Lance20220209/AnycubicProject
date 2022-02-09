using Kingdee.BOS;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.Operation;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.Interaction;
using Kingdee.BOS.Core.List;
using Kingdee.BOS.Core.List.PlugIn;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.ConvertElement.ServiceArgs;
using Kingdee.BOS.Core.Permission;
using Kingdee.BOS.Orm;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using Kingdee.BOS.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnyCubicK3cloudProject
{
    [Description("胜途销售订单取消")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_SaleOrderCancel : AbstractListPlugIn
    {
        public override void BarItemClick(Kingdee.BOS.Core.DynamicForm.PlugIn.Args.BarItemClickEventArgs e)
        {
            base.BarItemClick(e);
            if (e.BarItemKey.Equals("ZWLF_tbCancelOrder"))
            {
                //权限项内码，通过 T_SEC_PermissionItem 权限项表格进行查询select *from T_SEC_PermissionItem where FNUMBER='QXDD'。
                 string sql = "/*dialect*/ select FITEMID from T_SEC_PermissionItem where FNUMBER='QXDD'";
                 string permissionItem = DBServiceHelper.ExecuteScalar<string>(Context, sql, "", null);
                 PermissionAuthResult permissionAuthResult = this.View.Model.FuncPermissionAuth(
                 new string[] { "" }, permissionItem, null, false).FirstOrDefault();
                 if (permissionAuthResult != null && !permissionAuthResult.Passed)
                 {
                     this.View.ShowErrMessage("当前用户没有取消订单的权限！");
                     e.Cancel = true;
                     return;
                 }
                else
                {
                    try
                    {
                        Msg msg = new Msg();
                        //选择的行,获取所有信息,放在listcoll里面
                        ListSelectedRowCollection listcoll = this.ListView.SelectedRowsInfo;
                        //接收返回的数组值
                        string[] listKey = listcoll.GetPrimaryKeyValues();
                        if (listKey.Length == 0)
                        {
                            this.View.ShowErrMessage("未选择取消的订单", "取消失败", MessageBoxType.Error);
                            return;
                        }
                        //for循环
                        foreach (string key in listKey)
                        {
                            msg = SaleOrderOuStockPush(key);
                            if (!msg.status)
                            {
                                this.View.ShowErrMessage(msg.result, "下推销售退货单失败", MessageBoxType.Error);
                                return;
                            }
                            else
                            {
                                //取消后的单需要重新更新胜途id
                                 sql = string.Format(@"/*dialect*/ update T_SAL_ORDER set F_ZWLF_ID=F_ZWLF_ID+'_1',
                                      F_ZWLF_SITE_TRADE_ID=F_ZWLF_SITE_TRADE_ID+'_1',
                                      F_ZWLF_CANCELSTATE=0,F_ZWLF_CancelDate=getdate() ,
                                      F_ZWLF_USERID={1},F_ZWLF_sal_bilno=F_ZWLF_sal_bilno+'_1'
                                      where  FID='{0}'", key,Context.UserId);
                                DBServiceHelper.Execute(Context, sql);
                                this.View.ShowMessage("销售订单取消成功！");
                            }
                        }
                    }
                    catch (KDException ex)
                    {
                        throw new KDException("hls", "订单取消失败：" + ex.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// 销售出库单下推退货单
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public Msg SaleOrderOuStockPush(string Id)
        {
            Msg msg = new Msg();
            //单据转换
            string srcFormId = "SAL_OUTSTOCK"; //销售出库单
            string destFormId = "SAL_RETURNSTOCK"; //销售退货单
            IMetaDataService mService = Kingdee.BOS.App.ServiceHelper.GetService<IMetaDataService>();
            IViewService vService = Kingdee.BOS.App.ServiceHelper.GetService<IViewService>();
            FormMetadata destmeta = mService.Load(Context, destFormId) as FormMetadata;
            //转换规则的唯一标识
            //string ruleKey = "OutStock_InStock";
            var rules = ConvertServiceHelper.GetConvertRules(Context, srcFormId, destFormId);
            var rule = rules.FirstOrDefault(t => t.IsDefault);
            // ConvertRuleElement rule = GetDefaultConvertRule(ctx, srcFormId, destFormId, ruleKey);
            List<ListSelectedRow> lstRows = new List<ListSelectedRow>();
            string strsql = string.Format(@"select  a.FENTRYID,a.FID from  T_SAL_OUTSTOCKENTRY a 
                                        inner join T_SAL_OUTSTOCKENTRY_LK b on a.FENTRYID=b.FENTRYID where FSBILLID='{0}'", Id);
            DataSet ds = DBServiceHelper.ExecuteDataSet(Context, strsql);
            for (int j = 0; j < ds.Tables[0].Rows.Count; j++)
            {
                long entryId = Convert.ToInt64(ds.Tables[0].Rows[j]["FENTRYID"]);
                string  SoFID= ds.Tables[0].Rows[j]["FID"].ToString();
                //单据标识
                ListSelectedRow row = new ListSelectedRow(SoFID, entryId.ToString(), 0, "SAL_OUTSTOCK");
                //源单单据体标识
                row.EntryEntityKey = "FEntity";
                lstRows.Add(row);
            }
            PushArgs pargs = new PushArgs(rule, lstRows.ToArray());
            IConvertService cvtService = Kingdee.BOS.App.ServiceHelper.GetService<IConvertService>();
            OperateOption option = OperateOption.Create();
            option.SetIgnoreWarning(true);
            option.SetVariableValue("ignoreTransaction", false);
            option.SetIgnoreInteractionFlag(true);
            ConvertOperationResult cvtResult = cvtService.Push(Context, pargs, option, false);
            if (cvtResult.IsSuccess)
            {
                string mssg = "";
                DynamicObject[] dylist =(from p in cvtResult.TargetDataEntities select p.DataEntity).ToArray();
                IOperationResult result = Operation(dylist, destmeta);
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
                    string FID = operateResults[0].PKValue.ToString();
                    mssg= operateResults[0].Message;
                    string fnmber = operateResults[0].Number;
                    msg.result = mssg;
                    msg.status = true;
                }
            }
            return msg;
        }

        /// <summary>
        ///保存
        /// </summary>
        /// <param name="newdlm"></param>
        /// <param name="materialmeta"></param>
        /// <returns></returns>
        private IOperationResult Operation(DynamicObject[] dylist, FormMetadata materialmeta)
        {
            OperateOption option = OperateOption.Create();
            option.SetIgnoreWarning(true);
            option.SetVariableValue("ignoreTransaction", true);
            //保存
            ISaveService saveService = Kingdee.BOS.App.ServiceHelper.GetService<ISaveService>();
            IOperationResult saveresult = saveService.Save(Context, materialmeta.BusinessInfo, dylist, option);
            //提交
            object[] items = dylist.Select(p => p["Id"]).ToArray();
            ISubmitService submitService = Kingdee.BOS.App.ServiceHelper.GetService<ISubmitService>();
            IOperationResult submitresult = submitService.Submit(Context, materialmeta.BusinessInfo, items, "Submit", option);
            //审核
            IAuditService auditService = Kingdee.BOS.App.ServiceHelper.GetService<IAuditService>();
            IOperationResult auditresult = auditService.Audit(Context, materialmeta.BusinessInfo, items, option);
            return auditresult;
        }
    }
}
