using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System.ComponentModel;
using DevExpress.ExpressApp.Model;

namespace Griddev.Module.BusinessObjects
{
    [DefaultClassOptions]
    [NavigationItem(false)]
    [XafDisplayName("작업 자재")]
    [ImageName("BO_Product")]
    public class PMTaskMaterial : BaseObject
    {
        public PMTaskMaterial(Session session) : base(session) { }

        private PMTask _task;
        private int _sequence;
        private string _materialCode;
        private string _materialName;
        private decimal? _quantity;
        private string _unit;
        private decimal? _unitCost;
        private decimal _amount;
        private string _remarks;

        [Association("PMTask-PMTaskMaterials")]
        public PMTask Task
        {
            get => _task;
            set => SetPropertyValue(nameof(Task), ref _task, value);
        }

        [ModelDefault("AllowEdit", "False")]
        public int Sequence
        {
            get => _sequence;
            set => SetPropertyValue(nameof(Sequence), ref _sequence, value);
        }

        [Size(50)]
        public string MaterialCode
        {
            get => _materialCode;
            set => SetPropertyValue(nameof(MaterialCode), ref _materialCode, value);
        }

        [Size(200)]
        public string MaterialName
        {
            get => _materialName;
            set => SetPropertyValue(nameof(MaterialName), ref _materialName, value);
        }

        [ModelDefault("DisplayFormat", "{0:N2}")]
        public decimal? Quantity
        {
            get => _quantity;
            set { SetPropertyValue(nameof(Quantity), ref _quantity, value); UpdateAmount(); }
        }

        public string Unit
        {
            get => _unit;
            set => SetPropertyValue(nameof(Unit), ref _unit, value);
        }

        [ModelDefault("DisplayFormat", "{0:N0}")]
        public decimal? UnitCost
        {
            get => _unitCost;
            set { SetPropertyValue(nameof(UnitCost), ref _unitCost, value); UpdateAmount(); }
        }

        [ModelDefault("DisplayFormat", "{0:N0}")]
        [ModelDefault("AllowEdit", "False")]
        public decimal Amount
        {
            get => _amount;
            set => SetPropertyValue(nameof(Amount), ref _amount, value);
        }

        [Size(SizeAttribute.Unlimited)]
        public string Remarks
        {
            get => _remarks;
            set => SetPropertyValue(nameof(Remarks), ref _remarks, value);
        }

        private void UpdateAmount()
        {
            Amount = (Quantity ?? 0) * (UnitCost ?? 0);
            Task?.UpdateTaskCost();
        }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Quantity = 1;
            Unit = "EA";
            UnitCost = 0;
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            UpdateAmount();
        }

        protected override void OnChanged(string propertyName, object oldValue, object newValue)
        {
            base.OnChanged(propertyName, oldValue, newValue);
            if (propertyName == nameof(Task) && Task != null && Sequence == 0)
            {
                Sequence = (Task.Materials?.Count ?? 0) + 1;
            }
        }
    }
} 