using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Layout;
using DevExpress.ExpressApp.Model.NodeGenerators;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Templates;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Utils;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using Griddev.Module.BusinessObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Griddev.Module.Controllers
{
    /// <summary>
    /// BOM 트리 관리를 위한 컨트롤러 (BOMTREE 패턴 적용)
    /// TreeListEditor에서만 동작하며, 간단한 ObjectSpace.Rollback() 방식 사용
    /// </summary>
    public partial class BOMTreeViewController : ViewController<DevExpress.ExpressApp.ListView>
    {
        private PopupWindowShowAction selectItemAction;
        private PopupWindowShowAction addChildItemAction;
        private SimpleAction refreshTreeAction;
        private SimpleAction copyAction;
        private SimpleAction pasteAction;
        private SimpleAction deleteAction;
        private SimpleAction undoAction;
        private List<BOMItem> copiedNodes = new List<BOMItem>();

        public BOMTreeViewController()
        {
            TargetObjectType = typeof(BOMItem);
            TargetViewType = ViewType.ListView;
            
            // 생성자에서 액션 초기화 (올바른 방법)
            InitializeActions();
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            
            // XAF 기본 액션들 숨기기
            HideStandardActions();
            
            // 이벤트 핸들러 등록
            View.SelectionChanged += View_SelectionChanged;
            View.ControlsCreated += View_ControlsCreated;
            UpdateActionStates();
        }
        
        private void HideStandardActions()
        {
            // XAF 기본 Delete 액션 숨기기
            var deleteAction = Frame.GetController<DeleteObjectsViewController>();
            if (deleteAction != null)
            {
                deleteAction.DeleteAction.Active["BOMTreeCustomDelete"] = false;
            }
            
            // XAF 기본 New 액션 숨기기
            var newObjectAction = Frame.GetController<NewObjectViewController>();
            if (newObjectAction != null)
            {
                newObjectAction.NewObjectAction.Active["BOMTreeCustomNew"] = false;
            }
        }

        protected override void OnDeactivated()
        {
            View.SelectionChanged -= View_SelectionChanged;
            View.ControlsCreated -= View_ControlsCreated;
            
            if (selectItemAction != null)
            {
                selectItemAction.CustomizePopupWindowParams -= SelectItemAction_CustomizePopupWindowParams;
                selectItemAction.Execute -= SelectItemAction_Execute;
            }
            if (addChildItemAction != null)
            {
                addChildItemAction.CustomizePopupWindowParams -= AddChildItemAction_CustomizePopupWindowParams;
                addChildItemAction.Execute -= AddChildItemAction_Execute;
            }
            
            base.OnDeactivated();
        }

        private void InitializeActions()
        {
            // 액션이 이미 생성되어 있으면 중복 생성 방지
            if (selectItemAction != null) return;
            
            // 품목선택 액션 (고유 ID 사용)
            selectItemAction = new PopupWindowShowAction(this, "BOMTreeSelectItem", PredefinedCategory.ObjectsCreation)
            {
                Caption = "품목선택",
                ImageName = "Action_Search",
                ToolTip = "품목을 선택하여 BOM에 추가합니다."
            };
            selectItemAction.CustomizePopupWindowParams += SelectItemAction_CustomizePopupWindowParams;
            selectItemAction.Execute += SelectItemAction_Execute;

            // 하위품목추가 액션
            addChildItemAction = new PopupWindowShowAction(this, "BOMTreeAddChildItem", PredefinedCategory.Edit)
            {
                Caption = "하위품목추가",
                ImageName = "Action_Add",
                ToolTip = "선택된 품목의 하위에 새 품목을 추가합니다.",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            addChildItemAction.CustomizePopupWindowParams += AddChildItemAction_CustomizePopupWindowParams;
            addChildItemAction.Execute += AddChildItemAction_Execute;

            // 트리 새로고침 액션
            refreshTreeAction = new SimpleAction(this, "BOMTreeRefresh", PredefinedCategory.View)
            {
                Caption = "트리새로고침",
                ImageName = "Action_Refresh",
                ToolTip = "BOM 트리를 새로고침합니다."
            };
            refreshTreeAction.Execute += RefreshTreeAction_Execute;

            // 복사/붙여넣기/삭제/되돌리기 액션 (저장 액션 제거)
            copyAction = new SimpleAction(this, "BOMTreeCopy", PredefinedCategory.Edit) 
            { 
                Caption = "복사",
                ImageName = "Action_Copy",
                ToolTip = "선택된 항목을 복사합니다."
            };
            copyAction.Execute += CopyAction_Execute;
            
            pasteAction = new SimpleAction(this, "BOMTreePaste", PredefinedCategory.Edit) 
            { 
                Caption = "붙여넣기",
                ImageName = "Action_Paste",
                ToolTip = "복사된 항목을 붙여넣습니다."
            };
            pasteAction.Execute += PasteAction_Execute;
            
            deleteAction = new SimpleAction(this, "BOMTreeDelete", PredefinedCategory.Edit) 
            { 
                Caption = "삭제",
                ImageName = "Action_Delete",
                ToolTip = "선택된 항목을 삭제합니다. (Ctrl+Z로 되돌리기 가능)"
            };
            deleteAction.Execute += DeleteAction_Execute;
            
            undoAction = new SimpleAction(this, "BOMTreeUndo", PredefinedCategory.Edit) 
            { 
                Caption = "되돌리기",
                ImageName = "Action_Undo",
                ToolTip = "마지막 작업을 되돌립니다."
            };
            undoAction.Execute += UndoAction_Execute;
        }

        protected override void OnViewControlsCreated()
        {
            base.OnViewControlsCreated();
            
            // GridView 편집 모드 활성화 (근본적 해결책)
            var editorTypeName = View.Editor?.GetType().Name ?? "";
            
            if (editorTypeName.Contains("GridListEditor"))
            {
                // GridView 편집 설정
                try
                {
                    var gridViewProperty = View.Editor.GetType().GetProperty("GridView");
                    if (gridViewProperty != null)
                    {
                        var gridView = gridViewProperty.GetValue(View.Editor);
                        if (gridView != null)
                        {
                            // GridView 편집 옵션 설정
                            var optionsBehaviorProperty = gridView.GetType().GetProperty("OptionsBehavior");
                            if (optionsBehaviorProperty != null)
                            {
                                var optionsBehavior = optionsBehaviorProperty.GetValue(gridView);
                                if (optionsBehavior != null)
                                {
                                    var editableProperty = optionsBehavior.GetType().GetProperty("Editable");
                                    editableProperty?.SetValue(optionsBehavior, true);
                                    
                                    var allowAddRowsProperty = optionsBehavior.GetType().GetProperty("AllowAddRows");
                                    allowAddRowsProperty?.SetValue(optionsBehavior, false);
                                    
                                    var allowDeleteRowsProperty = optionsBehavior.GetType().GetProperty("AllowDeleteRows");
                                    allowDeleteRowsProperty?.SetValue(optionsBehavior, false);
                                }
                            }
                            
                            var optionsViewProperty = gridView.GetType().GetProperty("OptionsView");
                            if (optionsViewProperty != null)
                            {
                                var optionsView = optionsViewProperty.GetValue(gridView);
                                if (optionsView != null)
                                {
                                    var newItemRowPositionProperty = optionsView.GetType().GetProperty("NewItemRowPosition");
                                    if (newItemRowPositionProperty != null)
                                    {
                                        var newItemRowPositionType = System.Type.GetType("DevExpress.XtraGrid.Views.Grid.NewItemRowPosition, DevExpress.XtraGrid");
                                        if (newItemRowPositionType != null)
                                        {
                                            var noneValue = System.Enum.Parse(newItemRowPositionType, "None");
                                            newItemRowPositionProperty.SetValue(optionsView, noneValue);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GridView 편집 설정 실패: {ex.Message}");
                }
            }
            else if (editorTypeName.Contains("TreeList"))
            {
                try
                {
                    var treeListProperty = View.Editor.GetType().GetProperty("TreeList");
                    if (treeListProperty != null)
                    {
                        var treeList = treeListProperty.GetValue(View.Editor);
                        if (treeList != null)
                        {
                            // TreeList 편집 모드 활성화 (올바른 방법)
                            var optionsBehaviorProperty = treeList.GetType().GetProperty("OptionsBehavior");
                            if (optionsBehaviorProperty != null)
                            {
                                var optionsBehavior = optionsBehaviorProperty.GetValue(treeList);
                                if (optionsBehavior != null)
                                {
                                    var editableProperty = optionsBehavior.GetType().GetProperty("Editable");
                                    editableProperty?.SetValue(optionsBehavior, true);
                                    
                                    var allowIncrementalSearchProperty = optionsBehavior.GetType().GetProperty("AllowIncrementalSearch");
                                    allowIncrementalSearchProperty?.SetValue(optionsBehavior, false);
                                    
                                    var readOnlyProperty = optionsBehavior.GetType().GetProperty("ReadOnly");
                                    readOnlyProperty?.SetValue(optionsBehavior, false);
                                    
                                    // 편집 트리거 설정 (더블클릭으로 변경)
                                    var editingModeProperty = optionsBehavior.GetType().GetProperty("EditingMode");
                                    if (editingModeProperty != null)
                                    {
                                        var editingModeType = System.Type.GetType("DevExpress.XtraTreeList.TreeListEditingMode, DevExpress.XtraTreeList");
                                        if (editingModeType != null)
                                        {
                                            var editOnDoubleClickValue = System.Enum.Parse(editingModeType, "EditOnDoubleClick");
                                            editingModeProperty.SetValue(optionsBehavior, editOnDoubleClickValue);
                                        }
                                    }
                                }
                            }
                            
                            // TreeList 편집 설정 추가
                            var optionsViewProperty = treeList.GetType().GetProperty("OptionsView");
                            if (optionsViewProperty != null)
                            {
                                var optionsView = optionsViewProperty.GetValue(treeList);
                                if (optionsView != null)
                                {
                                    var showIndicatorProperty = optionsView.GetType().GetProperty("ShowIndicator");
                                    showIndicatorProperty?.SetValue(optionsView, true);
                                }
                            }
                            

                            
                            // Quantity 컬럼에 SpinEdit 설정
                            var columnsProperty = treeList.GetType().GetProperty("Columns");
                            if (columnsProperty != null)
                            {
                                var columns = columnsProperty.GetValue(treeList);
                                if (columns != null)
                                {
                                    var indexer = columns.GetType().GetProperty("Item", new[] { typeof(string) });
                                    if (indexer != null)
                                    {
                                        var quantityColumn = indexer.GetValue(columns, new object[] { "Quantity" });
                                        if (quantityColumn != null)
                                        {
                                            var columnEditProperty = quantityColumn.GetType().GetProperty("ColumnEdit");
                                            if (columnEditProperty != null)
                                            {
                                                // SpinEdit 생성 및 설정
                                                var spinEditType = System.Type.GetType("DevExpress.XtraEditors.Repository.RepositoryItemSpinEdit, DevExpress.XtraEditors");
                                                if (spinEditType != null)
                                                {
                                                    var spinEdit = System.Activator.CreateInstance(spinEditType);
                                                    if (spinEdit != null)
                                                    {
                                                        var incrementProperty = spinEdit.GetType().GetProperty("Increment");
                                                        incrementProperty?.SetValue(spinEdit, 0.1m);
                                                        
                                                        var minValueProperty = spinEdit.GetType().GetProperty("MinValue");
                                                        minValueProperty?.SetValue(spinEdit, 0m);
                                                        
                                                        var maxValueProperty = spinEdit.GetType().GetProperty("MaxValue");
                                                        maxValueProperty?.SetValue(spinEdit, 9999m);
                                                        
                                                        var displayFormatProperty = spinEdit.GetType().GetProperty("DisplayFormat");
                                                        if (displayFormatProperty != null)
                                                        {
                                                            var formatInfoType = System.Type.GetType("DevExpress.Utils.FormatInfo, DevExpress.Data");
                                                            if (formatInfoType != null)
                                                            {
                                                                var createNumericMethod = formatInfoType.GetMethod("CreateNumericFormat", new[] { typeof(int), typeof(bool) });
                                                                if (createNumericMethod != null)
                                                                {
                                                                    var formatInfo = createNumericMethod.Invoke(null, new object[] { 2, false });
                                                                    displayFormatProperty.SetValue(spinEdit, formatInfo);
                                                                }
                                                            }
                                                        }
                                                        
                                                        columnEditProperty.SetValue(quantityColumn, spinEdit);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            
                            // 키보드 이벤트 등록 (KeyDown만 사용)
                            try
                            {
                                var keyDownEvent = treeList.GetType().GetEvent("KeyDown");
                                if (keyDownEvent != null)
                                {
                                    var handlerType = keyDownEvent.EventHandlerType;
                                    var method = this.GetType().GetMethod("TreeList_KeyDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    if (method != null)
                                    {
                                        var handler = System.Delegate.CreateDelegate(handlerType, this, method);
                                        keyDownEvent.AddEventHandler(treeList, handler);
                                    }
                                }
                                

                            }
                            catch
                            {
                                // 키보드 이벤트 등록 실패 시 무시
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"TreeList 설정 실패: {ex.Message}");
                }
            }
        }



        private void TreeList_KeyDown(object sender, object e)
        {
            try
            {
                // TreeList가 편집 중인지 확인
                var treeList = sender;
                var isEditing = false;
                
                try
                {
                    var editorProperty = treeList.GetType().GetProperty("ActiveEditor");
                    if (editorProperty != null)
                    {
                        var activeEditor = editorProperty.GetValue(treeList);
                        isEditing = activeEditor != null;
                    }
                }
                catch
                {
                    // 편집 상태 확인 실패 시 편집 중이 아닌 것으로 간주
                }
                
                // 키 이벤트 처리
                var eventType = e.GetType();
                var modifiersProperty = eventType.GetProperty("Modifiers") ?? eventType.GetProperty("Control");
                var keyCodeProperty = eventType.GetProperty("KeyCode") ?? eventType.GetProperty("KeyData");
                var handledProperty = eventType.GetProperty("Handled");
                
                if (keyCodeProperty != null && handledProperty != null)
                {
                    var keyCode = keyCodeProperty.GetValue(e);
                    string keyName = keyCode?.ToString() ?? "";
                    
                    bool isControl = false;
                    if (modifiersProperty != null)
                    {
                        var modifiers = modifiersProperty.GetValue(e);
                        isControl = modifiers?.ToString().Contains("Control") == true;
                    }
                    
                    // 키 처리 (편집 중이 아니고 다른 작업 중이 아닐 때만)
                    if (!isEditing && !_isDeleting && !_isPasting && !_isUndoing)
                    {
                        // 정확한 키 코드 확인 (숫자 키 코드 사용)
                        var keyCodeValue = keyCode?.ToString() ?? "";
                        
                        // 수정 키들(Ctrl, Alt, Shift)은 무시
                        if (keyCodeValue == "17" || keyCodeValue == "18" || keyCodeValue == "16" || // Ctrl, Alt, Shift
                            keyName == "ControlKey" || keyName == "Menu" || keyName == "ShiftKey" ||
                            keyName == "Control" || keyName == "Alt" || keyName == "Shift")
                        {
                            return; // 수정 키는 처리하지 않음
                        }
                        
                        if (isControl && (keyCodeValue == "67" || keyName == "C")) // Ctrl+C (정확한 C키)
                        {
                            if (View.SelectedObjects.Count > 0)
                            {
                                // 직접 복사 실행
                                copiedNodes = View.SelectedObjects.Cast<BOMItem>().ToList();
                                
                                // 복사 완료 메시지
                                var itemNames = string.Join(", ", copiedNodes.Select(x => x.ItemName).Take(3));
                                if (copiedNodes.Count > 3)
                                    itemNames += $" 외 {copiedNodes.Count - 3}개";
                                    
                                Application.ShowViewStrategy.ShowMessage(
                                    $"[Ctrl+C] 복사 완료: {copiedNodes.Count}개 항목\n({itemNames})", 
                                    InformationType.Success);
                                    
                                handledProperty.SetValue(e, true);
                            }
                            else
                            {
                                Application.ShowViewStrategy.ShowMessage("[Ctrl+C] 복사할 항목을 선택해주세요.", InformationType.Warning);
                                handledProperty.SetValue(e, true);
                            }
                        }
                        else if (isControl && (keyCodeValue == "86" || keyName == "V")) // Ctrl+V (정확한 V키)
                        {
                            if (copiedNodes.Count > 0)
                            {
                                // 직접 붙여넣기 실행
                                ExecuteDirectPaste();
                                handledProperty.SetValue(e, true);
                            }
                        }
                        else if (keyCodeValue == "46" || keyName == "Delete") // Delete 키 (정확한 Delete키)
                        {
                            if (View.SelectedObjects.Count > 0)
                            {
                                // 편집 중이면 편집 먼저 종료
                                try
                                {
                                    var treeListControl = sender;
                                    var closeEditorMethod = treeListControl.GetType().GetMethod("CloseEditor");
                                    closeEditorMethod?.Invoke(treeListControl, null);
                                }
                                catch { }
                                
                                // 직접 삭제 실행 (액션 우회)
                                ExecuteDirectDelete();
                                handledProperty.SetValue(e, true);
                            }
                        }
                        else if (isControl && (keyCodeValue == "90" || keyName == "Z")) // Ctrl+Z (정확한 Z키)
                        {
                            // 직접 되돌리기 실행
                            ExecuteDirectUndo();
                            handledProperty.SetValue(e, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 키보드 이벤트 처리 실패 시 무시
            }
        }

        private void View_SelectionChanged(object sender, EventArgs e)
        {
            UpdateActionStates();
            UpdateModeIndicator(); // 선택 변경 시 모드 확인
        }

        private void View_ControlsCreated(object sender, EventArgs e)
        {
            UpdateActionStates();
            UpdateModeIndicator(); // 컨트롤 생성 시 모드 확인
        }

        private void UpdateActionStates()
        {
            selectItemAction.Enabled["Always"] = true;
            addChildItemAction.Enabled["HasSelection"] = View.SelectedObjects.Count == 1;
            pasteAction.Enabled["HasCopiedNodes"] = copiedNodes.Count > 0;
            copyAction.Enabled["HasSelection"] = View.SelectedObjects.Count > 0;
            deleteAction.Enabled["HasSelection"] = View.SelectedObjects.Count > 0;
        }

        // 복사 기능
        private void CopyAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            if (View.SelectedObjects.Count > 0)
            {
                copiedNodes = View.SelectedObjects.Cast<BOMItem>().ToList();
                
                // 복사 완료 메시지
                var itemNames = string.Join(", ", copiedNodes.Select(x => x.ItemName).Take(3));
                if (copiedNodes.Count > 3)
                    itemNames += $" 외 {copiedNodes.Count - 3}개";
                    
                Application.ShowViewStrategy.ShowMessage(
                    $"복사 완료: {copiedNodes.Count}개 항목이 복사되었습니다.\n({itemNames})", 
                    InformationType.Success);
            }
            else
            {
                Application.ShowViewStrategy.ShowMessage("복사할 항목을 선택해주세요.", InformationType.Warning);
            }
        }

        private bool _isPasting = false; // 붙여넣기 중 플래그
        
        // 붙여넣기 기능
        private void PasteAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            if (_isPasting) 
            {
                Application.ShowViewStrategy.ShowMessage("이미 붙여넣기 작업이 진행 중입니다.", InformationType.Warning);
                return;
            }
            
            if (copiedNodes.Count == 0) 
            {
                Application.ShowViewStrategy.ShowMessage("복사된 항목이 없습니다. 먼저 항목을 복사해주세요.", InformationType.Info);
                return;
            }
            
            // 현재 선택된 노드(부모) 지정
            BOMItem parent = View.CurrentObject as BOMItem;
            
            try
            {
                _isPasting = true;
                int successCount = 0;
                int blockedCount = 0;
                var blockedReasons = new List<string>();
                
                foreach (var originalItem in copiedNodes)
                {
                    // 자기 자신을 자신의 하위로 붙여넣기 방지
                    if (IsValidPasteTarget(originalItem, parent))
                    {
                        CreateBOMItemCopy(originalItem, parent);
                        successCount++;
                    }
                    else
                    {
                        blockedCount++;
                        var reason = GetPasteBlockedReason(originalItem, parent);
                        if (!blockedReasons.Contains(reason))
                            blockedReasons.Add(reason);
                    }
                }
                
                // 붙여넣기 결과 메시지
                var message = "";
                if (successCount > 0)
                {
                    // 실제로 붙여넣어진 항목들의 이름 표시
                    var pastedItemNames = copiedNodes.Where(x => IsValidPasteTarget(x, parent))
                                                   .Select(x => x.ItemName)
                                                   .Take(3);
                    var itemNamesText = string.Join(", ", pastedItemNames);
                    if (successCount > 3)
                        itemNamesText += $" 외 {successCount - 3}개";
                    
                    message += $"붙여넣기 완료: {successCount}개 항목\n({itemNamesText})";
                    if (parent != null)
                        message += $"\n→ 부모: {parent.ItemName}";
                    else
                        message += "\n→ 위치: 최상위";
                }
                
                if (blockedCount > 0)
                {
                    if (successCount > 0) message += "\n\n";
                    message += $"차단된 항목: {blockedCount}개\n사유: {string.Join(", ", blockedReasons)}";
                }
                
                var messageType = successCount > 0 ? InformationType.Success : InformationType.Warning;
                Application.ShowViewStrategy.ShowMessage(message, messageType);
                
                // View.Refresh() 제거 - 자동으로 업데이트됨
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage($"붙여넣기 중 오류가 발생했습니다: {ex.Message}", InformationType.Error);
            }
            finally
            {
                _isPasting = false;
            }
        }

        private bool _isDeleting = false; // 삭제 중 플래그
        
        // 직접 삭제 실행 (키보드 이벤트용)
        private void ExecuteDirectDelete()
        {
            if (_isDeleting) return; // 삭제 중이면 무시
            
            var selected = View.SelectedObjects.Cast<BOMItem>().ToList();
            if (selected.Count == 0) return;
            
            try
            {
                _isDeleting = true;
                
                // 삭제 실행
                foreach (var item in selected)
                {
                    View.ObjectSpace.Delete(item);
                }
            }
            catch
            {
                // 오류 발생 시 무시
            }
            finally
            {
                _isDeleting = false;
            }
        }
        
        // 직접 붙여넣기 실행 (키보드 이벤트용)
        private void ExecuteDirectPaste()
        {
            if (_isPasting) 
            {
                Application.ShowViewStrategy.ShowMessage("[Ctrl+V] 이미 붙여넣기 작업이 진행 중입니다.", InformationType.Warning);
                return;
            }
            
            if (copiedNodes.Count == 0) 
            {
                Application.ShowViewStrategy.ShowMessage("[Ctrl+V] 복사된 항목이 없습니다. 먼저 Ctrl+C로 복사해주세요.", InformationType.Info);
                return;
            }
            
            BOMItem parent = View.CurrentObject as BOMItem;
            
            try
            {
                _isPasting = true;
                int successCount = 0;
                int blockedCount = 0;
                var blockedReasons = new List<string>();
                
                foreach (var originalItem in copiedNodes)
                {
                    // 자기 자신을 자신의 하위로 붙여넣기 방지
                    if (IsValidPasteTarget(originalItem, parent))
                    {
                        CreateBOMItemCopy(originalItem, parent);
                        successCount++;
                    }
                    else
                    {
                        blockedCount++;
                        var reason = GetPasteBlockedReason(originalItem, parent);
                        if (!blockedReasons.Contains(reason))
                            blockedReasons.Add(reason);
                    }
                }
                
                // 붙여넣기 결과 메시지
                var message = "";
                if (successCount > 0)
                {
                    // 실제로 붙여넣어진 항목들의 이름 표시
                    var pastedItemNames = copiedNodes.Where(x => IsValidPasteTarget(x, parent))
                                                   .Select(x => x.ItemName)
                                                   .Take(3);
                    var itemNamesText = string.Join(", ", pastedItemNames);
                    if (successCount > 3)
                        itemNamesText += $" 외 {successCount - 3}개";
                    
                    message += $"[Ctrl+V] 붙여넣기 완료: {successCount}개 항목\n({itemNamesText})";
                    if (parent != null)
                        message += $"\n→ 부모: {parent.ItemName}";
                    else
                        message += "\n→ 위치: 최상위";
                }
                
                if (blockedCount > 0)
                {
                    if (successCount > 0) message += "\n\n";
                    message += $"차단된 항목: {blockedCount}개\n사유: {string.Join(", ", blockedReasons)}";
                }
                
                var messageType = successCount > 0 ? InformationType.Success : InformationType.Warning;
                Application.ShowViewStrategy.ShowMessage(message, messageType);
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage($"[Ctrl+V] 붙여넣기 중 오류: {ex.Message}", InformationType.Error);
            }
            finally
            {
                _isPasting = false;
            }
        }
        
        // 직접 되돌리기 실행 (키보드 이벤트용)
        private void ExecuteDirectUndo()
        {
            if (_isUndoing) return;
            
            try
            {
                _isUndoing = true;
                View.ObjectSpace.Rollback();
            }
            catch
            {
                // 오류 발생 시 무시
            }
            finally
            {
                _isUndoing = false;
            }
        }
        
        // 삭제 기능 (BOMTREE 패턴: 간단한 ObjectSpace.Delete 사용)
        private void DeleteAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            if (_isDeleting) return; // 삭제 중이면 무시
            
            var selected = View.SelectedObjects.Cast<BOMItem>().ToList();
            if (selected.Count == 0)
            {
                return;
            }
            
            try
            {
                _isDeleting = true; // 삭제 시작
                
                // 편집 모드 종료 (TreeList가 편집 중이면 먼저 종료)
                var editorTypeName = View.Editor?.GetType().Name ?? "";
                if (editorTypeName.Contains("TreeList"))
                {
                    var treeListProperty = View.Editor.GetType().GetProperty("TreeList");
                    if (treeListProperty != null)
                    {
                        var treeList = treeListProperty.GetValue(View.Editor);
                        if (treeList != null)
                        {
                            try
                            {
                                var closeEditorMethod = treeList.GetType().GetMethod("CloseEditor");
                                closeEditorMethod?.Invoke(treeList, null);
                            }
                            catch { }
                        }
                    }
                }
                
                // 삭제 실행
                foreach (var item in selected)
                {
                    View.ObjectSpace.Delete(item);
                }
                
                // View.Refresh() 제거 - 자동으로 업데이트됨
            }
            catch (Exception ex)
            {
                // 오류 발생 시 무시
            }
            finally
            {
                _isDeleting = false; // 삭제 완료
            }
        }

        private bool _isUndoing = false; // 되돌리기 중 플래그
        
        // Undo 기능 (BOMTREE 패턴: ObjectSpace.Rollback 사용)
        private void UndoAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            if (_isUndoing) return;
            
            try
            {
                _isUndoing = true;
                View.ObjectSpace.Rollback();
                // View.Refresh() 제거 - Rollback이 자동으로 UI 업데이트
            }
            catch (Exception ex)
            {
                // 오류 발생 시 무시
            }
            finally
            {
                _isUndoing = false;
            }
        }



        // 트리 새로고침 (모드 표시기 겸용)
        private void RefreshTreeAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            View.Refresh();
            UpdateModeIndicator();
        }
        
        // 모드 표시기 업데이트
        private void UpdateModeIndicator()
        {
            try
            {
                bool isEditMode = IsInEditMode();
                
                if (isEditMode)
                {
                    // 편집 모드 표시
                    refreshTreeAction.Caption = "🔧 편집 중";
                    refreshTreeAction.ToolTip = "편집 모드 - ESC로 취소, Enter로 확정, 클릭하면 새로고침";
                }
                else
                {
                    // 선택 모드 표시
                    refreshTreeAction.Caption = "트리새로고침";
                    refreshTreeAction.ToolTip = "선택 모드 - 더블클릭으로 편집, F2로 편집, 클릭하면 새로고침";
                }
            }
            catch { }
        }
        
        // 현재 편집 모드인지 확인
        private bool IsInEditMode()
        {
            try
            {
                var editorTypeName = View.Editor?.GetType().Name ?? "";
                if (editorTypeName.Contains("TreeList"))
                {
                    var treeListProperty = View.Editor.GetType().GetProperty("TreeList");
                    if (treeListProperty != null)
                    {
                        var treeList = treeListProperty.GetValue(View.Editor);
                        if (treeList != null)
                        {
                            var editorProperty = treeList.GetType().GetProperty("ActiveEditor");
                            if (editorProperty != null)
                            {
                                var activeEditor = editorProperty.GetValue(treeList);
                                return activeEditor != null;
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }
        
        // 붙여넣기 대상 유효성 검사
        private bool IsValidPasteTarget(BOMItem originalItem, BOMItem targetParent)
        {
            // null 체크
            if (originalItem == null) return false;
            
            // 1. 자기 자신을 자신의 하위로 붙여넣기 방지
            if (targetParent != null && originalItem.Oid == targetParent.Oid)
            {
                return false; // 자기 자신에게 붙여넣기 금지
            }
            
            // 2. 자신의 하위 노드에 붙여넣기 방지 (순환 구조 방지)
            if (targetParent != null && IsDescendantOf(targetParent, originalItem))
            {
                return false; // 자신의 하위에 붙여넣기 금지
            }
            
            // 3. 동일한 Item을 같은 부모 하위에 중복 추가 방지
            if (targetParent != null && HasSameItemInChildren(targetParent, originalItem.Item))
            {
                return false; // 중복 Item 방지
            }
            
            return true; // 유효한 붙여넣기 대상
        }
        
        // 대상이 원본의 하위 노드인지 확인
        private bool IsDescendantOf(BOMItem potentialDescendant, BOMItem ancestor)
        {
            if (potentialDescendant?.Parent == null) return false;
            
            var current = potentialDescendant.Parent;
            var visited = new HashSet<Guid>(); // 순환 참조 방지
            
            while (current != null && !visited.Contains(current.Oid))
            {
                visited.Add(current.Oid);
                
                if (current.Oid == ancestor.Oid)
                {
                    return true; // 하위 노드임
                }
                
                current = current.Parent;
            }
            
            return false;
        }
        
        // 같은 Item이 이미 하위에 있는지 확인
        private bool HasSameItemInChildren(BOMItem parent, Item item)
        {
            if (parent?.Children == null || item == null) return false;
            
            try
            {
                foreach (BOMItem child in parent.Children)
                {
                    if (child?.Item != null && child.Item.Oid == item.Oid)
                    {
                        return true; // 같은 Item이 이미 존재
                    }
                }
            }
            catch
            {
                // Children 접근 오류 시 false 반환
            }
            
            return false;
        }
        
        // 붙여넣기 차단 사유 반환
        private string GetPasteBlockedReason(BOMItem originalItem, BOMItem targetParent)
        {
            if (originalItem == null) return "유효하지 않은 항목";
            
            // 1. 자기 자신을 자신의 하위로 붙여넣기 방지
            if (targetParent != null && originalItem.Oid == targetParent.Oid)
            {
                return "자기 자신에게 붙여넣기 불가";
            }
            
            // 2. 자신의 하위 노드에 붙여넣기 방지 (순환 구조 방지)
            if (targetParent != null && IsDescendantOf(targetParent, originalItem))
            {
                return "하위 노드에 상위 노드 붙여넣기 불가";
            }
            
            // 3. 동일한 Item을 같은 부모 하위에 중복 추가 방지
            if (targetParent != null && HasSameItemInChildren(targetParent, originalItem.Item))
            {
                return "동일 품목 중복 추가 불가";
            }
            
            return "알 수 없는 이유";
        }
        
        // BOMItem 복사본 생성 (무한루프 방지)
        private BOMItem CreateBOMItemCopy(BOMItem original, BOMItem parent)
        {
            return CreateBOMItemCopy(original, parent, new HashSet<Guid>(), 0);
        }
        
        // BOMItem 복사본 생성 (방문한 노드 추적, 깊이 제한)
        private BOMItem CreateBOMItemCopy(BOMItem original, BOMItem parent, HashSet<Guid> visitedNodes, int depth)
        {
            // 깊이 제한 (최대 10레벨)
            if (depth > 10)
            {
                return null; // 깊이 초과 시 중단
            }
            
            // 이미 방문한 노드면 무한루프 방지
            if (visitedNodes.Contains(original.Oid))
            {
                return null; // 순환 참조 차단
            }
            
            // null 체크
            if (original?.Item == null)
            {
                return null;
            }
            
            // 현재 노드를 방문 목록에 추가
            visitedNodes.Add(original.Oid);
            
            var newItem = View.ObjectSpace.CreateObject<BOMItem>();
            newItem.Item = original.Item;
            newItem.Quantity = original.Quantity;
            newItem.Parent = parent;
            
            // 하위 아이템들도 재귀적으로 복사 (순환 참조 체크, 깊이 증가)
            try
            {
                if (original.Children != null)
                {
                    foreach (BOMItem child in original.Children)
                    {
                        if (child != null && !visitedNodes.Contains(child.Oid))
                        {
                            var copiedChild = CreateBOMItemCopy(child, newItem, visitedNodes, depth + 1);
                            // copiedChild가 null이면 순환 참조나 깊이 초과로 인해 건너뛴 것
                        }
                    }
                }
            }
            catch
            {
                // Children 접근 오류 시 무시
            }
            
            // 현재 노드를 방문 목록에서 제거 (다른 경로에서 재사용 가능)
            visitedNodes.Remove(original.Oid);
            
            return newItem;
        }

        // 품목선택 팝업 설정
        private void SelectItemAction_CustomizePopupWindowParams(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var lookupObjectSpace = Application.CreateObjectSpace(typeof(Item));
            var parentBOMItem = View.CurrentObject as BOMItem;
            CriteriaOperator criteria = null;
            string caption = "";
            
            if (parentBOMItem != null)
            {
                var allowedItemTypes = GetAllowedChildItemTypes(parentBOMItem.Item.ItemType);
                
                if (allowedItemTypes.Count == 0)
                {
                    caption = $"'{GetItemTypeDisplayName(parentBOMItem.Item.ItemType)}' 품목 하위에는 다른 품목을 추가할 수 없습니다.";
                    criteria = CriteriaOperator.Parse("1=0");
                }
                else
                {
                    var typeFilters = allowedItemTypes.Select(itemType => 
                        CriteriaOperator.Parse("ItemType = ?", (int)itemType)).ToArray();
                    criteria = CriteriaOperator.Or(typeFilters);
                    
                    var allowedTypeNames = allowedItemTypes.Select(GetItemTypeDisplayName);
                    caption = $"하위 품목 선택 (부모: {parentBOMItem.ItemName}) - 허용타입: {string.Join(", ", allowedTypeNames)}";
                }
            }
            else
            {
                caption = "품목 선택 (최상위 추가)";
                criteria = CriteriaOperator.Parse("IsActive = True");
            }
            
            var itemsListView = Application.CreateListView(lookupObjectSpace, typeof(Item), false);
            itemsListView.Caption = caption;
            
            if (criteria != null)
            {
                ((DevExpress.ExpressApp.ListView)itemsListView).CollectionSource.Criteria["ItemTypeFilter"] = criteria;
            }
            
            e.View = itemsListView;
            e.DialogController.SaveOnAccept = false;
            e.DialogController.AcceptAction.Caption = "선택";
            e.DialogController.CancelAction.Caption = "취소";
        }

        private void AddChildItemAction_CustomizePopupWindowParams(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            SelectItemAction_CustomizePopupWindowParams(sender, e);
        }
        
        private List<ItemType> GetAllowedChildItemTypes(ItemType parentItemType)
        {
            return parentItemType switch
            {
                ItemType.완제품 => new List<ItemType> { ItemType.반제품, ItemType.원재료, ItemType.부자재 },
                ItemType.반제품 => new List<ItemType> { ItemType.원재료, ItemType.부자재 },
                ItemType.원재료 => new List<ItemType>(),
                ItemType.부자재 => new List<ItemType>(),
                _ => new List<ItemType>()
            };
        }
        
        private string GetItemTypeDisplayName(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.완제품 => "완제품",
                ItemType.반제품 => "반제품", 
                ItemType.원재료 => "원재료",
                ItemType.부자재 => "부자재",
                _ => "기타"
            };
        }

        // 품목 선택 실행
        private void SelectItemAction_Execute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var selectedItem = e.PopupWindowView.CurrentObject as Item;
            var parentBOMItem = View.CurrentObject as BOMItem;
            
            if (selectedItem != null)
            {
                AddItemToBOM(selectedItem.Oid, parentBOMItem?.Oid);
            }
        }

        private void AddChildItemAction_Execute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            SelectItemAction_Execute(sender, e);
        }

        // BOM에 품목 추가
        private void AddItemToBOM(Guid itemOid, Guid? parentBOMItemOid)
        {
            try
            {
                var objectSpace = View.ObjectSpace;
                var item = objectSpace.GetObjectByKey<Item>(itemOid);
                
                if (item == null)
                {
                    Application.ShowViewStrategy.ShowMessage("선택된 품목을 찾을 수 없습니다.", InformationType.Error);
                    return;
                }

                // 기존 BOM 구조가 있는지 확인하고 복사
                var existingBOMItems = GetExistingBOMStructure(item);
                
                if (existingBOMItems.Any())
                {
                    // 기존 BOM 구조 자동 복사 (질문 없이)
                    CopyExistingBOMStructure(objectSpace, parentBOMItemOid, item);
                    Application.ShowViewStrategy.ShowMessage($"'{item.ItemName}'의 BOM 구조가 복사되었습니다.", InformationType.Success);
                }
                else
                {
                    CreateSingleBOMItem(objectSpace, item, parentBOMItemOid);
                    Application.ShowViewStrategy.ShowMessage($"'{item.ItemName}'이(가) 추가되었습니다.", InformationType.Success);
                }
                
                View.Refresh();
                UpdateActionStates();
            }
            catch (Exception ex)
            {
                Application.ShowViewStrategy.ShowMessage($"품목 추가 중 오류가 발생했습니다: {ex.Message}", InformationType.Error);
            }
        }

        // 단일 BOMItem 생성
        private BOMItem CreateSingleBOMItem(IObjectSpace objectSpace, Item item, Guid? parentBOMItemOid)
        {
            var newBOMItem = objectSpace.CreateObject<BOMItem>();
            newBOMItem.Item = item;
            newBOMItem.Quantity = 1;
            
            if (parentBOMItemOid.HasValue)
            {
                var parentBOMItem = objectSpace.GetObjectByKey<BOMItem>(parentBOMItemOid.Value);
                newBOMItem.Parent = parentBOMItem;
            }
            
            return newBOMItem;
        }

        // 기존 BOM 구조 조회
        private List<BOMItem> GetExistingBOMStructure(Item item)
        {
            var objectSpace = View.ObjectSpace;
            var existingBOMs = objectSpace.GetObjects<BOMItem>(
                CriteriaOperator.Parse("Item.Oid = ? AND Parent IS NULL", item.Oid))
                .ToList();
            
            var allItems = new List<BOMItem>();
            foreach (var rootItem in existingBOMs)
            {
                allItems.Add(rootItem);
                allItems.AddRange(GetAllChildren(rootItem));
            }
            
            return allItems;
        }

        // 기존 BOM 구조 복사
        private void CopyExistingBOMStructure(IObjectSpace objectSpace, Guid? parentBOMItemOid, Item rootItem)
        {
            var existingBOMs = GetExistingBOMStructure(rootItem);
            var bomItemMapping = new Dictionary<Guid, BOMItem>();
            
            var sortedBOMs = existingBOMs.OrderBy(b => b.Level).ToList();
            
            foreach (var originalBOM in sortedBOMs)
            {
                var newBOMItem = objectSpace.CreateObject<BOMItem>();
                newBOMItem.Item = originalBOM.Item;
                newBOMItem.Quantity = originalBOM.Quantity ?? 1;
                
                if (originalBOM.Parent != null && bomItemMapping.ContainsKey(originalBOM.Parent.Oid))
                {
                    newBOMItem.Parent = bomItemMapping[originalBOM.Parent.Oid];
                }
                else if (originalBOM.Parent == null && parentBOMItemOid.HasValue)
                {
                    var parentBOMItem = objectSpace.GetObjectByKey<BOMItem>(parentBOMItemOid.Value);
                    newBOMItem.Parent = parentBOMItem;
                }
                
                bomItemMapping[originalBOM.Oid] = newBOMItem;
            }
        }

        // 모든 하위 노드 가져오기
        private List<BOMItem> GetAllChildren(BOMItem parent)
        {
            var children = new List<BOMItem>();
            foreach (BOMItem child in parent.Children)
            {
                children.Add(child);
                children.AddRange(GetAllChildren(child));
            }
            return children;
        }
    }
} 







