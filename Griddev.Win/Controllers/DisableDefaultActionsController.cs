using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Editors;
using Griddev.Module.BusinessObjects;
using System.Linq;
using Griddev.Win.Editors;

namespace Griddev.Win.Controllers
{
    // XAF 기본 New, Delete 액션을 비활성화하는 컨트롤러
    public class DisableDefaultActionsController : ViewController
    {
        public DisableDefaultActionsController()
        {
            // Order 객체에 대해서만 동작
            TargetObjectType = typeof(Order);
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            
            // Order ListView에서만 기본 액션들을 비활성화
            if (View is ListView && View.ObjectTypeInfo.Type == typeof(Order))
            {
                DisableDefaultActions();
            }
        }

        protected override void OnViewControlsCreated()
        {
            base.OnViewControlsCreated();
            
            // Editor가 생성된 후 다시 확인
            if (View is ListView && View.ObjectTypeInfo.Type == typeof(Order))
            {
                DisableDefaultActions();
            }
        }

        private void DisableDefaultActions()
        {
            // MasterDetailListEditor를 사용하는 경우에만 기본 액션들 비활성화
            if (View is ListView listView && listView.Editor?.GetType().Name == "MasterDetailListEditor")
            {
                // 기본 New 액션 비활성화
                var newObjectViewController = Frame.GetController<NewObjectViewController>();
                if (newObjectViewController?.NewObjectAction != null)
                {
                    newObjectViewController.NewObjectAction.Active["DisableForMasterDetail"] = false;
                }

                // 기본 Delete 액션 비활성화
                var deleteObjectsViewController = Frame.GetController<DeleteObjectsViewController>();
                if (deleteObjectsViewController?.DeleteAction != null)
                {
                    deleteObjectsViewController.DeleteAction.Active["DisableForMasterDetail"] = false;
                }

                // 기본 Refresh 액션 숨기기
                var refreshController = Frame.GetController<RefreshController>();
                if (refreshController?.RefreshAction != null)
                {
                    refreshController.RefreshAction.Active["HideForMasterDetail"] = false;
                }
            }
        }
    }
} 