using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Griddev.Module.BusinessObjects
{
    [DefaultClassOptions]
    [NavigationItem(false)]  // 네비게이션에서 숨김 (마스터 화면에서만 접근)
    [DefaultProperty("DisplayName")]
    [ImageName("BO_OrderDetail")]
    [XafDisplayName("주문 상세")]
    
    // 수량이 0이거나 음수일 때 경고 표시
    [Appearance("ZeroQuantity", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Quantity <= 0", BackColor = "LightPink")]
    [Appearance("HighAmount", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Amount > 1000000", BackColor = "LightYellow")]
    
    public class OrderDetail : BaseObject
    {
        public OrderDetail(Session session) : base(session) { }

        private Order _order;
        private int _sequence;
        private string _itemCode;
        private string _itemName;
        private string _specification;
        private decimal? _quantity;
        private string _unit;
        private decimal? _unitPrice;
        private decimal _amount;
        private DateTime _deliveryDate;
        private string _remarks;

        #region 기본 속성들
        [Association("Order-OrderDetails")]
        [Index(0)]
        [RuleRequiredField(DefaultContexts.Save)]
        [Browsable(false)]  // Master-Detail에서 불필요 (부모 참조)
        public Order Order
        {
            get => _order;
            set 
            {
                try
                {
                    // 삭제 중이거나 Session이 유효하지 않은 경우 직접 필드 설정
                    if (IsDeleted || !IsSessionValid())
                    {
                        _order = value;
                    }
                    else
                    {
                        SetPropertyValue(nameof(Order), ref _order, value);
        }
                }
                catch (ObjectDisposedException)
                {
                    // Session이 해제된 경우 직접 필드 설정
                    _order = value;
                    System.Diagnostics.Debug.WriteLine("Order setter: Session이 해제되어 직접 필드 설정을 사용합니다.");
                }
            }
        }

        [Index(1)]
        [ModelDefault("AllowEdit", "False")]
        public int Sequence
        {
            get => _sequence;
            set => SetPropertyValue(nameof(Sequence), ref _sequence, value);
        }

        [Index(2)]
        [RuleRequiredField(DefaultContexts.Save)]
        [Size(50)]
        public string ItemCode
        {
            get => _itemCode;
            set => SetPropertyValue(nameof(ItemCode), ref _itemCode, value);
        }

        [Index(3)]
        [RuleRequiredField(DefaultContexts.Save)]
        [Size(200)]
        public string ItemName
        {
            get => _itemName;
            set => SetPropertyValue(nameof(ItemName), ref _itemName, value);
        }

        [Index(4)]
        [Size(300)]
        public string Specification
        {
            get => _specification;
            set => SetPropertyValue(nameof(Specification), ref _specification, value);
        }

        [Index(5)]
        [RuleRequiredField(DefaultContexts.Save)]
        [RuleRange("Quantity", DefaultContexts.Save, 0.01, 999999.99, "수량은 0.01 ~ 999,999.99 범위여야 합니다.")]
        [ModelDefault("DisplayFormat", "{0:N2}")]
        public decimal? Quantity
        {
            get => _quantity;
            set
            {
                SetPropertyValue(nameof(Quantity), ref _quantity, value);
                try
                {
                UpdateAmount();
                }
                catch (ObjectDisposedException)
                {
                    // Session이 해제된 경우 무시
                }
            }
        }

        [Index(6)]
        [Size(20)]
        public string Unit
        {
            get => _unit;
            set => SetPropertyValue(nameof(Unit), ref _unit, value);
        }

        [Index(7)]
        [RuleRequiredField(DefaultContexts.Save)]
        [RuleRange("UnitPrice", DefaultContexts.Save, 0, 99999999.99, "단가는 0 ~ 99,999,999.99 범위여야 합니다.")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal? UnitPrice
        {
            get => _unitPrice;
            set
            {
                SetPropertyValue(nameof(UnitPrice), ref _unitPrice, value);
                try
                {
                UpdateAmount();
                }
                catch (ObjectDisposedException)
                {
                    // Session이 해제된 경우 무시
                }
            }
        }

        [Index(8)]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal Amount
        {
            get => _amount;
            set => SetPropertyValue(nameof(Amount), ref _amount, value);
        }

        [Index(9)]
        public DateTime DeliveryDate
        {
            get => _deliveryDate;
            set => SetPropertyValue(nameof(DeliveryDate), ref _deliveryDate, value);
        }

        [Index(10)]
        [Size(SizeAttribute.Unlimited)]
        public string Remarks
        {
            get => _remarks;
            set => SetPropertyValue(nameof(Remarks), ref _remarks, value);
        }
        #endregion

        #region 계산된 속성들
        [Browsable(false)]  // ListView에서 숨김
        public string DisplayName => $"{ItemCode} - {ItemName}";

        [Browsable(false)]  // Master-Detail에서 불필요
        public string OrderNo => Order?.OrderNo ?? string.Empty;

        [Browsable(false)]  // Master-Detail에서 불필요  
        public string CustomerName => Order?.CustomerName ?? string.Empty;
        #endregion

        #region 메서드들
        public override void AfterConstruction()
        {
            try
        {
            base.AfterConstruction();
                
                // 순번 자동 설정
                if (Order != null)
                {
                    Sequence = (Order.OrderDetails?.Count ?? 0) + 1;
                    DeliveryDate = Order.DeliveryDate;
                }
                else
                {
            DeliveryDate = DateTime.Today.AddDays(30);
        }

                Quantity = 1;
                UnitPrice = 0;
                Unit = "EA";
                UpdateAmount();
            }
            catch (ObjectDisposedException)
            {
                // Session이 해제된 경우 무시
                System.Diagnostics.Debug.WriteLine("AfterConstruction: Session이 해제되어 초기화를 건너뜁니다.");
            }
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            
            try
            {
                if (IsSessionValid() && !Session.IsObjectsLoading)
                {
                    UpdateAmount();
                    
                    // 마스터의 총액 업데이트
                    Order?.UpdateTotalAmount();
                }
            }
            catch (ObjectDisposedException)
            {
                // Session이 이미 해제된 경우 무시
                System.Diagnostics.Debug.WriteLine("OnSaving: Session이 이미 해제되어 저장 로직을 건너뜁니다.");
            }
        }

        protected override void OnDeleting()
        {
            // Session에 접근하지 않고 Order 참조만 저장
            var masterOrder = Order;
            
            base.OnDeleting();
            
            // 마스터 총액 업데이트 (Order가 유효할 때만)
            if (masterOrder != null)
            {
                try
                {
                    // 모든 접근을 안전하게 처리
                    bool canUpdate = false;
                    
                    try
                    {
                        canUpdate = !masterOrder.IsDeleted && masterOrder.Session != null && masterOrder.Session.IsConnected;
                    }
                    catch (ObjectDisposedException)
                    {
                        canUpdate = false;
                    }
                    
                    if (canUpdate)
                    {
                        masterOrder.UpdateTotalAmount();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Session이 이미 해제된 경우 무시
                    System.Diagnostics.Debug.WriteLine("OnDeleting: Session이 이미 해제되어 총액 업데이트를 건너뜁니다.");
                }
                catch (Exception ex)
                {
                    // 기타 예외도 무시하고 로그만 남김
                    System.Diagnostics.Debug.WriteLine($"OnDeleting: 총액 업데이트 중 오류 발생: {ex.Message}");
                }
            }
        }

        private bool IsSessionValid()
        {
            try
            {
                return Session != null && Session.IsConnected;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateAmount()
        {
            try
            {
                // Session이 유효하고 삭제 중이 아닐 때만 계산
                if (IsSessionValid() && !IsDeleted)
                {
                    Amount = (Quantity ?? 0) * (UnitPrice ?? 0);
                }
            }
            catch (ObjectDisposedException)
            {
                // Session이 해제된 경우 무시
                System.Diagnostics.Debug.WriteLine("UpdateAmount: Session이 해제되어 금액 계산을 건너뜁니다.");
            }
        }

        protected override void OnChanged(string propertyName, object oldValue, object newValue)
        {
            try
            {
                // Session이 유효하고 연결된 경우에만 base.OnChanged 호출
                if (IsSessionValid())
                {
                    base.OnChanged(propertyName, oldValue, newValue);
                    
                    // Order가 변경되면 순번과 납기일자 업데이트 (삭제 중이 아닐 때만)
                    if (propertyName == nameof(Order) && Order != null && !IsDeleted)
                    {
                        try
                        {
                            if (Sequence == 0)
                            {
                                Sequence = (Order.OrderDetails?.Count ?? 0) + 1;
                            }
                            
                            if (DeliveryDate == DateTime.MinValue)
            {
                DeliveryDate = Order.DeliveryDate;
            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // Order나 관련 객체가 해제된 경우 무시
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // Session이 이미 해제된 경우 완전히 무시
                System.Diagnostics.Debug.WriteLine($"OnChanged: Session이 해제되어 {propertyName} 변경을 무시합니다.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnChanged 오류: {ex.Message}");
            }
        }
        #endregion

        #region 검증 규칙들
        [RuleFromBoolProperty("RuleFromBoolProperty for OrderDetail.ValidateDeliveryDate", 
            DefaultContexts.Save, "납기일자는 주문일자보다 늦어야 합니다.")]
        [Browsable(false)]
        public bool ValidateDeliveryDate => Order == null || DeliveryDate >= Order.OrderDate;

        [RuleFromBoolProperty("RuleFromBoolProperty for OrderDetail.ValidateQuantityAndPrice", 
            DefaultContexts.Save, "수량과 단가를 모두 입력해주세요.")]
        [Browsable(false)]
        public bool ValidateQuantityAndPrice => (Quantity ?? 0) > 0 && (UnitPrice ?? 0) >= 0;
        #endregion

        public override string ToString()
        {
            return DisplayName;
        }
    }
} 