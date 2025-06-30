using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Win.Editors;
using Griddev.Module.BusinessObjects;
using Griddev.Win.Editors;

namespace Griddev.Win.Controllers
{
    public class MasterDetailDeleteController : ViewController<ListView>
    {
        public MasterDetailDeleteController()
        {
            // Order ListView에서만 활성화
            TargetObjectType = typeof(Order);
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            
            // MasterDetailListEditor를 사용하는 경우에만 기본 삭제 액션 비활성화
            if (View?.Editor is MasterDetailListEditor)
            {
                // XAF의 기본 삭제 액션 찾기
                var deleteAction = Frame.GetController<DevExpress.ExpressApp.SystemModule.DeleteObjectsViewController>()?.DeleteAction;
                if (deleteAction != null)
                {
                    deleteAction.Active["MasterDetailCustomDelete"] = false;
                }

                // 기본 새로 만들기 액션도 필요시 제어 가능
                var newAction = Frame.GetController<DevExpress.ExpressApp.SystemModule.NewObjectViewController>()?.NewObjectAction;
                if (newAction != null)
                {
                    // 새로 만들기는 유지하되, 필요시 커스터마이징 가능
                    // newAction.Active["MasterDetailCustomNew"] = false;
                }
            }
        }

        protected override void OnDeactivated()
        {
            // 다른 뷰로 이동할 때 액션 복원
            var deleteAction = Frame.GetController<DevExpress.ExpressApp.SystemModule.DeleteObjectsViewController>()?.DeleteAction;
            if (deleteAction != null)
            {
                deleteAction.Active["MasterDetailCustomDelete"] = true;
            }

            base.OnDeactivated();
        }
    }
} 