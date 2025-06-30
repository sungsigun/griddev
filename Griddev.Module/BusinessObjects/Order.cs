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
using System.Drawing;

namespace Griddev.Module.BusinessObjects
{
    // OrderStatus 열거형 추가
    public enum OrderStatus
    {
        [XafDisplayName("신규")]
        New = 0,
        
        [XafDisplayName("확정")]
        Confirmed = 1,
        
        [XafDisplayName("생산중")]
        InProgress = 2,
        
        [XafDisplayName("완료")]
        Completed = 3,
        
        [XafDisplayName("취소")]
        Cancelled = 4
    }

    [DefaultClassOptions]
    [NavigationItem("주문 관리")]
    [DefaultProperty("DisplayName")]
    [ImageName("BO_Order")]
    [XafDisplayName("주문 등록")]
    
    // 상태별 색상 구분 - enum 값으로 수정
    [Appearance("StatusNew", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Status = 0", FontColor = "Blue")]
    [Appearance("StatusConfirmed", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Status = 1", FontColor = "Green")]
    [Appearance("StatusInProgress", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Status = 2", FontColor = "Orange")]
    [Appearance("StatusCompleted", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Status = 3", FontColor = "DarkGreen", FontStyle = FontStyle.Bold)]
    [Appearance("StatusCancelled", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Status = 4", FontColor = "Red")]
    
    public class Order : BaseObject
    {
        public Order(Session session) : base(session) { }

        private string _orderNo;
        private DateTime _orderDate;
        private string _customerCode;
        private string _customerName;
        private string _projectName;
        private DateTime _deliveryDate;
        private OrderStatus? _status = OrderStatus.New;
        private string _salesPerson;
        private decimal _totalAmount;
        private string _remarks;

        #region 기본 속성들
        [Index(0)]
        [RuleRequiredField(DefaultContexts.Save)]
        [Size(50)]
        public string OrderNo
        {
            get => _orderNo;
            set => SetPropertyValue(nameof(OrderNo), ref _orderNo, value);
        }

        [Index(1)]
        [RuleRequiredField(DefaultContexts.Save)]
        public DateTime OrderDate
        {
            get => _orderDate;
            set => SetPropertyValue(nameof(OrderDate), ref _orderDate, value);
        }

        [Index(2)]
        [RuleRequiredField(DefaultContexts.Save)]
        [Size(50)]
        public string CustomerCode
        {
            get => _customerCode;
            set => SetPropertyValue(nameof(CustomerCode), ref _customerCode, value);
        }

        [Index(3)]
        [RuleRequiredField(DefaultContexts.Save)]
        [Size(200)]
        public string CustomerName
        {
            get => _customerName;
            set => SetPropertyValue(nameof(CustomerName), ref _customerName, value);
        }

        [Index(4)]
        [Size(300)]
        public string ProjectName
        {
            get => _projectName;
            set => SetPropertyValue(nameof(ProjectName), ref _projectName, value);
        }

        [Index(5)]
        [RuleRequiredField(DefaultContexts.Save)]
        public DateTime DeliveryDate
        {
            get => _deliveryDate;
            set => SetPropertyValue(nameof(DeliveryDate), ref _deliveryDate, value);
        }

        [Index(6)]
        [RuleRequiredField("Order_Status_Required", DefaultContexts.Save)]
        public OrderStatus? Status
        {
            get { return _status; }
            set { SetPropertyValue(nameof(Status), ref _status, value); }
        }

        [Index(7)]
        [Size(100)]
        public string SalesPerson
        {
            get => _salesPerson;
            set => SetPropertyValue(nameof(SalesPerson), ref _salesPerson, value);
        }

        [Index(8)]
        [ModelDefault("AllowEdit", "False")]
        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal TotalAmount
        {
            get => _totalAmount;
            set => SetPropertyValue(nameof(TotalAmount), ref _totalAmount, value);
        }

        [Index(9)]
        [Size(SizeAttribute.Unlimited)]
        public string Remarks
        {
            get => _remarks;
            set => SetPropertyValue(nameof(Remarks), ref _remarks, value);
        }
        #endregion

        #region 연관 관계
        [Association("Order-OrderDetails")]
        [DevExpress.Xpo.Aggregated]
        public XPCollection<OrderDetail> OrderDetails
        {
            get => GetCollection<OrderDetail>(nameof(OrderDetails));
        }

        [Association("Order-OrderHistories")]
        [DevExpress.Xpo.Aggregated]
        [System.ComponentModel.DisplayName("주문이력")]
        public XPCollection<OrderHistory> OrderHistories
        {
            get => GetCollection<OrderHistory>(nameof(OrderHistories));
        }
        #endregion

        #region 계산된 속성들
        [Browsable(false)]
        public string DisplayName => $"{OrderNo} - {CustomerName}";

        [Browsable(false)]  // ListView에서 숨김
        public int DetailCount => OrderDetails?.Count ?? 0;
        #endregion

        #region 메서드들
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            OrderDate = DateTime.Today;
            DeliveryDate = DateTime.Today.AddDays(30);
            Status = OrderStatus.New;
            TotalAmount = 0;
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            if (!Session.IsObjectsLoading)
            {
                UpdateTotalAmount();
            }
        }

        public void UpdateTotalAmount()
        {
            if (OrderDetails != null)
            {
            TotalAmount = OrderDetails.Sum(d => d.Amount);
        }
    }

        protected override void OnDeleting()
        {
            // 하위 상세도 함께 삭제
            if (!Session.IsObjectsLoading && OrderDetails != null)
            {
                var detailsList = OrderDetails.ToList();
                foreach (OrderDetail detail in detailsList)
                {
                    Session.Delete(detail);
                }
            }
            
            base.OnDeleting();
        }
        #endregion

        #region 검증 규칙들
        [RuleFromBoolProperty("RuleFromBoolProperty for Order.ValidateDeliveryDate", 
            DefaultContexts.Save, "납기일자는 주문일자보다 늦어야 합니다.")]
        [Browsable(false)]
        public bool ValidateDeliveryDate => DeliveryDate >= OrderDate;

        [RuleFromBoolProperty("RuleFromBoolProperty for Order.ValidateStatus", 
            DefaultContexts.Save, "상태를 선택해주세요.")]
        [Browsable(false)]
        public bool ValidateStatus => Status != OrderStatus.New;
        #endregion

        public override string ToString()
        {
            return DisplayName;
        }
    }
} 