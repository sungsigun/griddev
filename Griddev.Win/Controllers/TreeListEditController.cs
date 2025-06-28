using DevExpress.ExpressApp;
using DevExpress.ExpressApp.TreeListEditors.Win;
using DevExpress.XtraTreeList;
using DevExpress.XtraTreeList.Columns;
using DevExpress.XtraTreeList.Nodes;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraEditors;
using Griddev.Module.BusinessObjects;
using System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;

namespace Griddev.Win.Controllers
{
    /// <summary>
    /// TreeList에서 강제로 인라인 편집을 활성화하는 컨트롤러
    /// </summary>
    public class TreeListEditController : ViewController<ListView>
    {
        public TreeListEditController()
        {
            TargetObjectType = typeof(BOMItem);
            TargetViewType = ViewType.ListView;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            View.ControlsCreated += View_ControlsCreated;
        }

        protected override void OnDeactivated()
        {
            View.ControlsCreated -= View_ControlsCreated;
            
            // TreeList 이벤트 핸들러 정리
            if (View.Editor is TreeListEditor treeListEditor)
            {
                var treeList = treeListEditor.TreeList;
                if (treeList != null)
                {
                    try
                    {
                        treeList.CellValueChanged -= TreeList_CellValueChanged;
                        treeList.FocusedNodeChanged -= TreeList_FocusedNodeChanged;
                    }
                    catch
                    {
                        // 이벤트 핸들러 정리 실패 시 무시
                    }
                }
            }
            
            base.OnDeactivated();
        }

        private void View_ControlsCreated(object sender, System.EventArgs e)
        {
            if (View.Editor is TreeListEditor treeListEditor)
            {
                var treeList = treeListEditor.TreeList;
                if (treeList != null)
                {
                    // TreeList 편집 모드 강제 활성화
                    SetupTreeListEditing(treeList);
                }
            }
        }

        private void SetupTreeListEditing(TreeList treeList)
        {
            try
            {
                // 편집 모드 설정
                treeList.OptionsBehavior.Editable = true;
                treeList.OptionsBehavior.ReadOnly = false;
                treeList.OptionsBehavior.AllowIncrementalSearch = false;
                treeList.OptionsBehavior.EditingMode = TreeListEditingMode.Default;

                // Quantity 컬럼에 SpinEdit 설정
                var quantityColumn = treeList.Columns["Quantity"];
                if (quantityColumn != null)
                {
                    var spinEdit = new RepositoryItemSpinEdit();
                    spinEdit.MinValue = 0;
                    spinEdit.MaxValue = 9999;
                    spinEdit.Increment = 0.1m;
                    spinEdit.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
                    spinEdit.DisplayFormat.FormatString = "f2";
                    
                    quantityColumn.ColumnEdit = spinEdit;
                }

                // 편집 이벤트 핸들러
                treeList.CellValueChanged += TreeList_CellValueChanged;
                
                // 포커스 변경 시 안전하게 편집 종료
                treeList.FocusedNodeChanged += TreeList_FocusedNodeChanged;
                
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"TreeList 편집 설정 오류: {ex.Message}", "오류");
            }
        }

        private bool _isUpdating = false; // 무한 루프 방지 플래그
        
        private void TreeList_CellValueChanged(object sender, CellValueChangedEventArgs e)
        {
            if (e.Column.FieldName == "Quantity" && !_isUpdating)
            {
                try
                {
                    _isUpdating = true; // 무한 루프 방지
                    
                    if (decimal.TryParse(e.Value?.ToString(), out decimal newQuantity))
                    {
                        // 방법 1: TreeList의 GetDataRecordByNode로 실제 객체 찾기
                        var treeList = sender as TreeList;
                        var dataRecord = treeList?.GetDataRecordByNode(e.Node);
                        
                        if (dataRecord is BOMItem bomItem)
                        {
                            // XPO 객체 직접 수정
                            bomItem.Quantity = newQuantity;
                            
                            // 강제로 ObjectSpace에 변경 알림
                            View.ObjectSpace.SetModified(bomItem);
                            return;
                        }
                        
                        // 방법 2: Node의 Tag 속성 사용
                        if (e.Node.Tag is BOMItem tagBomItem)
                        {
                            tagBomItem.Quantity = newQuantity;
                            View.ObjectSpace.SetModified(tagBomItem);
                            return;
                        }
                        
                        // 방법 3: 현재 선택된 객체 사용
                        var currentObject = View.CurrentObject as BOMItem;
                        if (currentObject != null)
                        {
                            currentObject.Quantity = newQuantity;
                            View.ObjectSpace.SetModified(currentObject);
                            return;
                        }
                        
                        // 방법 4: CollectionSource에서 찾기 (마지막 수단)
                        var collection = View.CollectionSource.Collection as System.Collections.IEnumerable;
                        var allObjects = collection?.Cast<BOMItem>().ToList();
                        
                        if (allObjects != null && e.Node.Id >= 0 && e.Node.Id < allObjects.Count)
                        {
                            var targetObject = allObjects[e.Node.Id];
                            targetObject.Quantity = newQuantity;
                            View.ObjectSpace.SetModified(targetObject);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    // 편집 중 오류 발생 시 무시
                }
                finally
                {
                    _isUpdating = false; // 플래그 해제
                }
            }
        }

        private void TreeList_FocusedNodeChanged(object sender, FocusedNodeChangedEventArgs e)
        {
            if (_isUpdating) return; // 업데이트 중이면 무시
            
            try
            {
                var treeList = sender as TreeList;
                if (treeList != null)
                {
                    // 편집 중이면 안전하게 종료
                    if (treeList.ActiveEditor != null)
                    {
                        try
                        {
                            treeList.CloseEditor();
                        }
                        catch
                        {
                            // 편집 종료 실패 시 무시
                        }
                    }
                }
            }
            catch
            {
                // 포커스 변경 처리 실패 시 무시
            }
        }
    }
} 