using System.ComponentModel;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Updating;
using DevExpress.ExpressApp.Model.Core;
using DevExpress.ExpressApp.Model.DomainLogics;
using DevExpress.ExpressApp.Model.NodeGenerators;
using DevExpress.Xpo;
using DevExpress.ExpressApp.Xpo;
// using Griddev.Module.Utils;
// using Griddev.Module.Controllers;
// using Griddev.Module.BusinessObjects;

namespace Griddev.Module;

// For more typical usage scenarios, be sure to check out https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.ModuleBase.
public sealed class GriddevModule : ModuleBase {
    public GriddevModule() {
		// 
		// GriddevModule
		// 
        AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.BaseObject));
        AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.AuditDataItemPersistent));
        AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.AuditedObjectWeakReference));
        AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.FileData));
        AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.FileAttachmentBase));
        AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.HCategory));
		RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.SystemModule.SystemModule));
		RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.AuditTrail.AuditTrailModule));
		RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Objects.BusinessClassLibraryCustomizationModule));
		RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.CloneObject.CloneObjectModule));
		RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.ConditionalAppearance.ConditionalAppearanceModule));
		RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Office.OfficeModule));
		RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.StateMachine.StateMachineModule));
		RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.TreeListEditors.TreeListEditorsModuleBase));
		RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Validation.ValidationModule));
		RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.ViewVariantsModule.ViewVariantsModule));
    }
    public override IEnumerable<ModuleUpdater> GetModuleUpdaters(IObjectSpace objectSpace, Version versionFromDB) {
        ModuleUpdater updater = new DatabaseUpdate.Updater(objectSpace, versionFromDB);
        return new ModuleUpdater[] { updater };
    }
    public override void Setup(XafApplication application) {
        base.Setup(application);
        // Manage various aspects of the application UI and behavior at the module level.
        
        // 클립보드 설정 등록 (BOMTREE 패턴) - 주석처리
        // SetupClipboardConfigurations();
    }
    
    /*
    /// <summary>
    /// 클립보드 설정을 등록합니다 (BOMTREE 패턴)
    /// </summary>
    private void SetupClipboardConfigurations()
    {
        // Item (품목등록) 클립보드 설정
        var itemConfig = ClipboardConfiguration
            .Create() // 모든 데이터 붙여넣기
            .AddColumn("ItemCode", typeof(string), "품목코드")
            .AddColumn("ItemName", typeof(string), "품목명")
            .AddColumn("ItemType", typeof(ItemType), "품목유형")
            .AddColumn("Specification", typeof(string), "규격")
            .AddColumn("Unit", typeof(string), "단위")
            .AddColumn("UnitPrice", typeof(decimal?), "단가")
            .AddColumn("IsActive", typeof(bool), "활성상태")
            .SetMessages(
                "붙여넣을 새로운 품목이 없습니다.",
                "Excel에서 다음 형식으로 복사해주세요:\n품목코드  품목명  품목유형  규격  단위  단가  활성상태"
            );

        UniversalClipboardController.RegisterClipboardConfiguration<Item>(itemConfig);
        
        // 추가 Business Object들의 클립보드 설정을 여기에 등록
        // 예: Customer, Order, Product 등
    }
    */
    
    public override void CustomizeTypesInfo(ITypesInfo typesInfo) {
        base.CustomizeTypesInfo(typesInfo);
        CalculatedPersistentAliasHelper.CustomizeTypesInfo(typesInfo);
    }
}
