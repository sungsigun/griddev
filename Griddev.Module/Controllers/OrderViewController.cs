using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using Griddev.Module.BusinessObjects;

namespace Griddev.Module.Controllers
{
    // Order DetailView를 위한 컨트롤러 (ListView는 MasterDetailActionsController가 담당)
    public partial class OrderViewController : ViewController
    {
        private SimpleAction createOrderDetailAction;

        public OrderViewController()
        {
            InitializeComponent();
            
            // Order 객체에 대해서만 동작
            TargetObjectType = typeof(Order);
            
            // 주문상세 추가 액션 (DetailView에서만)
            createOrderDetailAction = new SimpleAction(this, "CreateOrderDetail", PredefinedCategory.Edit)
            {
                Caption = "상세 추가",
                ImageName = "Action_New",
                ToolTip = "새로운 주문 상세를 추가합니다."
            };
            createOrderDetailAction.Execute += CreateOrderDetailAction_Execute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            
            // DetailView에서만 활성화 (ListView는 MasterDetailActionsController가 담당)
            if (View is DetailView)
            {
                createOrderDetailAction.Active["Context"] = true;
            }
            else
            {
                createOrderDetailAction.Active["Context"] = false;
            }
        }

        private void CreateOrderDetailAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            if (View.CurrentObject is Order order)
            {
                // 새 OrderDetail 생성
                var newDetail = ObjectSpace.CreateObject<OrderDetail>();
                newDetail.Order = order;
                newDetail.Sequence = order.OrderDetails.Count + 1;
                newDetail.Quantity = 1;
                newDetail.Unit = "EA";
                
                // DetailView에서 OrderDetails 리스트에 추가
                order.OrderDetails.Add(newDetail);
                
                View.Refresh();
            }
        }
    }
} 