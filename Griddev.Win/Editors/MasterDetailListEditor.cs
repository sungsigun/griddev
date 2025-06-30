using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Utils;
using DevExpress.ExpressApp.Win.Editors;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.Xpo;
using Griddev.Module.BusinessObjects;

namespace Griddev.Win.Editors
{
    [ListEditor(typeof(object))]
    public class MasterDetailListEditor : GridListEditor
    {
        private SplitContainer splitContainer;
        private TabControl detailTabControl;
        private Dictionary<string, GridControl> detailGridControls;
        private Dictionary<string, GridView> detailGridViews;
        private bool isUpdatingSelection = false;
        private GridView masterGridView; // 기본 GridView 참조 저장
        private GridControl masterGridControl; // 마스터 GridControl 참조 저장
        private int lastActiveTabIndex = -1; // 저장 전 활성 탭 인덱스 저장

        public MasterDetailListEditor(IModelListView model) : base(model) 
        {
            // XAF 기본 삭제 기능 대신 커스텀 삭제 기능 사용
            detailGridControls = new Dictionary<string, GridControl>();
            detailGridViews = new Dictionary<string, GridView>();
        }

        public override string Name => "MasterDetailListEditor";

        protected override object CreateControlsCore()
        {
            // 기본 GridControl 생성
            var baseControl = (GridControl)base.CreateControlsCore();
            masterGridControl = baseControl; // 마스터 GridControl 참조 저장
            masterGridView = (GridView)baseControl.MainView; // 참조 저장

            // SplitContainer 생성 (툴바 없이 전체 화면 사용)
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = GetOrientation(),
                SplitterDistance = GetSplitterDistance(),
                SplitterWidth = 5
            };

            // Master Grid (기존 GridControl 사용)
            baseControl.Dock = DockStyle.Fill;
            splitContainer.Panel1.Controls.Add(baseControl);

            // Detail TabControl 생성
            detailTabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };
            
            // 탭 변경 이벤트 구독 (활성 탭 추적용)
            detailTabControl.SelectedIndexChanged += DetailTabControl_SelectedIndexChanged;
            
            splitContainer.Panel2.Controls.Add(detailTabControl);

            // Master Grid 설정
            SetupMasterGrid();

            return splitContainer;
        }

        private Orientation GetOrientation()
        {
            // Model에서 방향 설정 가져오기 (기본값: Vertical)
            if (Model is IModelMasterDetailListView mdModel && mdModel.SplitOrientation != null)
            {
                return mdModel.SplitOrientation == "Horizontal" ? Orientation.Horizontal : Orientation.Vertical;
            }
            return Orientation.Vertical;
        }

        private int GetSplitterDistance()
        {
            // Model에서 분할 비율 가져오기 (기본값: 50%)
            if (Model is IModelMasterDetailListView mdModel && mdModel.SplitterPosition > 0)
            {
                return mdModel.SplitterPosition;
            }
            return 300; // 기본값
        }

        private void SetupMasterGrid()
        {
            // Excel 스타일 편집 설정
            masterGridView.OptionsBehavior.Editable = true;
            masterGridView.OptionsBehavior.EditingMode = GridEditingMode.Inplace;  // 셀 직접 편집
            
            // 새 행 설정 - ShowNewItemRow는 deprecated되었으므로 NewItemRowPosition만 사용
            masterGridView.OptionsView.NewItemRowPosition = DevExpress.XtraGrid.Views.Grid.NewItemRowPosition.Top;
            
            // Excel 스타일 키보드 네비게이션
            masterGridView.OptionsNavigation.EnterMoveNextColumn = true;
            masterGridView.OptionsNavigation.AutoMoveRowFocus = true;
            masterGridView.OptionsNavigation.UseTabKey = true;
            
            // UI 설정 - 컬럼 헤더 표시 개선
            masterGridView.OptionsView.ShowGroupPanel = false;
            masterGridView.OptionsView.ShowFilterPanelMode = DevExpress.XtraGrid.Views.Base.ShowFilterPanelMode.Never;
            masterGridView.OptionsView.ShowColumnHeaders = true;  // 컬럼 헤더 확실히 표시
            masterGridView.OptionsView.ColumnAutoWidth = false;   // 자동 너비 조정 해제
            masterGridView.OptionsView.ColumnHeaderAutoHeight = DevExpress.Utils.DefaultBoolean.False;
            masterGridView.Appearance.HeaderPanel.TextOptions.WordWrap = DevExpress.Utils.WordWrap.NoWrap;
            
            // 컬럼 크기 조정 설정
            masterGridView.OptionsCustomization.AllowColumnResizing = true;
            masterGridView.OptionsCustomization.AllowColumnMoving = true;
            
            // 멀티 셀렉션 설정
            masterGridView.OptionsSelection.MultiSelect = true;
            masterGridView.OptionsSelection.MultiSelectMode = GridMultiSelectMode.RowSelect;
            masterGridView.OptionsSelection.EnableAppearanceFocusedCell = false;
            masterGridView.OptionsSelection.EnableAppearanceHideSelection = false;
            
            // 검증 이벤트 추가
            masterGridView.ValidateRow += BaseGridView_ValidateRow;
            masterGridView.InvalidRowException += GridView_InvalidRowException;

            // 마스터 선택 변경 이벤트
            masterGridView.FocusedRowChanged += BaseGridView_FocusedRowChanged;
            
            // Del 키 이벤트 추가
            masterGridView.KeyDown += MasterGridView_KeyDown;
            
            // 데이터 바인딩 후 컬럼 설정
            masterGridView.DataSourceChanged += MasterGridView_DataSourceChanged;
        }

        private void MasterGridView_DataSourceChanged(object sender, EventArgs e)
        {
            try
            {
                if (masterGridView.Columns.Count > 0)
                {
                    // 컬럼 헤더 표시 강제 설정
                    masterGridView.OptionsView.ShowColumnHeaders = true;
                    
                    // 시스템 컬럼 숨기기
                    HideSystemColumns(masterGridView);
                    
                    // 컬럼 순서 및 캡션 설정
                    SetupMasterColumns();
                    
                    // 컬럼 너비 자동 조정
                    masterGridControl.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            masterGridView.BestFitColumns();
                        }
                        catch
                        {
                            // BestFitColumns 실패 시 무시
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MasterGridView_DataSourceChanged 오류: {ex.Message}");
            }
        }

        private void SetupMasterColumns()
        {
            try
            {
                // Order 컬럼 설정
                var columns = new Dictionary<string, string>
                {
                    { "OrderNo", "주문번호" },
                    { "OrderDate", "주문일자" },
                    { "CustomerCode", "고객코드" },
                    { "CustomerName", "고객명" },
                    { "ProjectName", "프로젝트명" },
                    { "DeliveryDate", "납기일" },
                    { "Status", "상태" },
                    { "SalesPerson", "영업담당" },
                    { "TotalAmount", "총액" },
                    { "Remarks", "비고" }
                };

                int visibleIndex = 0;
                foreach (var kvp in columns)
                {
                    var column = masterGridView.Columns[kvp.Key];
                    if (column != null && column.Visible)
                    {
                        column.Caption = kvp.Value;
                        column.VisibleIndex = visibleIndex++;
                        
                        // 특정 컬럼 너비 설정
                        switch (kvp.Key)
                        {
                            case "OrderNo":
                                column.Width = 100;
                                break;
                            case "OrderDate":
                                column.Width = 100;
                                break;
                            case "CustomerCode":
                                column.Width = 100;
                                break;
                            case "CustomerName":
                                column.Width = 120;
                                break;
                            case "ProjectName":
                                column.Width = 150;
                                break;
                            case "DeliveryDate":
                                column.Width = 100;
                                break;
                            case "Status":
                                column.Width = 80;
                                break;
                            case "SalesPerson":
                                column.Width = 100;
                                break;
                            case "TotalAmount":
                                column.Width = 100;
                                break;
                            case "Remarks":
                                column.Width = 120;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetupMasterColumns 오류: {ex.Message}");
            }
        }

        // SetupDetailGrid 메서드는 더 이상 사용하지 않음 (탭 기반 구조로 변경)
        // 각 탭의 GridView는 SetupDetailGridView 메서드에서 개별적으로 설정됨

        private void HideSystemColumns(GridView gridView)
        {
            try
            {
                // 숨길 시스템 컬럼 목록
                string[] systemColumns = {
                    "Oid",                    // Primary Key
                    "OptimisticLockField",    // 동시성 제어
                    "GCRecord",              // Garbage Collection
                    "This",                  // 객체 참조
                    "Session",               // XPO Session
                    "ClassInfo",             // 클래스 정보
                    "IsLoading",             // 로딩 상태
                    "IsDeleted",             // 삭제 상태
                    "IsDirty",               // 변경 상태
                    "Loading"                // 로딩 플래그
                };

                foreach (var columnName in systemColumns)
                {
                    var column = gridView.Columns[columnName];
                    if (column != null)
                    {
                        column.Visible = false;
                    }
                }

                // Detail 그리드에서 추가로 숨길 컬럼들 (탭 기반 구조에서는 모든 detail 그리드에 적용)
                if (gridView != masterGridView)
                {
                    string[] detailHideColumns = {
                        "Order",              // 부모 참조 (Master-Detail에서 불필요)
                        "DisplayName",        // ToString 용도
                        "OrderNo",           // 마스터에서 이미 표시
                        "CustomerName"       // 마스터에서 이미 표시
                    };

                    foreach (var columnName in detailHideColumns)
                    {
                        var column = gridView.Columns[columnName];
                        if (column != null)
                        {
                            column.Visible = false;
                        }
                    }
                }

                // Order에서 추가로 숨길 컬럼들
                if (gridView == masterGridView)
                {
                    string[] masterHideColumns = {
                        "DisplayName",        // ToString 용도
                        "DetailCount"        // 계산된 속성
                    };

                    foreach (var columnName in masterHideColumns)
                    {
                        var column = gridView.Columns[columnName];
                        if (column != null)
                        {
                            column.Visible = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HideSystemColumns 오류: {ex.Message}");
            }
        }

        private void BaseGridView_ValidateRow(object sender, DevExpress.XtraGrid.Views.Base.ValidateRowEventArgs e)
        {
            // Master 행 검증 로직 (필요시 추가)
        }

        private void DetailGridView_ValidateRow(object sender, DevExpress.XtraGrid.Views.Base.ValidateRowEventArgs e)
        {
            // Detail 행 검증 로직 (필요시 추가)
        }

        private void GridView_InvalidRowException(object sender, DevExpress.XtraGrid.Views.Base.InvalidRowExceptionEventArgs e)
        {
            // 사용자 친화적인 오류 메시지 표시
            e.ExceptionMode = DevExpress.XtraEditors.Controls.ExceptionMode.DisplayError;
            
            // 구체적인 오류 메시지 제공
            if (e.Exception.Message.Contains("Decimal"))
            {
                e.ErrorText = "숫자 형식이 올바르지 않습니다. 올바른 숫자를 입력해주세요.";
            }
            else if (e.Exception.Message.Contains("RuleRange"))
            {
                e.ErrorText = "입력 값이 허용 범위를 벗어났습니다. 올바른 값을 입력해주세요.";
            }
            else
            {
                e.ErrorText = "입력 값에 오류가 있습니다: " + e.Exception.Message;
            }
        }

        private void BaseGridView_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            if (isUpdatingSelection) return;

            try
            {
                // 객체 상태 확인
                if (masterGridView == null || masterGridControl == null || masterGridControl.IsDisposed ||
                    detailTabControl == null || detailTabControl.IsDisposed)
                {
                    return;
                }

                isUpdatingSelection = true;
                UpdateDetailData();

            }
            catch (ObjectDisposedException)
            {
                // 객체가 이미 해제된 경우 무시
                System.Diagnostics.Debug.WriteLine("BaseGridView_FocusedRowChanged: ObjectDisposedException 발생 - 무시");
            }
            catch (Exception ex)
            {
                // 기타 예외는 로그만 남기고 무시
                System.Diagnostics.Debug.WriteLine($"BaseGridView_FocusedRowChanged 오류: {ex.Message}");
            }
            finally
            {
                isUpdatingSelection = false;
            }
        }

        private void DetailGridView_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
        {
            // 디테일 선택 변경 처리
        }

        private void DetailGridView_RowUpdated(object sender, DevExpress.XtraGrid.Views.Base.RowObjectEventArgs e)
        {
            try
            {
                // 범용적으로 처리 - 특정 타입에 의존하지 않음
                var masterObject = masterGridView?.GetFocusedRow();
                if (masterObject != null && e.Row != null)
                {
                    // 새로 추가된 객체인 경우 기본값 설정 로직을 여기에 추가할 수 있음
                    // 현재는 특정 타입에 의존하지 않도록 범용적으로 처리
                    System.Diagnostics.Debug.WriteLine($"새 행 추가됨: {e.Row.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DetailGridView_RowUpdated 오류: {ex.Message}");
            }
        }

        // 탭 변경 이벤트 핸들러 (활성 탭 추적)
        private void DetailTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (detailTabControl != null && !isUpdatingSelection)
            {
                lastActiveTabIndex = detailTabControl.SelectedIndex;
                System.Diagnostics.Debug.WriteLine($"활성 탭 변경: {lastActiveTabIndex}");
            }
        }

        private void DetailGridView_DataSourceChanged(object sender, EventArgs e)
        {
            try
            {
                var gridView = sender as GridView;
                if (gridView?.Columns.Count > 0)
                {
                    // 컬럼 헤더 표시 강제 설정
                    gridView.OptionsView.ShowColumnHeaders = true;
                    
                    // 시스템 컬럼 숨기기
                    HideSystemColumns(gridView);
                    
                    // 컬럼 순서 및 캡션 설정
                    if (gridView.DataSource != null)
                    {
                        var dataSourceType = gridView.DataSource.GetType();
                        if (dataSourceType.IsGenericType)
                        {
                            var itemType = dataSourceType.GetGenericArguments()[0];
                            SetupDetailColumns(gridView, itemType);
                        }
                    }
                    
                    // 컬럼 너비 자동 조정
                    var gridControl = gridView.GridControl;
                    gridControl?.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            gridView.BestFitColumns();
                        }
                        catch
                        {
                            // BestFitColumns 실패 시 무시
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DetailGridView_DataSourceChanged 오류: {ex.Message}");
            }
        }

        private void SetupDetailColumns(GridView gridView, Type itemType)
        {
            try
            {
                if (gridView?.Columns == null || itemType == null) return;

                // 타입의 모든 속성 가져오기
                var properties = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                
                int visibleIndex = 0;
                foreach (var property in properties)
                {
                    var column = gridView.Columns[property.Name];
                    if (column != null && column.Visible)
                    {
                        // DisplayName 특성 확인
                        var displayNameAttr = property.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>();
                        if (displayNameAttr != null && !string.IsNullOrEmpty(displayNameAttr.DisplayName))
                        {
                            column.Caption = displayNameAttr.DisplayName;
                        }
                        else
                        {
                            // 기본 한글 캡션 설정
                            column.Caption = GetPropertyDisplayName(property.Name, itemType);
                        }
                        
                        column.VisibleIndex = visibleIndex++;
                        
                        // 데이터 타입에 따른 기본 너비 설정
                        SetColumnWidth(column, property);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetupDetailColumns 오류: {ex.Message}");
            }
        }

        // 속성 이름에 대한 표시 이름 가져오기
        private string GetPropertyDisplayName(string propertyName, Type itemType)
        {
            // 타입별 속성 이름 변환
            var translations = new Dictionary<string, Dictionary<string, string>>
            {
                // OrderDetail 속성들
                [typeof(OrderDetail).Name] = new Dictionary<string, string>
                {
                    { "Sequence", "순번" },
                    { "ItemCode", "품목코드" },
                    { "ItemName", "품목명" },
                    { "Specification", "규격" },
                    { "Quantity", "수량" },
                    { "Unit", "단위" },
                    { "UnitPrice", "단가" },
                    { "Amount", "금액" },
                    { "DeliveryDate", "납기일" },
                    { "Remarks", "비고" }
                },
                // OrderHistory 속성들
                ["OrderHistory"] = new Dictionary<string, string>
                {
                    { "Sequence", "순번" },
                    { "ActionDate", "처리일시" },
                    { "ActionType", "처리유형" },
                    { "ActionBy", "처리자" },
                    { "PreviousStatus", "이전상태" },
                    { "NewStatus", "새상태" },
                    { "ChangeDetails", "변경내용" },
                    { "Remarks", "비고" }
                },
                // Order 속성들
                [typeof(Order).Name] = new Dictionary<string, string>
                {
                    { "OrderNo", "주문번호" },
                    { "OrderDate", "주문일자" },
                    { "CustomerCode", "고객코드" },
                    { "CustomerName", "고객명" },
                    { "ProjectName", "프로젝트명" },
                    { "DeliveryDate", "납기일" },
                    { "Status", "상태" },
                    { "SalesPerson", "영업담당" },
                    { "TotalAmount", "총액" },
                    { "Remarks", "비고" }
                },
                // 공통 속성들
                ["Common"] = new Dictionary<string, string>
                {
                    { "Name", "이름" },
                    { "Code", "코드" },
                    { "Description", "설명" },
                    { "CreatedDate", "생성일" },
                    { "ModifiedDate", "수정일" },
                    { "IsActive", "활성" },
                    { "Status", "상태" },
                    { "Remarks", "비고" }
                }
            };

            // 타입별 번역 확인
            if (translations.ContainsKey(itemType.Name) && 
                translations[itemType.Name].ContainsKey(propertyName))
            {
                return translations[itemType.Name][propertyName];
            }
            
            // 공통 번역 확인
            if (translations["Common"].ContainsKey(propertyName))
            {
                return translations["Common"][propertyName];
            }
            
            // 기본값은 속성 이름 그대로
            return propertyName;
        }

        // 데이터 타입에 따른 컬럼 너비 설정
        private void SetColumnWidth(GridColumn column, PropertyInfo property)
        {
            var propertyType = property.PropertyType;
            
            // Nullable 타입 처리
            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                propertyType = Nullable.GetUnderlyingType(propertyType);
            }
            
            // 타입별 기본 너비 설정
            if (propertyType == typeof(DateTime))
            {
                column.Width = 100;
            }
            else if (propertyType == typeof(decimal) || propertyType == typeof(double) || propertyType == typeof(float))
            {
                column.Width = 100;
            }
            else if (propertyType == typeof(int) || propertyType == typeof(long))
            {
                column.Width = 80;
            }
            else if (propertyType == typeof(bool))
            {
                column.Width = 60;
            }
            else if (propertyType == typeof(string))
            {
                // 속성 이름에 따른 문자열 필드 너비
                var propertyName = property.Name.ToLower();
                if (propertyName.Contains("code") || propertyName.Contains("no"))
                {
                    column.Width = 100;
                }
                else if (propertyName.Contains("name") || propertyName.Contains("title"))
                {
                    column.Width = 150;
                }
                else if (propertyName.Contains("description") || propertyName.Contains("remarks"))
                {
                    column.Width = 200;
                }
                else
                {
                    column.Width = 120;
                }
            }
            else
            {
                column.Width = 100; // 기본값
            }
        }

        private void UpdateDetailData()
        {
            try
            {
                // 현재 편집 중인 내용을 안전하게 종료
                SafeEndCurrentEdit();

                var masterObject = masterGridView?.GetFocusedRow();
                
                // 마스터 객체가 null이거나 Disposed된 경우
                if (masterObject == null)
                {
                    ClearAllDetailTabs();
                    return;
                }

                // XPO 객체인 경우 Disposed 상태 확인
                if (masterObject is BaseObject baseObj)
                {
                    // Session이 null이거나 객체가 삭제된 상태면 무시
                    if (baseObj.Session == null || baseObj.IsDeleted)
                    {
                        ClearAllDetailTabs();
                        return;
                    }
                }

                // 모든 Detail Collection 찾기
                var detailCollections = GetDetailCollections(masterObject);
                
                // 기존 탭들 정리
                ClearAllDetailTabs();
                
                // 각 Collection마다 탭 생성
                foreach (var collection in detailCollections)
                {
                    CreateDetailTab(collection);
                }
            }
            catch (ObjectDisposedException)
            {
                // 객체가 해제된 경우 Detail을 비움
                ClearAllDetailTabs();
            }
            catch (Exception ex)
            {
                // 기타 예외 발생 시 로그 남기고 Detail을 비움
                System.Diagnostics.Debug.WriteLine($"UpdateDetailData 오류: {ex.Message}");
                ClearAllDetailTabs();
            }
        }

        // 모든 Detail 탭 정리
        private void ClearAllDetailTabs()
        {
            try
            {
                if (detailTabControl != null)
                {
                    detailTabControl.TabPages.Clear();
                }
                
                // 기존 GridControl들 정리
                foreach (var gridControl in detailGridControls.Values)
                {
                    gridControl?.Dispose();
                }
                detailGridControls.Clear();
                detailGridViews.Clear();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearAllDetailTabs 오류: {ex.Message}");
            }
        }

        // Detail 탭 생성
        private void CreateDetailTab(DetailCollection collection)
        {
            try
            {
                // TabPage 생성
                var tabPage = new TabPage(collection.Name)
                {
                    UseVisualStyleBackColor = true
                };

                // GridControl 생성
                var gridControl = new GridControl
                {
                    Dock = DockStyle.Fill,
                    DataSource = collection.Data
                };

                // GridView 생성 및 설정
                var gridView = new GridView(gridControl);
                gridControl.MainView = gridView;
                
                // Detail GridView 설정
                SetupDetailGridView(gridView, collection.ItemType);
                
                // 컨트롤들을 Dictionary에 저장
                detailGridControls[collection.PropertyName] = gridControl;
                detailGridViews[collection.PropertyName] = gridView;
                
                // 탭에 GridControl 추가
                tabPage.Controls.Add(gridControl);
                
                // TabControl에 탭 추가
                detailTabControl.TabPages.Add(tabPage);
                
                // 컬럼 설정
                SetupDetailColumns(gridView, collection.ItemType);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateDetailTab 오류: {ex.Message}");
            }
        }

        // Detail GridView 설정
        private void SetupDetailGridView(GridView gridView, Type itemType)
        {
            // OrderHistory는 + 버튼 없이 조회만 가능
            bool isOrderHistory = itemType.Name == "OrderHistory";
            
            // Excel 스타일 편집 설정
            gridView.OptionsBehavior.Editable = true;
            gridView.OptionsBehavior.EditingMode = GridEditingMode.Inplace;
            
            // Detail 탭에서 + 버튼 제거하고 빈 라인만 표시 (주문상세와 동일)
            gridView.OptionsBehavior.AllowAddRows = DevExpress.Utils.DefaultBoolean.False;  // + 버튼 제거
            gridView.OptionsView.NewItemRowPosition = DevExpress.XtraGrid.Views.Grid.NewItemRowPosition.Top;  // 빈 라인은 위쪽에 표시
            
            // Excel 스타일 키보드 네비게이션
            gridView.OptionsNavigation.EnterMoveNextColumn = true;
            gridView.OptionsNavigation.AutoMoveRowFocus = true;
            gridView.OptionsNavigation.UseTabKey = true;
            
            // UI 설정
            gridView.OptionsView.ShowGroupPanel = false;
            gridView.OptionsView.ShowFilterPanelMode = DevExpress.XtraGrid.Views.Base.ShowFilterPanelMode.Never;
            gridView.OptionsView.ShowColumnHeaders = true;
            gridView.OptionsView.ColumnAutoWidth = false;
            
            // 멀티 셀렉션 설정
            gridView.OptionsSelection.MultiSelect = true;
            gridView.OptionsSelection.MultiSelectMode = GridMultiSelectMode.RowSelect;
            gridView.OptionsSelection.EnableAppearanceFocusedCell = false;
            gridView.OptionsSelection.EnableAppearanceHideSelection = false;
            
            // 컬럼 크기 조정 설정
            gridView.OptionsCustomization.AllowColumnResizing = true;
            gridView.OptionsCustomization.AllowColumnMoving = true;
            
            // 이벤트 핸들러 등록
            gridView.ValidateRow += DetailGridView_ValidateRow;
            gridView.InvalidRowException += GridView_InvalidRowException;
            gridView.FocusedRowChanged += DetailGridView_FocusedRowChanged;
            gridView.KeyDown += DetailGridView_KeyDown;
            gridView.DataSourceChanged += (s, e) => 
            {
                HideSystemColumns(gridView);
                SetupDetailColumns(gridView, itemType);
            };
        }

        public override void BreakLinksToControls()
        {
            try
            {
                // 마스터 그리드 이벤트 핸들러 제거 - 안전하게 처리
                if (masterGridView != null && masterGridControl != null && !masterGridControl.IsDisposed)
                {
                    masterGridView.FocusedRowChanged -= BaseGridView_FocusedRowChanged;
                    masterGridView.ValidateRow -= BaseGridView_ValidateRow;
                    masterGridView.InvalidRowException -= GridView_InvalidRowException;
                    masterGridView.DataSourceChanged -= MasterGridView_DataSourceChanged;
                    masterGridView.KeyDown -= MasterGridView_KeyDown;
                }

                // 모든 디테일 그리드 이벤트 핸들러 제거
                foreach (var kvp in detailGridViews)
                {
                    var detailGridView = kvp.Value;
                    var detailGridControl = detailGridControls.ContainsKey(kvp.Key) ? detailGridControls[kvp.Key] : null;
                    
                    if (detailGridView != null && detailGridControl != null && !detailGridControl.IsDisposed)
                    {
                        detailGridView.FocusedRowChanged -= DetailGridView_FocusedRowChanged;
                        detailGridView.ValidateRow -= DetailGridView_ValidateRow;
                        detailGridView.InvalidRowException -= GridView_InvalidRowException;
                        detailGridView.KeyDown -= DetailGridView_KeyDown;
                    }
                }

                // 컨트롤 참조 정리
                masterGridView = null;
                masterGridControl = null;
                detailTabControl = null;
                splitContainer = null;
                
                // Dictionary들 정리
                detailGridControls?.Clear();
                detailGridViews?.Clear();
            }
            catch (Exception ex)
            {
                // 로깅 또는 무시
                System.Diagnostics.Debug.WriteLine($"BreakLinksToControls 오류: {ex.Message}");
            }
            finally
            {
                base.BreakLinksToControls();
            }
        }

        public override void Refresh()
        {
            try
            {
                // 현재 편집 중인 내용을 안전하게 종료
                SafeEndCurrentEdit();
                
                // 기본 클래스의 Refresh 사용
                base.Refresh();
                
                // 디테일 데이터 소스를 안전하게 재설정
                SafeRefreshDetailAfterRefresh();
            }
            catch (ObjectDisposedException)
            {
                // Refresh 중 객체가 해제된 경우 무시
                System.Diagnostics.Debug.WriteLine("Refresh: 객체가 해제되어 새로고침을 건너뜁니다.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh 오류: {ex.Message}");
            }
        }

        private void SafeRefreshDetailAfterRefresh()
        {
            try
            {
                // Refresh 후 Detail 데이터를 안전하게 재설정
                UpdateDetailData();
            }
            catch (ObjectDisposedException)
            {
                // 객체가 해제된 경우 Detail을 비움
                ClearAllDetailTabs();
                System.Diagnostics.Debug.WriteLine("SafeRefreshDetailAfterRefresh: 객체가 해제되어 Detail을 비웠습니다.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SafeRefreshDetailAfterRefresh 오류: {ex.Message}");
                ClearAllDetailTabs();
            }
        }

        private void SafeEndCurrentEdit()
        {
            try
            {
                // 마스터 그리드 편집 종료
                if (masterGridView != null && masterGridControl != null && !masterGridControl.IsDisposed)
                {
                    try
                    {
                        if (masterGridView.ActiveEditor != null)
                        {
                            masterGridView.CloseEditor();
                        }
                        
                        if (masterGridView.IsEditing)
                        {
                            masterGridView.UpdateCurrentRow();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // 마스터 그리드 객체가 해제된 경우 무시
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"마스터 그리드 편집 종료 오류: {ex.Message}");
                    }
                }

                // 모든 디테일 그리드 편집 종료
                foreach (var kvp in detailGridViews)
                {
                    var detailGridView = kvp.Value;
                    var detailGridControl = detailGridControls.ContainsKey(kvp.Key) ? detailGridControls[kvp.Key] : null;
                    
                    if (detailGridView != null && detailGridControl != null && !detailGridControl.IsDisposed)
                    {
                        try
                        {
                            if (detailGridView.ActiveEditor != null)
                            {
                                detailGridView.CloseEditor();
                            }
                            
                            if (detailGridView.IsEditing)
                            {
                                detailGridView.UpdateCurrentRow();
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // 디테일 그리드 객체가 해제된 경우 무시
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"디테일 그리드 편집 종료 오류: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SafeEndCurrentEdit 전체 오류: {ex.Message}");
            }
        }

        private void MasterGridView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                try
                {
                    // 편집 중인 경우 편집 종료
                    SafeEndCurrentEdit();
                    
                    // 선택된 행들 가져오기
                    var selectedRowHandles = masterGridView.GetSelectedRows();
                    if (selectedRowHandles.Length > 0)
                    {
                        var selectedOrders = new List<Order>();
                        
                        // 선택된 Order 객체들 수집
                        foreach (int rowHandle in selectedRowHandles)
                        {
                            if (rowHandle >= 0) // 실제 데이터 행인지 확인
                            {
                                var order = masterGridView.GetRow(rowHandle) as Order;
                                if (order != null)
                                {
                                    selectedOrders.Add(order);
                                }
                            }
                        }
                        
                        if (selectedOrders.Count > 0)
                        {
                            // 삭제 확인
                            string message = selectedOrders.Count == 1 
                                ? $"주문 '{selectedOrders[0].OrderNo}'을(를) 삭제하시겠습니까?"
                                : $"선택된 {selectedOrders.Count}개의 주문을 삭제하시겠습니까?";
                                
                            var result = MessageBox.Show(message, "삭제 확인", 
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                
                            if (result == DialogResult.Yes)
                            {
                                // 선택된 주문들 삭제
                                foreach (var order in selectedOrders)
                                {
                                    try
                                    {
                                        CollectionSource.ObjectSpace.Delete(order);
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"주문 삭제 오류: {ex.Message}");
                                    }
                                }
                                
                                // 변경사항 저장
                                try
                                {
                                    CollectionSource.ObjectSpace.CommitChanges();
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}", "오류", 
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                        }
                    }
                    
                    e.Handled = true; // 기본 Delete 동작 방지
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"삭제 처리 중 오류가 발생했습니다: {ex.Message}", "오류", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DetailGridView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                try
                {
                    // 편집 중인 경우 편집 종료
                    SafeEndCurrentEdit();
                    
                    var gridView = sender as GridView;
                    if (gridView == null) return;
                    
                    // 선택된 행들 가져오기
                    var selectedRowHandles = gridView.GetSelectedRows();
                    if (selectedRowHandles.Length > 0)
                    {
                        var selectedItems = new List<object>();
                        
                        // 선택된 객체들 수집
                        foreach (int rowHandle in selectedRowHandles)
                        {
                            if (rowHandle >= 0) // 실제 데이터 행인지 확인
                            {
                                var item = gridView.GetRow(rowHandle);
                                if (item != null)
                                {
                                    selectedItems.Add(item);
                                }
                            }
                        }
                        
                        if (selectedItems.Count > 0)
                        {
                            // 범용 삭제 확인 메시지
                            var itemTypeName = GetTypeDisplayName(selectedItems[0].GetType());
                            var displayName = GetItemDisplayName(selectedItems[0]);
                            
                            string message = selectedItems.Count == 1 
                                ? $"{itemTypeName} '{displayName}'을(를) 삭제하시겠습니까?"
                                : $"선택된 {selectedItems.Count}개의 {itemTypeName}을(를) 삭제하시겠습니까?";
                                
                            var result = MessageBox.Show(message, "삭제 확인", 
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                
                            if (result == DialogResult.Yes)
                            {
                                // 선택된 아이템들 삭제
                                foreach (var item in selectedItems)
                                {
                                    try
                                    {
                                        CollectionSource.ObjectSpace.Delete(item);
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"아이템 삭제 오류: {ex.Message}");
                                    }
                                }
                                
                                // 변경사항 저장
                                try
                                {
                                    CollectionSource.ObjectSpace.CommitChanges();
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}", "오류", 
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                        }
                    }
                    
                    e.Handled = true; // 기본 Delete 동작 방지
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"삭제 처리 중 오류가 발생했습니다: {ex.Message}", "오류", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        protected override void AssignDataSourceToControl(object dataSource)
        {
            // 기본 클래스의 데이터소스 할당 사용
            base.AssignDataSourceToControl(dataSource);
            

        }

        public override object FocusedObject
        {
            get => masterGridView?.GetFocusedRow();
            set
            {
                if (masterGridView != null && value != null && value is BaseObject baseObj)
                {
                    var handle = masterGridView.LocateByValue("Oid", baseObj.Oid);
                    if (handle != GridControl.InvalidRowHandle)
                    {
                        masterGridView.FocusedRowHandle = handle;
                    }
                }
            }
        }

        public override SelectionType SelectionType => SelectionType.Full;

        public override IList GetSelectedObjects()
        {
            var result = new ArrayList();
            if (masterGridView?.GetFocusedRow() != null)
            {
                result.Add(masterGridView.GetFocusedRow());
            }
            return result;
        }

        // Detail Collection 정보를 담는 클래스
        private class DetailCollection
        {
            public string Name { get; set; }          // 탭 표시 이름
            public string PropertyName { get; set; }  // 속성 이름
            public object Data { get; set; }          // 실제 데이터
            public Type ItemType { get; set; }        // 아이템 타입
        }

        // 마스터 객체에서 모든 Detail Collection 찾기
        private List<DetailCollection> GetDetailCollections(object masterObject)
        {
            var collections = new List<DetailCollection>();
            
            if (masterObject == null) return collections;
            
            var objectType = masterObject.GetType();
            var properties = objectType.GetProperties();
            
            foreach (var property in properties)
            {
                // Association 특성이 있는 Collection 속성 찾기
                var associationAttr = property.GetCustomAttribute<AssociationAttribute>();
                if (associationAttr != null && 
                    typeof(IEnumerable).IsAssignableFrom(property.PropertyType) &&
                    property.PropertyType != typeof(string) &&
                    property.PropertyType.IsGenericType)
                {
                    var data = property.GetValue(masterObject);
                    var itemType = property.PropertyType.GetGenericArguments()[0];
                    
                    collections.Add(new DetailCollection
                    {
                        Name = GetCollectionDisplayName(property),
                        PropertyName = property.Name,
                        Data = data,
                        ItemType = itemType
                    });
                }
            }
            
            return collections;
        }

        // Collection 속성의 표시 이름 가져오기
        private string GetCollectionDisplayName(PropertyInfo property)
        {
            // DisplayName 특성 확인
            var displayNameAttr = property.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>();
            if (displayNameAttr != null && !string.IsNullOrEmpty(displayNameAttr.DisplayName))
            {
                return displayNameAttr.DisplayName;
            }
            
            // 기본 이름 변환 (예: OrderDetails → 주문상세)
            var name = property.Name;
            
            // 간단한 한글 변환 (실제로는 리소스 파일 사용 권장)
            var translations = new Dictionary<string, string>
            {
                { "OrderDetails", "주문상세" },
                { "Orders", "주문" },
                { "Employees", "직원" },
                { "Invoices", "송장" },
                { "Contacts", "연락처" },
                { "Products", "제품" },
                { "Tasks", "작업" },
                { "Projects", "프로젝트" }
            };
            
            return translations.ContainsKey(name) ? translations[name] : name;
        }
    }

    // Model 확장 인터페이스
    public interface IModelMasterDetailListView : IModelListView
    {
        [Category("Master-Detail")]
        [DefaultValue("Vertical")]
        string SplitOrientation { get; set; }

        [Category("Master-Detail")]
        [DefaultValue(300)]
        int SplitterPosition { get; set; }
    }
} 