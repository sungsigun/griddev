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
    [XafDisplayName("예지보전 작업")]
    [ImageName("BO_Task")]
    [NavigationItem(false)]
    public class PMTask : BaseObject
    {
        public PMTask(Session session) : base(session) { }

        private PMPlan _plan;
        private int _sequence;
        private string _taskName;
        private PMTaskType _taskType;
        private string _description;
        private DateTime _plannedStart;
        private DateTime _plannedEnd;
        private DateTime? _actualStart;
        private DateTime? _actualEnd;
        private Employee _responsible;
        private decimal? _manHour;
        private decimal _taskCost;
        private PMTaskStatus _status;

        [Association("PMPlan-PMTasks")]
        [XafDisplayName("계획")]
        public PMPlan Plan
        {
            get => _plan;
            set => SetPropertyValue(nameof(Plan), ref _plan, value);
        }

        [ModelDefault("AllowEdit", "False")]
        public int Sequence
        {
            get => _sequence;
            set => SetPropertyValue(nameof(Sequence), ref _sequence, value);
        }

        [RuleRequiredField]
        [Size(200)]
        [XafDisplayName("작업명")]
        public string TaskName
        {
            get => _taskName;
            set => SetPropertyValue(nameof(TaskName), ref _taskName, value);
        }

        public PMTaskType TaskType
        {
            get => _taskType;
            set => SetPropertyValue(nameof(TaskType), ref _taskType, value);
        }

        [Size(SizeAttribute.Unlimited)]
        public string Description
        {
            get => _description;
            set => SetPropertyValue(nameof(Description), ref _description, value);
        }

        public DateTime PlannedStart
        {
            get => _plannedStart;
            set => SetPropertyValue(nameof(PlannedStart), ref _plannedStart, value);
        }

        public DateTime PlannedEnd
        {
            get => _plannedEnd;
            set => SetPropertyValue(nameof(PlannedEnd), ref _plannedEnd, value);
        }

        public DateTime? ActualStart
        {
            get => _actualStart;
            set => SetPropertyValue(nameof(ActualStart), ref _actualStart, value);
        }

        public DateTime? ActualEnd
        {
            get => _actualEnd;
            set => SetPropertyValue(nameof(ActualEnd), ref _actualEnd, value);
        }

        public Employee Responsible
        {
            get => _responsible;
            set => SetPropertyValue(nameof(Responsible), ref _responsible, value);
        }

        [ModelDefault("DisplayFormat", "{0:N2}")]
        public decimal? ManHour
        {
            get => _manHour;
            set { SetPropertyValue(nameof(ManHour), ref _manHour, value); UpdateTaskCost(); }
        }

        [ModelDefault("DisplayFormat", "{0:N0}")]
        [ModelDefault("AllowEdit", "False")]
        public decimal TaskCost
        {
            get => _taskCost;
            set => SetPropertyValue(nameof(TaskCost), ref _taskCost, value);
        }

        public PMTaskStatus Status
        {
            get => _status;
            set => SetPropertyValue(nameof(Status), ref _status, value);
        }

        [Association("PMTask-PMTaskMaterials"), DevExpress.Xpo.Aggregated]
        public XPCollection<PMTaskMaterial> Materials => GetCollection<PMTaskMaterial>(nameof(Materials));

        internal void UpdateTaskCost()
        {
            const decimal defaultHourRate = 50000m; // 예시 단가
            var laborCost = (ManHour ?? 0) * defaultHourRate;
            var materialCost = Materials.Sum(m => m.Amount);
            TaskCost = laborCost + materialCost;
            Plan?.UpdateTotalCost();
        }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            PlannedStart = DateTime.Today;
            PlannedEnd = DateTime.Today;
            Status = PMTaskStatus.Pending;
            ManHour = 1;
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            UpdateTaskCost();
        }

        protected override void OnChanged(string propertyName, object oldValue, object newValue)
        {
            base.OnChanged(propertyName, oldValue, newValue);
            if (propertyName == nameof(Plan) && Plan != null && Sequence == 0)
            {
                Sequence = (Plan.Tasks?.Count ?? 0) + 1;
            }
        }
    }
} 