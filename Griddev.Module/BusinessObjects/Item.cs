using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Griddev.Module.BusinessObjects
{
    [DefaultClassOptions]
    [NavigationItem("BOM 관리")]
    [DefaultProperty("DisplayName")]
    [ImageName("BO_Product")]
    [XafDisplayName("품목등록")]
    public class Item : BaseObject
    {
        public Item(Session session)
            : base(session)
        {
        }

        private string _itemCode;
        private string _itemName;
        private ItemType _itemType;
        private string _specification;
        private string _unit;
        private decimal _unitPrice;
        private string _description;
        private bool _isActive;
        private DateTime _createdDate;

        [RuleRequiredField(DefaultContexts.Save)]
        [RuleUniqueValue("RuleUniqueValue for Item.ItemCode", DefaultContexts.Save, "품목코드가 중복됩니다.")]
        [Size(50)]
        [Index(0)]
        public string ItemCode
        {
            get => _itemCode;
            set => SetPropertyValue(nameof(ItemCode), ref _itemCode, value?.ToUpper());
        }

        [RuleRequiredField(DefaultContexts.Save)]
        [Size(200)]
        [Index(1)]
        public string ItemName
        {
            get => _itemName;
            set => SetPropertyValue(nameof(ItemName), ref _itemName, value);
        }

        [Index(2)]
        public ItemType ItemType
        {
            get => _itemType;
            set => SetPropertyValue(nameof(ItemType), ref _itemType, value);
        }

        [Size(20)]
        [Index(3)]
        public string Unit
        {
            get => _unit;
            set => SetPropertyValue(nameof(Unit), ref _unit, value);
        }

        [Size(500)]
        [Index(4)]
        public string Description
        {
            get => _description;
            set => SetPropertyValue(nameof(Description), ref _description, value);
        }

        [Browsable(false)]
        public string Specification
        {
            get => _specification;
            set => SetPropertyValue(nameof(Specification), ref _specification, value);
        }

        [Browsable(false)]
        public decimal UnitPrice
        {
            get => _unitPrice;
            set => SetPropertyValue(nameof(UnitPrice), ref _unitPrice, value);
        }

        [Index(5)]
        public bool IsActive
        {
            get => _isActive;
            set => SetPropertyValue(nameof(IsActive), ref _isActive, value);
        }

        [Index(6)]
        public DateTime CreatedDate
        {
            get => _createdDate;
            set => SetPropertyValue(nameof(CreatedDate), ref _createdDate, value);
        }

        /// <summary>
        /// BOM 등록 여부 (계산 속성)
        /// </summary>
        [Index(7)]
        [ModelDefault("AllowEdit", "False")]
        public bool HasBOM
        {
            get
            {
                return BOMItems != null && BOMItems.Count > 0;
            }
        }

        /// <summary>
        /// BOM 등록 상태 텍스트 (UI 표시용)
        /// </summary>
        [Index(8)]
        [Size(80)]
        [ModelDefault("AllowEdit", "False")]
        public string BOMRegistrationStatus
        {
            get
            {
                return HasBOM ? "[등록됨]" : "";
            }
        }

        [Association("Item-BOMItems")]
        [Browsable(false)]
        public XPCollection<BOMItem> BOMItems
        {
            get => GetCollection<BOMItem>(nameof(BOMItems));
        }

        [Browsable(false)]
        public string DisplayName => $"{ItemCode} - {ItemName}";

        [Browsable(false)]
        public string ItemTypeText
        {
            get
            {
                return ItemType switch
                {
                    ItemType.완제품 => "완제품",
                    ItemType.반제품 => "반제품", 
                    ItemType.원재료 => "원재료",
                    ItemType.부자재 => "부자재",
                    _ => "기타"
                };
            }
        }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Unit = "EA";
            IsActive = true;
            CreatedDate = DateTime.Now;
            ItemType = ItemType.원재료;
            
            // 필수 필드 기본값 설정
            if (string.IsNullOrEmpty(ItemCode))
            {
                ItemCode = "";
            }
            if (string.IsNullOrEmpty(ItemName))
            {
                ItemName = "";
            }
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
} 