using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using System.Linq;
using DevExpress.ExpressApp.Model;

namespace Griddev.Module.BusinessObjects
{
    [DefaultClassOptions]
    [XafDisplayName("예지보전 계획")]
    [ImageName("BO_Project")]
    public class PMPlan : BaseObject
    {
        public PMPlan(Session session) : base(session) { }

        private string _planNo;
        private Equipment _equipment;
        private DateTime _planDate;
        private DateTime _periodStart;
        private DateTime _periodEnd;
        private MaintenancePriority _priority;
        private PMPlanStatus _status;
        private decimal _totalCost;
        private string _remarks;

        [RuleRequiredField(DefaultContexts.Save)]
        [Size(50)]
        [XafDisplayName("계획번호")]
        public string PlanNo
        {
            get => _planNo;
            set => SetPropertyValue(nameof(PlanNo), ref _planNo, value);
        }

        [Association("Equipment-PMPlans")]
        [XafDisplayName("설비")]
        public Equipment Equipment
        {
            get => _equipment;
            set => SetPropertyValue(nameof(Equipment), ref _equipment, value);
        }

        [XafDisplayName("계획작성일")]
        public DateTime PlanDate
        {
            get => _planDate;
            set => SetPropertyValue(nameof(PlanDate), ref _planDate, value);
        }

        [XafDisplayName("기간 시작")]
        public DateTime PeriodStart
        {
            get => _periodStart;
            set => SetPropertyValue(nameof(PeriodStart), ref _periodStart, value);
        }

        [XafDisplayName("기간 종료")]
        public DateTime PeriodEnd
        {
            get => _periodEnd;
            set => SetPropertyValue(nameof(PeriodEnd), ref _periodEnd, value);
        }

        public MaintenancePriority Priority
        {
            get => _priority;
            set => SetPropertyValue(nameof(Priority), ref _priority, value);
        }

        public PMPlanStatus Status
        {
            get => _status;
            set => SetPropertyValue(nameof(Status), ref _status, value);
        }

        [ModelDefault("DisplayFormat", "{0:N0}")]
        [XafDisplayName("총 비용")]
        [ImmediatePostData]
        public decimal TotalCost
        {
            get => _totalCost;
            set => SetPropertyValue(nameof(TotalCost), ref _totalCost, value);
        }

        [Size(SizeAttribute.Unlimited)]
        public string Remarks
        {
            get => _remarks;
            set => SetPropertyValue(nameof(Remarks), ref _remarks, value);
        }

        [Association("PMPlan-PMTasks"), DevExpress.Xpo.Aggregated]
        public XPCollection<PMTask> Tasks => GetCollection<PMTask>(nameof(Tasks));

        public void UpdateTotalCost()
        {
            TotalCost = Tasks.Sum(t => t.TaskCost);
        }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            PlanDate = DateTime.Today;
            PeriodStart = DateTime.Today;
            PeriodEnd = DateTime.Today.AddMonths(1);
            Priority = MaintenancePriority.Normal;
            Status = PMPlanStatus.Draft;
        }
    }
} 