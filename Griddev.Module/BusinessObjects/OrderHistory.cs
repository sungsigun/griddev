using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using System;
using System.ComponentModel;

namespace Griddev.Module.BusinessObjects
{
    [DefaultClassOptions]
    [System.ComponentModel.DisplayName("주문이력")]
    public class OrderHistory : BaseObject
    {
        public OrderHistory(Session session) : base(session) { }

        private Order _order;
        [Association("Order-OrderHistories")]
        [System.ComponentModel.DisplayName("주문")]
        [Browsable(false)]  // Master-Detail에서 불필요 (부모 참조)
        public Order Order
        {
            get => _order;
            set => SetPropertyValue(nameof(Order), ref _order, value);
        }

        private int _sequence;
        [System.ComponentModel.DisplayName("순번")]
        public int Sequence
        {
            get => _sequence;
            set => SetPropertyValue(nameof(Sequence), ref _sequence, value);
        }

        private DateTime _actionDate = DateTime.Now;
        [System.ComponentModel.DisplayName("처리일시")]
        public DateTime ActionDate
        {
            get => _actionDate;
            set => SetPropertyValue(nameof(ActionDate), ref _actionDate, value);
        }

        private string _actionType;
        [System.ComponentModel.DisplayName("처리유형")]
        [Size(50)]
        public string ActionType
        {
            get => _actionType;
            set => SetPropertyValue(nameof(ActionType), ref _actionType, value);
        }

        private string _actionBy;
        [System.ComponentModel.DisplayName("처리자")]
        [Size(100)]
        public string ActionBy
        {
            get => _actionBy;
            set => SetPropertyValue(nameof(ActionBy), ref _actionBy, value);
        }

        private string _previousStatus;
        [System.ComponentModel.DisplayName("이전상태")]
        [Size(50)]
        public string PreviousStatus
        {
            get => _previousStatus;
            set => SetPropertyValue(nameof(PreviousStatus), ref _previousStatus, value);
        }

        private string _newStatus;
        [System.ComponentModel.DisplayName("새상태")]
        [Size(50)]
        public string NewStatus
        {
            get => _newStatus;
            set => SetPropertyValue(nameof(NewStatus), ref _newStatus, value);
        }

        private string _remarks;
        [System.ComponentModel.DisplayName("비고")]
        [Size(500)]
        public string Remarks
        {
            get => _remarks;
            set => SetPropertyValue(nameof(Remarks), ref _remarks, value);
        }

        private string _changeDetails;
        [System.ComponentModel.DisplayName("변경내용")]
        [Size(1000)]
        public string ChangeDetails
        {
            get => _changeDetails;
            set => SetPropertyValue(nameof(ChangeDetails), ref _changeDetails, value);
        }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            ActionDate = DateTime.Now;
            ActionBy = Environment.UserName; // 현재 사용자
            
            // 순번 자동 설정 (OrderDetail과 동일한 방식)
            if (Order != null)
            {
                Sequence = (Order.OrderHistories?.Count ?? 0) + 1;
            }
        }

        protected override void OnChanged(string propertyName, object oldValue, object newValue)
        {
            base.OnChanged(propertyName, oldValue, newValue);
            
            // Order가 변경되면 순번 업데이트 (OrderDetail과 동일한 방식)
            if (propertyName == nameof(Order) && Order != null && !IsDeleted)
            {
                if (Sequence == 0)
                {
                    Sequence = (Order.OrderHistories?.Count ?? 0) + 1;
                }
            }
        }
    }
} 