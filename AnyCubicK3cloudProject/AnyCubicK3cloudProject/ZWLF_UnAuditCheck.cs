using Kingdee.BOS;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.Operation;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.Interaction;
using Kingdee.BOS.Core.List;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.ConvertElement.ServiceArgs;
using Kingdee.BOS.Orm;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using Kingdee.BOS.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;

namespace AnyCubicK3cloudProject
{
    [Description("已推送胜途单据反审核校验")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_UnAuditCheck : AbstractOperationServicePlugIn
    {
        /// <summary>
        /// 定制加载指定字段到实体里<p>
        /// </summary>
        /// <param name="e">事件对象</param>
        public override void OnPreparePropertys(Kingdee.BOS.Core.DynamicForm.PlugIn.Args.PreparePropertysEventArgs e)
        {
            base.OnPreparePropertys(e);
            e.FieldKeys.Add("F_ZWLF_PushState");//推送状态
        }
        public override void BeginOperationTransaction(BeginOperationTransactionArgs e)
        {
           if (e.DataEntitys != null && e.DataEntitys.Count<DynamicObject>() > 0)
           {
               foreach (DynamicObject item in e.DataEntitys)
               {
                   string F_ZWLF_PushState = item["F_ZWLF_PushState"].ToString();
                   if (F_ZWLF_PushState=="1")
                   {
                       throw new KDException("hls", "该单据已经在胜途生成对应的单据，需删除胜途的对应单据才能反审核！");
                   }
               }
           }
        }
    }
}
