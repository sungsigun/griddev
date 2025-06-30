using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Griddev.Module.BusinessObjects
{
    [DefaultClassOptions]
    [ImageName("BO_Machine")] // generic icon
    [XafDisplayName("설비")]
    public class Equipment : BaseObject
    {
        public Equipment(Session session) : base(session) { }

        private string _equipmentCode;
        private string _equipmentName;

        [Size(50)]
        [XafDisplayName("설비코드")]
        public string EquipmentCode
        {
            get => _equipmentCode;
            set => SetPropertyValue(nameof(EquipmentCode), ref _equipmentCode, value);
        }

        [Size(100)]
        [XafDisplayName("설비명")]
        public string EquipmentName
        {
            get => _equipmentName;
            set => SetPropertyValue(nameof(EquipmentName), ref _equipmentName, value);
        }

        [Association("Equipment-PMPlans")]
        public XPCollection<PMPlan> PMPlans => GetCollection<PMPlan>(nameof(PMPlans));

        public override string ToString()
        {
            return EquipmentName;
        }
    }
} 