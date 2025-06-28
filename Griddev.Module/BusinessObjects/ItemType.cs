using DevExpress.Persistent.Base;
using System.ComponentModel;

namespace Griddev.Module.BusinessObjects
{
    public enum ItemType
    {
        [ImageName("BO_Product")]
        [Description("완제품")]
        완제품 = 0,
        
        [ImageName("BO_Assembly")]
        [Description("반제품")]
        반제품 = 1,
        
        [ImageName("BO_Part")]
        [Description("원재료")]
        원재료 = 2,
        
        [ImageName("BO_Part")]
        [Description("부자재")]
        부자재 = 3
    }
} 