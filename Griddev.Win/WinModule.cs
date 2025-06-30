using System.ComponentModel;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Updating;
using DevExpress.ExpressApp.Model.Core;
using DevExpress.ExpressApp.Model.DomainLogics;
using DevExpress.ExpressApp.Model.NodeGenerators;
using DevExpress.ExpressApp.Win;
using DevExpress.ExpressApp.Win.Utils;
using Griddev.Win.Editors;
using Griddev.Module;

namespace Griddev.Win;

[ToolboxItemFilter("Xaf.Platform.Win")]
// For more typical usage scenarios, be sure to check out https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.ModuleBase.
public sealed class GriddevWinModule : ModuleBase {
    public GriddevWinModule() {
        DevExpress.ExpressApp.Editors.FormattingProvider.UseMaskSettings = true;
        RequiredModuleTypes.Add(typeof(GriddevModule));
    }
    public override IEnumerable<ModuleUpdater> GetModuleUpdaters(IObjectSpace objectSpace, Version versionFromDB) {
        return ModuleUpdater.EmptyModuleUpdaters;
    }
    public override void Setup(XafApplication application) {
        base.Setup(application);
        // Manage various aspects of the application UI and behavior at the module level.
    }
    
    public override void ExtendModelInterfaces(ModelInterfaceExtenders extenders)
    {
        base.ExtendModelInterfaces(extenders);
        // MasterDetailListEditor용 Model Extension 등록
        extenders.Add<IModelListView, IModelMasterDetailListView>();
    }
}
