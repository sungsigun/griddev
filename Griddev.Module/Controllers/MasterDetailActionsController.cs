using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.Persistent.Base;
using Griddev.Module.BusinessObjects;
using System.ComponentModel;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Templates;
using DevExpress.ExpressApp.Utils;
using DevExpress.Xpo;
using System.Reflection;
using System.Linq;

namespace Griddev.Module.Controllers
{
    // OrderViewController와 동일한 패턴으로 구현
    public partial class MasterDetailActionsController : ViewController<DevExpress.ExpressApp.ListView>
    {
        private SimpleAction smartDeleteAction;
        private SimpleAction splitHorizontalAction;
        private SimpleAction splitVerticalAction;
        private SimpleAction splitPosition30Action;
        private SimpleAction splitPosition50Action;
        private SimpleAction splitPosition70Action;

        // XAF 기본 Refresh 액션을 안전하게 처리
        private RefreshController refreshController;

        public MasterDetailActionsController()
        {
            CreateActions();
        }

        private void CreateActions()
        {
            // 🗑️ 스마트 삭제 액션
            smartDeleteAction = new SimpleAction(this, "SmartDelete", PredefinedCategory.Edit)
            {
                Caption = "스마트 삭제",
                ToolTip = "포커스된 그리드에 따라 마스터 또는 디테일 항목을 삭제합니다",
                ImageName = "Action_Delete"
            };
            smartDeleteAction.Execute += SmartDeleteAction_Execute;

            // ↔ 세로 분할 액션
            splitVerticalAction = new SimpleAction(this, "SplitVertical", PredefinedCategory.Edit)
            {
                Caption = "세로 분할",
                ToolTip = "Master-Detail을 세로로 분할합니다",
                ImageName = "SplitVertical"
            };
            splitVerticalAction.Execute += SplitVerticalAction_Execute;

            // ↕ 가로 분할 액션
            splitHorizontalAction = new SimpleAction(this, "SplitHorizontal", PredefinedCategory.Edit)
            {
                Caption = "가로 분할",
                ToolTip = "Master-Detail을 가로로 분할합니다",
                ImageName = "SplitHorizontal"
            };
            splitHorizontalAction.Execute += SplitHorizontalAction_Execute;

            // 30% 분할 위치 액션
            splitPosition30Action = new SimpleAction(this, "SplitPosition30", PredefinedCategory.Edit)
            {
                Caption = "30%",
                ToolTip = "분할 위치를 30%로 설정합니다"
            };
            splitPosition30Action.Execute += (s, e) => SetSplitterPosition(30);

            // 50% 분할 위치 액션
            splitPosition50Action = new SimpleAction(this, "SplitPosition50", PredefinedCategory.Edit)
            {
                Caption = "50%",
                ToolTip = "분할 위치를 50%로 설정합니다"
            };
            splitPosition50Action.Execute += (s, e) => SetSplitterPosition(50);

            // 70% 분할 위치 액션
            splitPosition70Action = new SimpleAction(this, "SplitPosition70", PredefinedCategory.Edit)
            {
                Caption = "70%",
                ToolTip = "분할 위치를 70%로 설정합니다"
            };
            splitPosition70Action.Execute += (s, e) => SetSplitterPosition(70);
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            
            // MasterDetailListEditor를 사용하는 ListView에서만 활성화
            UpdateActionsVisibility();

            // RefreshController 찾기 및 이벤트 핸들러 등록
            refreshController = Frame.GetController<RefreshController>();
            if (refreshController != null)
            {
                refreshController.RefreshAction.Executing += RefreshAction_Executing;
                refreshController.RefreshAction.Executed += RefreshAction_Executed;
            }
        }

        protected override void OnViewControlsCreated()
        {
            base.OnViewControlsCreated();
            UpdateActionsVisibility();
        }

        private void UpdateActionsVisibility()
        {
            bool isMasterDetailEditor = View?.Editor?.GetType().Name == "MasterDetailListEditor";
            
            smartDeleteAction.Active["MasterDetailEditor"] = isMasterDetailEditor;
            splitVerticalAction.Active["MasterDetailEditor"] = isMasterDetailEditor;
            splitHorizontalAction.Active["MasterDetailEditor"] = isMasterDetailEditor;
            splitPosition30Action.Active["MasterDetailEditor"] = isMasterDetailEditor;
            splitPosition50Action.Active["MasterDetailEditor"] = isMasterDetailEditor;
            splitPosition70Action.Active["MasterDetailEditor"] = isMasterDetailEditor;
            
            // 스마트 삭제 툴팁 업데이트
            if (isMasterDetailEditor)
            {
                UpdateSmartDeleteTooltip();
            }
        }

        private void SmartDeleteAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            try
            {
                // 현재 편집 중인 내용을 안전하게 종료
                SafeEndCurrentEdit();

                var focusedGrid = GetFocusedGrid();
                if (focusedGrid == null) return;

                bool isMasterGrid = focusedGrid.Item1;
                string tabName = focusedGrid.Item2;
                var gridView = focusedGrid.Item3;

                if (gridView == null) return;

                // Reflection을 통해 선택된 행들 가져오기
                var getSelectedRowsMethod = gridView.GetType().GetMethod("GetSelectedRows");
                var selectedRowHandles = getSelectedRowsMethod?.Invoke(gridView, null) as int[];
                
                if (selectedRowHandles == null || selectedRowHandles.Length == 0) return;

                var selectedItems = new List<object>();

                // 선택된 객체들 수집
                var getRowMethod = gridView.GetType().GetMethod("GetRow", new[] { typeof(int) });
                foreach (int rowHandle in selectedRowHandles)
                {
                    if (rowHandle >= 0) // 실제 데이터 행인지 확인
                    {
                        var item = getRowMethod?.Invoke(gridView, new object[] { rowHandle });
                        if (item != null)
                        {
                            selectedItems.Add(item);
                        }
                    }
                }

                if (selectedItems.Count == 0) return;

                // 삭제 확인 메시지 생성
                string message = CreateDeleteConfirmationMessage(selectedItems, isMasterGrid, tabName);
                
                var result = MessageBox.Show(message, "삭제 확인", 
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    // 선택된 아이템들 삭제
                    foreach (var item in selectedItems)
                    {
                        try
                        {
                            ObjectSpace.Delete(item);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"아이템 삭제 오류: {ex.Message}");
                        }
                    }

                    // 변경사항 저장
                    try
                    {
                        ObjectSpace.CommitChanges();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}", "오류", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"삭제 처리 중 오류가 발생했습니다: {ex.Message}", "오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 삭제 확인 메시지 생성
        private string CreateDeleteConfirmationMessage(List<object> selectedItems, bool isMasterGrid, string tabName)
        {
            if (selectedItems.Count == 0) return "";

            var firstItem = selectedItems[0];
            var itemTypeName = GetTypeDisplayName(firstItem.GetType());
            var displayName = GetItemDisplayName(firstItem);

            if (isMasterGrid)
            {
                if (selectedItems.Count == 1)
                {
                    return $"{itemTypeName} '{displayName}'을(를) 삭제하시겠습니까?\n관련된 모든 하위 항목도 함께 삭제됩니다.";
                }
                else
                {
                    return $"선택된 {selectedItems.Count}개의 {itemTypeName}을(를) 삭제하시겠습니까?\n관련된 모든 하위 항목도 함께 삭제됩니다.";
                }
            }
            else
            {
                var detailTypeName = string.IsNullOrEmpty(tabName) ? itemTypeName : tabName;
                if (selectedItems.Count == 1)
                {
                    return $"{detailTypeName} '{displayName}'을(를) 삭제하시겠습니까?";
                }
                else
                {
                    return $"선택된 {selectedItems.Count}개의 {detailTypeName}을(를) 삭제하시겠습니까?";
                }
            }
        }

        // 타입의 표시 이름 가져오기
        private string GetTypeDisplayName(Type type)
        {
            var typeTranslations = new Dictionary<string, string>
            {
                { typeof(OrderDetail).Name, "주문상세" },
                { typeof(Order).Name, "주문" },
                { "OrderHistory", "주문이력" },
                { "Employee", "직원" },
                { "Invoice", "송장" },
                { "Contact", "연락처" },
                { "Product", "제품" },
                { "Task", "작업" },
                { "Project", "프로젝트" }
            };
            
            return typeTranslations.ContainsKey(type.Name) ? typeTranslations[type.Name] : type.Name;
        }

        // 객체의 표시 이름 가져오기
        private string GetItemDisplayName(object item)
        {
            if (item == null) return "";
            
            var type = item.GetType();
            var properties = type.GetProperties();
            
            // 일반적인 표시 이름 속성들 우선순위대로 확인
            var displayProperties = new[] { "Name", "Title", "Code", "No", "ItemName", "OrderNo", "Description" };
            
            foreach (var propName in displayProperties)
            {
                var property = properties.FirstOrDefault(p => p.Name.Equals(propName, StringComparison.OrdinalIgnoreCase));
                if (property != null)
                {
                    var value = property.GetValue(item);
                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                    {
                        return value.ToString();
                    }
                }
            }
            
            // 표시할 속성이 없으면 ToString() 사용
            return item.ToString();
        }

        private bool GetControlFocus(object control)
        {
            try
            {
                var controlType = control.GetType();
                var focusedProperty = controlType.GetProperty("Focused");
                var containsFocusProperty = controlType.GetProperty("ContainsFocus");
                
                bool focused = (bool)(focusedProperty?.GetValue(control) ?? false);
                bool containsFocus = (bool)(containsFocusProperty?.GetValue(control) ?? false);
                
                return focused || containsFocus;
            }
            catch
            {
                return false;
            }
        }

        private object GetFocusedRow(object gridView)
        {
            try
            {
                var gridViewType = gridView.GetType();
                var getFocusedRowMethod = gridViewType.GetMethod("GetFocusedRow");
                return getFocusedRowMethod?.Invoke(gridView, null);
            }
            catch
            {
                return null;
            }
        }

        private List<Order> GetSelectedOrders(object gridView)
        {
            var selectedOrders = new List<Order>();
            try
            {
                var gridViewType = gridView.GetType();
                var getSelectedRowsMethod = gridViewType.GetMethod("GetSelectedRows");
                var getRowMethod = gridViewType.GetMethod("GetRow", new Type[] { typeof(int) });
                
                var selectedRowHandles = getSelectedRowsMethod?.Invoke(gridView, null) as int[];
                if (selectedRowHandles != null)
                {
                    foreach (int rowHandle in selectedRowHandles)
                    {
                        if (rowHandle >= 0) // 실제 데이터 행인지 확인
                        {
                            var order = getRowMethod?.Invoke(gridView, new object[] { rowHandle }) as Order;
                            if (order != null)
                            {
                                selectedOrders.Add(order);
                            }
                        }
                    }
                }
                
                // 선택된 행이 없으면 포커스된 행 사용
                if (selectedOrders.Count == 0)
                {
                    var focusedOrder = GetFocusedRow(gridView) as Order;
                    if (focusedOrder != null)
                    {
                        selectedOrders.Add(focusedOrder);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSelectedOrders 오류: {ex.Message}");
            }
            return selectedOrders;
        }

        private List<OrderDetail> GetSelectedOrderDetails(object gridView)
        {
            var selectedDetails = new List<OrderDetail>();
            try
            {
                var gridViewType = gridView.GetType();
                var getSelectedRowsMethod = gridViewType.GetMethod("GetSelectedRows");
                var getRowMethod = gridViewType.GetMethod("GetRow", new Type[] { typeof(int) });
                
                var selectedRowHandles = getSelectedRowsMethod?.Invoke(gridView, null) as int[];
                if (selectedRowHandles != null)
                {
                    foreach (int rowHandle in selectedRowHandles)
                    {
                        if (rowHandle >= 0) // 실제 데이터 행인지 확인
                        {
                            var detail = getRowMethod?.Invoke(gridView, new object[] { rowHandle }) as OrderDetail;
                            if (detail != null)
                            {
                                selectedDetails.Add(detail);
                            }
                        }
                    }
                }
                
                // 선택된 행이 없으면 포커스된 행 사용
                if (selectedDetails.Count == 0)
                {
                    var focusedDetail = GetFocusedRow(gridView) as OrderDetail;
                    if (focusedDetail != null)
                    {
                        selectedDetails.Add(focusedDetail);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSelectedOrderDetails 오류: {ex.Message}");
            }
            return selectedDetails;
        }

        private void DeleteOrder(Order order)
        {
            var result = MessageBox.Show(
                $"주문 '{order.OrderNo}'을(를) 삭제하시겠습니까?\n관련된 모든 상세 항목도 함께 삭제됩니다.",
                "주문 삭제 확인",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    ObjectSpace.Delete(order);
                    ObjectSpace.CommitChanges();
                    View.RefreshDataSource();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}", "오류", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DeleteOrderDetail(OrderDetail detail)
        {
            var result = MessageBox.Show(
                $"상세 항목 '{detail.ItemName}'을(를) 삭제하시겠습니까?",
                "상세 삭제 확인",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    var parentOrder = detail.Order;
                    ObjectSpace.Delete(detail);
                    ObjectSpace.CommitChanges();
                    
                    // 부모 주문의 총액 업데이트
                    parentOrder?.UpdateTotalAmount();
                    
                    // UI 새로고침
                    View.RefreshDataSource();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}", "오류", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SetSplitOrientation(Orientation orientation)
        {
            try
            {
                if (View is DevExpress.ExpressApp.ListView listView && 
                    listView.Editor?.GetType().Name == "MasterDetailListEditor")
                {
                    var editor = listView.Editor;
                    var editorType = editor.GetType();
                    
                    // Reflection을 사용하여 SplitContainer에 접근
                    var splitContainerField = editorType.GetField("splitContainer", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    var splitContainer = splitContainerField?.GetValue(editor);
                    if (splitContainer != null)
                    {
                        SetSplitContainerOrientation(splitContainer, orientation);
                        
                        // Model에 저장 (IModelMasterDetailListView 인터페이스 사용)
                        if (listView.Model != null)
                        {
                            try
                            {
                                var modelType = listView.Model.GetType();
                                var splitOrientationProperty = modelType.GetProperty("SplitOrientation");
                                splitOrientationProperty?.SetValue(listView.Model, orientation.ToString());
                            }
                            catch { }
                        }
                        
                        MessageBox.Show($"분할 방향을 {orientation}로 변경했습니다.", "분할 설정", 
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"분할 설정 중 오류가 발생했습니다: {ex.Message}", "오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetSplitterPosition(int percentage)
        {
            try
            {
                if (View is DevExpress.ExpressApp.ListView listView && 
                    listView.Editor?.GetType().Name == "MasterDetailListEditor")
                {
                    var editor = listView.Editor;
                    var editorType = editor.GetType();
                    
                    // Reflection을 사용하여 SplitContainer에 접근
                    var splitContainerField = editorType.GetField("splitContainer", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    var splitContainer = splitContainerField?.GetValue(editor);
                    if (splitContainer != null)
                    {
                        var totalSize = GetSplitContainerSize(splitContainer);
                            
                        if (totalSize > 0)
                        {
                            SetSplitContainerDistance(splitContainer, (int)(totalSize * percentage / 100.0));
                            
                            // Model에 저장
                            if (listView.Model != null)
                            {
                                try
                                {
                                    var modelType = listView.Model.GetType();
                                    var splitterPositionProperty = modelType.GetProperty("SplitterPosition");
                                    var currentDistance = GetSplitContainerDistance(splitContainer);
                                    splitterPositionProperty?.SetValue(listView.Model, currentDistance);
                                }
                                catch { }
                            }
                            
                            MessageBox.Show($"분할 위치를 {percentage}%로 설정했습니다.", "분할 설정", 
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"분할 설정 중 오류가 발생했습니다: {ex.Message}", "오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetSplitContainerOrientation(object splitContainer, Orientation orientation)
        {
            try
            {
                var splitContainerType = splitContainer.GetType();
                var orientationProperty = splitContainerType.GetProperty("Orientation");
                orientationProperty?.SetValue(splitContainer, orientation);
            }
            catch { }
        }

        private int GetSplitContainerSize(object splitContainer)
        {
            try
            {
                var splitContainerType = splitContainer.GetType();
                var orientationProperty = splitContainerType.GetProperty("Orientation");
                var widthProperty = splitContainerType.GetProperty("Width");
                var heightProperty = splitContainerType.GetProperty("Height");
                
                var orientation = orientationProperty?.GetValue(splitContainer);
                bool isVertical = orientation?.ToString() == "Vertical";
                
                if (isVertical)
                {
                    return (int)(widthProperty?.GetValue(splitContainer) ?? 0);
                }
                else
                {
                    return (int)(heightProperty?.GetValue(splitContainer) ?? 0);
                }
            }
            catch
            {
                return 0;
            }
        }

        private void SetSplitContainerDistance(object splitContainer, int distance)
        {
            try
            {
                var splitContainerType = splitContainer.GetType();
                var splitterDistanceProperty = splitContainerType.GetProperty("SplitterDistance");
                splitterDistanceProperty?.SetValue(splitContainer, distance);
            }
            catch { }
        }

        private int GetSplitContainerDistance(object splitContainer)
        {
            try
            {
                var splitContainerType = splitContainer.GetType();
                var splitterDistanceProperty = splitContainerType.GetProperty("SplitterDistance");
                return (int)(splitterDistanceProperty?.GetValue(splitContainer) ?? 0);
            }
            catch
            {
                return 0;
            }
        }

        private void SafeEndCurrentEdit()
        {
            try
            {
                if (View is DevExpress.ExpressApp.ListView listView && 
                    listView.Editor?.GetType().Name == "MasterDetailListEditor")
                {
                    var editor = listView.Editor;
                    var editorType = editor.GetType();
                    
                    // Reflection을 사용하여 Master와 Detail GridView에 접근
                    var masterGridViewField = editorType.GetField("masterGridView", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var detailGridViewField = editorType.GetField("detailGridView", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    var masterGridView = masterGridViewField?.GetValue(editor);
                    var detailGridView = detailGridViewField?.GetValue(editor);
                    
                    // Master GridView 편집 종료
                    if (masterGridView != null)
                    {
                        try
                        {
                            var masterType = masterGridView.GetType();
                            var isEditingProperty = masterType.GetProperty("IsEditing");
                            var closeEditorMethod = masterType.GetMethod("CloseEditor");
                            
                            bool isEditing = (bool)(isEditingProperty?.GetValue(masterGridView) ?? false);
                            if (isEditing)
                            {
                                closeEditorMethod?.Invoke(masterGridView, null);
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // 이미 해제된 경우 무시
                        }
                    }
                    
                    // Detail GridView 편집 종료
                    if (detailGridView != null)
                    {
                        try
                        {
                            var detailType = detailGridView.GetType();
                            var isEditingProperty = detailType.GetProperty("IsEditing");
                            var closeEditorMethod = detailType.GetMethod("CloseEditor");
                            
                            bool isEditing = (bool)(isEditingProperty?.GetValue(detailGridView) ?? false);
                            if (isEditing)
                            {
                                closeEditorMethod?.Invoke(detailGridView, null);
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // 이미 해제된 경우 무시
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SafeEndCurrentEdit 오류: {ex.Message}");
            }
        }

        protected override void OnDeactivated()
        {
            smartDeleteAction = null;
            splitVerticalAction = null;
            splitHorizontalAction = null;
            splitPosition30Action = null;
            splitPosition50Action = null;
            splitPosition70Action = null;

            // 이벤트 핸들러 제거
            if (refreshController != null)
            {
                refreshController.RefreshAction.Executing -= RefreshAction_Executing;
                refreshController.RefreshAction.Executed -= RefreshAction_Executed;
                refreshController = null;
            }

            base.OnDeactivated();
        }

        private void RefreshAction_Executing(object sender, CancelEventArgs e)
        {
            try
            {
                // ListView인 경우에만 처리
                if (View is DevExpress.ExpressApp.ListView listView && 
                    listView.Editor?.GetType().Name == "MasterDetailListEditor")
                {
                    // 현재 편집 중인 내용을 안전하게 종료
                    SafeEndCurrentEdit();
                    
                    System.Diagnostics.Debug.WriteLine("Refresh 실행 전: 편집 종료 완료");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshAction_Executing 오류: {ex.Message}");
                // 오류가 발생해도 Refresh는 계속 진행
            }
        }

        private void RefreshAction_Executed(object sender, ActionBaseEventArgs e)
        {
            try
            {
                // ListView인 경우에만 처리
                if (View is DevExpress.ExpressApp.ListView listView && 
                    listView.Editor?.GetType().Name == "MasterDetailListEditor")
                {
                    // Refresh 완료 후 Detail 데이터를 안전하게 재설정
                    SafeRefreshDetailAfterRefresh();
                    
                    System.Diagnostics.Debug.WriteLine("Refresh 실행 후: Detail 데이터 재설정 완료");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshAction_Executed 오류: {ex.Message}");
            }
        }

        private void SafeRefreshDetailAfterRefresh()
        {
            try
            {
                // ListView인 경우에만 처리
                if (View is DevExpress.ExpressApp.ListView listView && 
                    listView.Editor?.GetType().Name == "MasterDetailListEditor")
                {
                    var editor = listView.Editor;
                    
                    // Reflection을 통해 SafeRefreshDetailAfterRefresh 메서드 호출
                    var refreshMethod = editor.GetType().GetMethod("SafeRefreshDetailAfterRefresh", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (refreshMethod != null)
                    {
                        // 직접 호출 (ObjectSpace 새로고침 완료 후)
                        try
                        {
                            refreshMethod.Invoke(editor, null);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"SafeRefreshDetailAfterRefresh 호출 오류: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SafeRefreshDetailAfterRefresh 오류: {ex.Message}");
            }
        }

        private void DeleteMultipleOrders(List<Order> orders)
        {
            if (orders.Count == 0) return;
            
            string message = orders.Count == 1 
                ? $"주문 '{orders[0].OrderNo}'을(를) 삭제하시겠습니까?\n관련된 모든 상세 항목도 함께 삭제됩니다."
                : $"선택된 {orders.Count}개의 주문을 삭제하시겠습니까?\n관련된 모든 상세 항목도 함께 삭제됩니다.";
                
            var result = MessageBox.Show(message, "주문 삭제 확인", 
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    foreach (var order in orders)
                    {
                        ObjectSpace.Delete(order);
                    }
                    ObjectSpace.CommitChanges();
                    View.RefreshDataSource();
                    
                    string successMessage = orders.Count == 1 
                        ? "주문이 삭제되었습니다."
                        : $"{orders.Count}개의 주문이 삭제되었습니다.";
                    MessageBox.Show(successMessage, "삭제 완료", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}", "오류", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DeleteMultipleOrderDetails(List<OrderDetail> details)
        {
            if (details.Count == 0) return;
            
            string message = details.Count == 1 
                ? $"상세 항목 '{details[0].ItemName}'을(를) 삭제하시겠습니까?"
                : $"선택된 {details.Count}개의 상세 항목을 삭제하시겠습니까?";
                
            var result = MessageBox.Show(message, "상세 삭제 확인", 
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    // 영향받는 주문들 수집 (총액 업데이트용)
                    var affectedOrders = new HashSet<Order>();
                    
                    foreach (var detail in details)
                    {
                        if (detail.Order != null)
                        {
                            affectedOrders.Add(detail.Order);
                        }
                        ObjectSpace.Delete(detail);
                    }
                    
                    ObjectSpace.CommitChanges();
                    
                    // 영향받는 주문들의 총액 업데이트
                    foreach (var order in affectedOrders)
                    {
                        order.UpdateTotalAmount();
                    }
                    
                    View.RefreshDataSource();
                    
                    string successMessage = details.Count == 1 
                        ? "상세 항목이 삭제되었습니다."
                        : $"{details.Count}개의 상세 항목이 삭제되었습니다.";
                    MessageBox.Show(successMessage, "삭제 완료", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}", "오류", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void UpdateSmartDeleteTooltip()
        {
            // 현재 포커스된 그리드에 따라 툴팁 업데이트
            try
            {
                var focusedGrid = GetFocusedGrid();
                if (focusedGrid != null)
                {
                    if (focusedGrid.Item1) // Master Grid
                    {
                        smartDeleteAction.ToolTip = "선택된 마스터 항목을 삭제합니다";
                    }
                    else // Detail Grid
                    {
                        var tabName = focusedGrid.Item2;
                        smartDeleteAction.ToolTip = $"선택된 {tabName} 항목을 삭제합니다";
                    }
                }
                else
                {
                    smartDeleteAction.ToolTip = "포커스된 그리드에 따라 마스터 또는 디테일 항목을 삭제합니다";
                }
            }
            catch
            {
                smartDeleteAction.ToolTip = "포커스된 그리드에 따라 마스터 또는 디테일 항목을 삭제합니다";
            }
        }

        private void SplitVerticalAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            SafeEndCurrentEdit();
            SetSplitOrientation(Orientation.Vertical);
        }

        private void SplitHorizontalAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            SafeEndCurrentEdit();
            SetSplitOrientation(Orientation.Horizontal);
        }

        // 현재 포커스된 그리드 정보 반환 (isMaster, tabName, gridView)
        private Tuple<bool, string, object> GetFocusedGrid()
        {
            try
            {
                if (View?.Editor?.GetType().Name != "MasterDetailListEditor") return null;
                
                var editor = View.Editor;
                var editorType = editor.GetType();
                
                // Master GridView 가져오기
                var masterGridViewField = editorType.GetField("masterGridView", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var masterGridView = masterGridViewField?.GetValue(editor);
                
                // Master GridControl 가져오기
                var masterGridControlField = editorType.GetField("masterGridControl", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var masterGridControl = masterGridControlField?.GetValue(editor);
                
                // TabControl 가져오기
                var detailTabControlField = editorType.GetField("detailTabControl", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var detailTabControl = detailTabControlField?.GetValue(editor);
                
                // Detail GridViews Dictionary 가져오기
                var detailGridViewsField = editorType.GetField("detailGridViews", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var detailGridViews = detailGridViewsField?.GetValue(editor);
                
                // Master Grid에 포커스가 있는지 확인
                if (masterGridControl != null && GetControlFocus(masterGridControl))
                {
                    return new Tuple<bool, string, object>(true, "Master", masterGridView);
                }
                
                // Detail Grids 중에서 포커스된 것 찾기
                if (detailTabControl != null && detailGridViews != null)
                {
                    // TabControl의 SelectedTab 속성 가져오기
                    var selectedTabProperty = detailTabControl.GetType().GetProperty("SelectedTab");
                    var selectedTab = selectedTabProperty?.GetValue(detailTabControl);
                    
                    if (selectedTab != null)
                    {
                        // 선택된 탭에서 GridControl 찾기
                        var controlsProperty = selectedTab.GetType().GetProperty("Controls");
                        var controls = controlsProperty?.GetValue(selectedTab);
                        
                        if (controls != null)
                        {
                            // Controls 컬렉션에서 GridControl 찾기
                            foreach (var control in (System.Collections.IEnumerable)controls)
                            {
                                if (control.GetType().Name == "GridControl" && GetControlFocus(control))
                                {
                                    var textProperty = selectedTab.GetType().GetProperty("Text");
                                    var tabName = textProperty?.GetValue(selectedTab)?.ToString() ?? "Detail";
                                    
                                    var mainViewProperty = control.GetType().GetProperty("MainView");
                                    var gridView = mainViewProperty?.GetValue(control);
                                    
                                    return new Tuple<bool, string, object>(false, tabName, gridView);
                                }
                            }
                        }
                    }
                }
                
                // 기본값: Master Grid 반환
                return new Tuple<bool, string, object>(true, "Master", masterGridView);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetFocusedGrid 오류: {ex.Message}");
                return null;
            }
        }
    }
} 