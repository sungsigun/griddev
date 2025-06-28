using DevExpress.ExpressApp;
using DevExpress.Data.Filtering;
using DevExpress.Persistent.Base;
using DevExpress.ExpressApp.Updating;
using DevExpress.Xpo;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.BaseImpl;
using Griddev.Module.BusinessObjects;

namespace Griddev.Module.DatabaseUpdate;

// For more typical usage scenarios, be sure to check out https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.Updating.ModuleUpdater
public class Updater : ModuleUpdater {
    public Updater(IObjectSpace objectSpace, Version currentDBVersion) :
        base(objectSpace, currentDBVersion) {
    }
    public override void UpdateDatabaseAfterUpdateSchema() {
        base.UpdateDatabaseAfterUpdateSchema();
        
        // Department 테스트 데이터 생성
        if (ObjectSpace.GetObjectsCount(typeof(Department), null) == 0)
        {
            var rootDept = ObjectSpace.CreateObject<Department>();
            rootDept.Name = "본사";
            rootDept.EmployeeCount = 10;
            
            var salesDept = ObjectSpace.CreateObject<Department>();
            salesDept.Name = "영업부";
            salesDept.EmployeeCount = 15;
            salesDept.ParentDepartment = rootDept;
            
            var devDept = ObjectSpace.CreateObject<Department>();
            devDept.Name = "개발부";
            devDept.EmployeeCount = 20;
            devDept.ParentDepartment = rootDept;
            
            var subDevDept = ObjectSpace.CreateObject<Department>();
            subDevDept.Name = "개발1팀";
            subDevDept.EmployeeCount = 8;
            subDevDept.ParentDepartment = devDept;
            
            ObjectSpace.CommitChanges();
        }
    }
    public override void UpdateDatabaseBeforeUpdateSchema() {
        base.UpdateDatabaseBeforeUpdateSchema();
        //if(CurrentDBVersion < new Version("1.1.0.0") && CurrentDBVersion > new Version("0.0.0.0")) {
        //    RenameColumn("DomainObject1Table", "OldColumnName", "NewColumnName");
        //}
    }
}
