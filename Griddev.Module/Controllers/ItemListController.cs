using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.Persistent.Base;
using Griddev.Module.BusinessObjects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Griddev.Module.Controllers
{
    /// <summary>
    /// 품목등록 ListView에서 키보드 단축키 지원 컨트롤러
    /// </summary>
    public partial class ItemListController : ViewController<DevExpress.ExpressApp.ListView>
    {
        private SimpleAction copyAction;
        private SimpleAction pasteAction;
        private SimpleAction deleteAction;
        private SimpleAction undoAction;
        private List<Item> copiedItems = new List<Item>();

        public ItemListController()
        {
            TargetObjectType = typeof(Item);
            TargetViewType = ViewType.ListView;
            
            // 액션 초기화
            InitializeActions();
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            
            // XAF 기본 Delete 액션 숨기기 (커스텀 삭제 사용)
            var deleteController = Frame.GetController<DeleteObjectsViewController>();
            if (deleteController != null)
            {
                deleteController.DeleteAction.Active["ItemCustomDelete"] = false;
            }
            
            // 이벤트 핸들러 등록
            View.SelectionChanged += View_SelectionChanged;
            View.ControlsCreated += View_ControlsCreated;
            View.ObjectSpace.ModifiedChanged += ObjectSpace_ModifiedChanged;
            UpdateActionStates();
        }

        protected override void OnDeactivated()
        {
            View.SelectionChanged -= View_SelectionChanged;
            View.ControlsCreated -= View_ControlsCreated;
            View.ObjectSpace.ModifiedChanged -= ObjectSpace_ModifiedChanged;
            base.OnDeactivated();
        }

        private void InitializeActions()
        {
            // 복사 액션
            copyAction = new SimpleAction(this, "ItemCopy", PredefinedCategory.Edit)
            {
                Caption = "복사",
                ImageName = "Action_Copy",
                ToolTip = "선택된 품목을 복사합니다.",
                Shortcut = "Ctrl+C"
            };
            copyAction.Execute += CopyAction_Execute;

            // 붙여넣기 액션
            pasteAction = new SimpleAction(this, "ItemPaste", PredefinedCategory.Edit)
            {
                Caption = "붙여넣기",
                ImageName = "Action_Paste",
                ToolTip = "복사된 품목을 붙여넣습니다.",
                Shortcut = "Ctrl+V"
            };
            pasteAction.Execute += PasteAction_Execute;

            // 삭제 액션
            deleteAction = new SimpleAction(this, "ItemDelete", PredefinedCategory.Edit)
            {
                Caption = "삭제",
                ImageName = "Action_Delete",
                ToolTip = "선택된 품목을 삭제합니다. (Ctrl+U로 되돌리기 가능)",
                Shortcut = "Delete"
            };
            deleteAction.Execute += DeleteAction_Execute;

            // 되돌리기 액션
            undoAction = new SimpleAction(this, "ItemUndo", PredefinedCategory.Edit)
            {
                Caption = "되돌리기",
                ImageName = "Action_Undo",
                ToolTip = "마지막 작업을 되돌립니다. (Ctrl+U)",
                Shortcut = "Ctrl+U"
            };
            undoAction.Execute += UndoAction_Execute;
        }

        protected override void OnViewControlsCreated()
        {
            base.OnViewControlsCreated();
            
            // GridView를 엑셀처럼 자유롭게 편집 가능하도록 설정
            if (View.Editor?.GetType().Name == "GridListEditor")
            {
                try
                {
                    var gridViewProperty = View.Editor.GetType().GetProperty("GridView");
                    if (gridViewProperty != null)
                    {
                        var gridView = gridViewProperty.GetValue(View.Editor);
                        if (gridView != null)
                        {
                            // 키보드 이벤트 등록 (상단 버튼과 연결)
                            var keyDownEvent = gridView.GetType().GetEvent("KeyDown");
                            if (keyDownEvent != null)
                            {
                                var handlerType = keyDownEvent.EventHandlerType;
                                var method = this.GetType().GetMethod("GridView_KeyDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (method != null)
                                {
                                    var handler = System.Delegate.CreateDelegate(handlerType, this, method);
                                    keyDownEvent.AddEventHandler(gridView, handler);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GridView 설정 실패: {ex.Message}");
                }
            }
        }

        private void GridView_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            try
            {
                // 상단 버튼과 연결된 단축키 처리
                if (e.Control && e.KeyCode == System.Windows.Forms.Keys.C)
                {
                    e.Handled = true;
                    copyAction.DoExecute();  // 상단 복사 버튼 실행
                }
                else if (e.Control && e.KeyCode == System.Windows.Forms.Keys.V)
                {
                    e.Handled = true;
                    pasteAction.DoExecute();  // 상단 붙여넣기 버튼 실행
                }
                else if (e.KeyCode == System.Windows.Forms.Keys.Delete)
                {
                    e.Handled = true;
                    deleteAction.DoExecute();  // 상단 삭제 버튼 실행
                }
                else if (e.Control && e.KeyCode == System.Windows.Forms.Keys.U)
                {
                    e.Handled = true;
                    undoAction.DoExecute();  // 상단 되돌리기 버튼 실행
                }
                // 다른 키들은 정상적인 셀 편집 허용
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"키보드 단축키 처리 실패: {ex.Message}");
            }
        }

        private void View_SelectionChanged(object sender, EventArgs e)
        {
            UpdateActionStates();
        }

        private void View_ControlsCreated(object sender, EventArgs e)
        {
            UpdateActionStates();
        }

        private void ObjectSpace_ModifiedChanged(object sender, EventArgs e)
        {
            UpdateActionStates();
        }

        private void UpdateActionStates()
        {
            bool hasSelection = View.SelectedObjects.Count > 0;
            bool hasCopiedData = copiedItems.Count > 0;
            bool hasChanges = View.ObjectSpace.IsModified;

            copyAction.Enabled["HasSelection"] = hasSelection;
            pasteAction.Enabled["HasCopiedData"] = hasCopiedData;
            deleteAction.Enabled["HasSelection"] = hasSelection;
            undoAction.Enabled["HasChanges"] = hasChanges;
        }

        // 복사 기능
        private void CopyAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            if (View.SelectedObjects.Count > 0)
            {
                copiedItems = View.SelectedObjects.Cast<Item>().ToList();
                
                var itemNames = string.Join(", ", copiedItems.Select(x => x.ItemName).Take(3));
                if (copiedItems.Count > 3)
                    itemNames += $" 외 {copiedItems.Count - 3}개";
                    
                Application.ShowViewStrategy.ShowMessage(
                    $"[Ctrl+C] 복사 완료: {copiedItems.Count}개 품목\n({itemNames})", 
                    InformationType.Success);
                    
                UpdateActionStates();
            }
            else
            {
                Application.ShowViewStrategy.ShowMessage("[Ctrl+C] 복사할 품목을 선택해주세요.", InformationType.Warning);
            }
        }

        // 붙여넣기 기능
        private void PasteAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            if (copiedItems.Count == 0)
            {
                Application.ShowViewStrategy.ShowMessage("[Ctrl+V] 복사된 품목이 없습니다. 먼저 Ctrl+C로 복사해주세요.", InformationType.Info);
                return;
            }

            try
            {
                int successCount = 0;
                var newItems = new List<Item>();

                // 복사된 항목 수만큼 새 품목 생성
                foreach (var originalItem in copiedItems)
                {
                    // 새 품목 생성 (복사본)
                    var newItem = View.ObjectSpace.CreateObject<Item>();
                    newItem.ItemCode = GenerateNewItemCode(originalItem.ItemCode);
                    newItem.ItemName = $"{originalItem.ItemName} (복사본)";
                    newItem.ItemType = originalItem.ItemType;
                    newItem.Unit = originalItem.Unit;
                    newItem.Description = originalItem.Description;
                    newItem.IsActive = true;

                    newItems.Add(newItem);
                    successCount++;
                }

                // 변경사항 저장
                View.ObjectSpace.CommitChanges();
                
                // 뷰 새로고침
                View.Refresh();
                
                // 새로 생성된 첫 번째 항목을 현재 객체로 설정
                if (newItems.Count > 0)
                {
                    View.CurrentObject = newItems.First();
                }

                var itemNames = string.Join(", ", newItems.Select(x => x.ItemName).Take(3));
                if (successCount > 3)
                    itemNames += $" 외 {successCount - 3}개";

                Application.ShowViewStrategy.ShowMessage(
                    $"[Ctrl+V] 붙여넣기 완료: {successCount}개 품목 생성\n({itemNames})", 
                    InformationType.Success);
                    
                UpdateActionStates();
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage($"[Ctrl+V] 붙여넣기 중 오류: {ex.Message}", InformationType.Error);
            }
        }

        // 삭제 기능
        private void DeleteAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                var selectedItems = View.SelectedObjects.Cast<Item>().ToList();
                if (selectedItems.Count == 0) return;

                foreach (var item in selectedItems)
                {
                    View.ObjectSpace.Delete(item);
                }

                var itemNames = string.Join(", ", selectedItems.Select(x => x.ItemName).Take(3));
                if (selectedItems.Count > 3)
                    itemNames += $" 외 {selectedItems.Count - 3}개";

                Application.ShowViewStrategy.ShowMessage(
                    $"[Del] 삭제 완료: {selectedItems.Count}개 품목\n({itemNames})\n※ Ctrl+U로 되돌리기 가능", 
                    InformationType.Success);
                    
                UpdateActionStates();
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage($"[Del] 삭제 중 오류: {ex.Message}", InformationType.Error);
            }
        }

        // 되돌리기 기능
        private void UndoAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                View.ObjectSpace.Rollback();
                View.Refresh();
                
                Application.ShowViewStrategy.ShowMessage(
                    "[Ctrl+U] 되돌리기 완료", 
                    InformationType.Success);
                    
                UpdateActionStates();
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage($"[Ctrl+U] 되돌리기 중 오류: {ex.Message}", InformationType.Error);
            }
        }

        // 새로운 품목코드 생성 (중복 방지)
        private string GenerateNewItemCode(string originalCode)
        {
            try
            {
                var baseCode = originalCode;
                var counter = 1;
                string newCode;

                do
                {
                    newCode = $"{baseCode}_복사{counter}";
                    counter++;
                    
                    // 중복 체크 (최대 100번 시도)
                    var existingItem = View.ObjectSpace.FindObject<Item>(
                        CriteriaOperator.Parse("ItemCode = ?", newCode));
                        
                    if (existingItem == null)
                        break;
                        
                } while (counter <= 100);

                return newCode;
            }
            catch
            {
                // 오류 발생 시 타임스탬프 기반 코드 생성
                return $"{originalCode}_{DateTime.Now:yyyyMMddHHmmss}";
            }
        }
    }
} 