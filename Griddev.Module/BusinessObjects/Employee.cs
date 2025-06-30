using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Griddev.Module.BusinessObjects
{
    [DefaultClassOptions]
    [ImageName("BO_Person")] // generic icon
    [XafDisplayName("사원")]
    public class Employee : BaseObject
    {
        public Employee(Session session) : base(session) { }

        private string _employeeNo;
        private string _fullName;

        [Size(20)]
        [XafDisplayName("사번")]
        public string EmployeeNo
        {
            get => _employeeNo;
            set => SetPropertyValue(nameof(EmployeeNo), ref _employeeNo, value);
        }

        [Size(100)]
        [XafDisplayName("이름")]
        public string FullName
        {
            get => _fullName;
            set => SetPropertyValue(nameof(FullName), ref _fullName, value);
        }

        public override string ToString()
        {
            return FullName;
        }
    }
} 