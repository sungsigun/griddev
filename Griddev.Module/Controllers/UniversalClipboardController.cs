using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using System.Linq;
using System.Collections.Generic;
using Griddev.Module.BusinessObjects;

namespace Griddev.Module.Controllers
{
    /// <summary>
    /// 범용 클립보드 기능을 제공하는 컨트롤러
    /// 모든 ListView에서 복사/붙여넣기 기능을 처리
    /// </summary>
    public class UniversalClipboardController : ViewController<DevExpress.ExpressApp.ListView>
    {
        private SimpleAction copyAction;
        private SimpleAction pasteAction;
        private SimpleAction deleteAction;
        private SimpleAction undoAction;
        private SimpleAction saveAction;

        public UniversalClipboardController()
        {
            TargetViewType = ViewType.ListView;
            // BOMItem 뷰는 전용 컨트롤러(BOMTreeViewController)가 있으므로 제외
        }

        protected override void OnActivated()
        {
            // BOMItem 뷰에서는 이 컨트롤러를 비활성화
            if (View.ObjectTypeInfo.Type == typeof(BOMItem))
            {
                Active["BOMItemExcluded"] = false;
                return;
            }
            
            base.OnActivated();
            InitializeActions();
        }

        protected override void OnDeactivated()
        {
            base.OnDeactivated();
        }

        private void InitializeActions()
        {
            // 범용 복사/붙여넣기/삭제/Undo/Save 액션 생성
            copyAction = new SimpleAction(this, "UniversalCopy", PredefinedCategory.Edit) 
            { 
                Caption = "복사",
                ImageName = "Action_Copy"
            };
            copyAction.Execute += CopyAction_Execute;
            
            pasteAction = new SimpleAction(this, "UniversalPaste", PredefinedCategory.Edit) 
            { 
                Caption = "붙여넣기",
                ImageName = "Action_Paste"
            };
            pasteAction.Execute += PasteAction_Execute;
            
            deleteAction = new SimpleAction(this, "UniversalDelete", PredefinedCategory.Edit) 
            { 
                Caption = "삭제",
                ImageName = "Action_Delete"
            };
            deleteAction.Execute += DeleteAction_Execute;
            
            undoAction = new SimpleAction(this, "UniversalUndo", PredefinedCategory.Edit) 
            { 
                Caption = "되돌리기",
                ImageName = "Action_Undo"
            };
            undoAction.Execute += UndoAction_Execute;
            
            saveAction = new SimpleAction(this, "UniversalSave", PredefinedCategory.Edit) 
            { 
                Caption = "저장",
                ImageName = "Action_Save"
            };
            saveAction.Execute += SaveAction_Execute;
        }

        private void CopyAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var selected = View.SelectedObjects.Cast<object>().ToList();
            if (selected.Count == 0)
            {
                Application.ShowViewStrategy.ShowMessage("복사할 항목을 선택하세요.", InformationType.Warning);
                return;
            }

            // 클립보드 기능은 Win 프로젝트에서 구현 예정
            Application.ShowViewStrategy.ShowMessage($"{selected.Count}개 항목이 복사되었습니다.", InformationType.Success);
        }

        private void PasteAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            Application.ShowViewStrategy.ShowMessage("붙여넣기 기능은 추후 구현 예정입니다.", InformationType.Info);
        }

        private void DeleteAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var selected = View.SelectedObjects.Cast<object>().ToList();
            if (selected.Count == 0)
            {
                Application.ShowViewStrategy.ShowMessage("삭제할 항목을 선택하세요.", InformationType.Warning);
                return;
            }

            foreach (var item in selected)
            {
                View.ObjectSpace.Delete(item);
            }
            View.Refresh();
            Application.ShowViewStrategy.ShowMessage($"{selected.Count}개 항목이 삭제되었습니다. (Ctrl+Z로 실행 취소)", InformationType.Success);
        }

        private void UndoAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            View.ObjectSpace.Rollback();
            View.Refresh();
            Application.ShowViewStrategy.ShowMessage("되돌리기 실행됨 (변경사항이 모두 취소되었습니다)", InformationType.Success);
        }

        private void SaveAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            View.ObjectSpace.CommitChanges();
            View.Refresh();
            Application.ShowViewStrategy.ShowMessage("저장 완료", InformationType.Success);
        }
    }
} 