using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Base.General;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using System.ComponentModel;

namespace Griddev.Module.BusinessObjects
{
    [DefaultClassOptions]
    [NavigationItem("테스트")]
    [XafDisplayName("부서 관리")]
    [ModelDefault("EditorTypeName", "DevExpress.ExpressApp.TreeListEditors.Win.TreeListEditor")]
    [ModelDefault("AllowEdit", "True")]
    public class Department : BaseObject, ITreeNode
    {
        public Department(Session session) : base(session) { }

        private string _name;
        private Department _parentDepartment;
        private int _employeeCount;

        [Size(100)]
        [Index(0)]
        public string Name
        {
            get => _name;
            set => SetPropertyValue(nameof(Name), ref _name, value);
        }

        [Index(1)]
        [ModelDefault("AllowEdit", "True")]
        public int EmployeeCount
        {
            get => _employeeCount;
            set => SetPropertyValue(nameof(EmployeeCount), ref _employeeCount, value);
        }

        [Association("Department-SubDepartments")]
        [Index(2)]
        public Department ParentDepartment
        {
            get => _parentDepartment;
            set => SetPropertyValue(nameof(ParentDepartment), ref _parentDepartment, value);
        }

        [Association("Department-SubDepartments")]
        [DevExpress.Xpo.Aggregated]
        public XPCollection<Department> SubDepartments
        {
            get => GetCollection<Department>(nameof(SubDepartments));
        }

        // ITreeNode 인터페이스 구현
        ITreeNode ITreeNode.Parent => ParentDepartment;
        IBindingList ITreeNode.Children => SubDepartments;
        string ITreeNode.Name => Name ?? "새 부서";

        public override string ToString()
        {
            return Name ?? "새 부서";
        }
    }
} 