using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Base.General;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Griddev.Module.BusinessObjects
{
    [DefaultClassOptions]
    [NavigationItem("BOM 관리")]
    [DefaultProperty("DisplayName")]
    [ImageName("BO_Product")]
    [XafDisplayName("BOM 등록")]
    [ModelDefault("EditorTypeName", "DevExpress.ExpressApp.TreeListEditors.Win.TreeListEditor")]
    
    // 레벨별 텍스트 색상 구분
    [Appearance("Level0", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Level = 0", FontColor = "Blue", FontStyle = FontStyle.Bold)]
    [Appearance("Level1", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Level = 1", FontColor = "Green")]
    [Appearance("Level2", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Level = 2", FontColor = "DarkOrange")]
    [Appearance("Level3Plus", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Level >= 3", FontColor = "Gray")]
        
    public class BOMItem : BaseObject, ITreeNode
    {
        public BOMItem(Session session) : base(session) { }

        private decimal? _quantity;
        private BOMItem _parent;
        private int _level;
        private Item _item;
        private string _version;
        private bool _isActive;

        #region 기본 속성들
        [DataSourceProperty("AvailableItems")]
        [RuleRequiredField(DefaultContexts.Save)]
        [Browsable(false)] // Item 컬럼 숨김
        [Association("Item-BOMItems")]
        public Item Item
        {
            get => _item;
            set => SetPropertyValue(nameof(Item), ref _item, value);
        }

        [Size(20)]
        [Index(10)]
        [ModelDefault("AllowEdit", "False")]
        public string Version
        {
            get => _version;
            set => SetPropertyValue(nameof(Version), ref _version, value);
        }

        [Index(11)]
        [ModelDefault("AllowEdit", "False")]
        public bool IsActive
        {
            get => _isActive;
            set => SetPropertyValue(nameof(IsActive), ref _isActive, value);
        }

        // 표시 컬럼들 - 레벨에 들여쓰기 적용
        [Index(0)]
        [ModelDefault("AllowEdit", "False")]
        [Size(60)]
        public string LevelDisplay 
        {
            get
            {
                // 레벨별 들여쓰기 + 숫자만 표시
                var indent = new string(' ', Level * 3);
                return $"{indent}{Level}";
            }
        }

        [Index(1)]
        [Size(80)]
        [ModelDefault("AllowEdit", "False")]
        public string ItemCode => Item?.ItemCode ?? "N/A";

        [Index(2)]
        [Size(200)]
        [ModelDefault("AllowEdit", "False")]
        public string ItemName => Item?.ItemName ?? "삭제된 품목";

        [Index(3)]
        [Size(80)]
        [ModelDefault("AllowEdit", "False")]
        public string ItemTypeDisplay
        {
            get
            {
                return (Item?.ItemType ?? ItemType.원재료) switch
                {
                    ItemType.완제품 => "완제품",
                    ItemType.반제품 => "반제품",
                    ItemType.원재료 => "원재료",
                    ItemType.부자재 => "부자재",
                    _ => "기타"
                };
            }
        }

        [Index(4)]
        [ModelDefault("DisplayFormat", "{0:N2}")]
        [ModelDefault("EditMask", "f2")]
        [Size(80)]
        public decimal? Quantity
        {
            get => _quantity;
            set => SetPropertyValue(nameof(Quantity), ref _quantity, value);
        }

        [Index(5)]
        [Size(50)]
        [ModelDefault("AllowEdit", "False")]
        public string Unit => Item?.Unit ?? "EA";

        // 내부 레벨 속성 (숨김)
        [Browsable(false)]
        public int Level
        {
            get => _level;
            set => SetPropertyValue(nameof(Level), ref _level, value);
        }

        [Browsable(false)]
        public string Description => Item?.Description ?? "";
        #endregion

        #region ITreeNode 구현
        [Association("BOMItem-Children")]
        public BOMItem Parent
        {
            get => _parent;
            set
            {
                SetPropertyValue(nameof(Parent), ref _parent, value);
                if (!Session.IsObjectsLoading)
                {
                    UpdateLevel();
                }
            }
        }

        [Association("BOMItem-Children")]
        [DevExpress.Xpo.Aggregated]
        public XPCollection<BOMItem> Children
        {
            get => GetCollection<BOMItem>(nameof(Children));
        }

        // ITreeNode 인터페이스 명시적 구현
        ITreeNode ITreeNode.Parent => Parent;
        IBindingList ITreeNode.Children => Children;
        string ITreeNode.Name => DisplayName;
        #endregion

        #region 계산된 속성들
        [Browsable(false)]
        public string DisplayName => $"{ItemCode} - {ItemName}";

        [Browsable(false)]
        public XPCollection<Item> AvailableItems
        {
            get
            {
                return new XPCollection<Item>(Session, 
                    CriteriaOperator.Parse("IsActive = True"));
            }
        }
        #endregion

        #region 메서드들
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            Quantity = 1M;
            Version = "1.0";
            IsActive = true;
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            if (!Session.IsObjectsLoading)
            {
                UpdateLevel();
            }
        }

        protected override void OnDeleting()
        {
            // 하위 트리도 함께 삭제
            if (!Session.IsObjectsLoading)
            {
                var childrenList = Children.ToList();
                foreach (BOMItem child in childrenList)
                {
                    Session.Delete(child);
                }
            }
            
            base.OnDeleting();
        }

        private void UpdateLevel()
        {
            if (Parent == null)
            {
                Level = 0;
            }
            else
            {
                Level = Parent.Level + 1;
            }

            // 하위 노드들의 레벨도 업데이트
            if (!Session.IsObjectsLoading)
            {
                var childrenList = Children.ToList();
                foreach (BOMItem child in childrenList)
                {
                    child.UpdateLevel();
                }
            }
        }

        public BOMItem CreateChild(Item item, decimal quantity = 1)
        {
            var child = new BOMItem(Session)
            {
                Item = item,
                Quantity = quantity,
                Parent = this
            };
            return child;
        }

        public List<BOMItem> GetAllChildren()
        {
            var allChildren = new List<BOMItem>();
            if (!Session.IsObjectsLoading)
            {
                var childrenList = Children.ToList();
                foreach (BOMItem child in childrenList)
                {
                    allChildren.Add(child);
                    allChildren.AddRange(child.GetAllChildren());
                }
            }
            return allChildren;
        }

        public bool IsChildOf(BOMItem potentialParent)
        {
            var current = Parent;
            while (current != null)
            {
                if (current == potentialParent)
                    return true;
                current = current.Parent;
            }
            return false;
        }

        public BOMItem CreateNewVersion()
        {
            if (Parent != null)
            {
                throw new InvalidOperationException("최상위 BOM에서만 새 버전을 생성할 수 있습니다.");
            }

            // 현재 버전을 비활성화
            IsActive = false;

            // 새 버전 번호 계산
            var currentVersionNumber = GetVersionNumber(Version);
            var newVersionNumber = currentVersionNumber + 0.1;
            var newVersionString = newVersionNumber.ToString("F1");

            // 새 버전 생성
            var newVersion = new BOMItem(Session)
            {
                Item = this.Item,
                Quantity = this.Quantity,
                Version = newVersionString,
                IsActive = true,
                Parent = null
            };

            // 하위 구조 복사
            CopyChildrenToNewVersion(this, newVersion);

            return newVersion;
        }

        private void CopyChildrenToNewVersion(BOMItem source, BOMItem target)
        {
            foreach (BOMItem child in source.Children)
            {
                var newChild = new BOMItem(Session)
                {
                    Item = child.Item,
                    Quantity = child.Quantity,
                    Version = target.Version,
                    IsActive = true,
                    Parent = target
                };

                // 재귀적으로 하위 구조 복사
                CopyChildrenToNewVersion(child, newChild);
            }
        }

        private double GetVersionNumber(string version)
        {
            if (string.IsNullOrEmpty(version))
                return 1.0;

            if (double.TryParse(version, out double result))
                return result;

            return 1.0;
        }
        #endregion

        #region 검증 규칙들
        [RuleFromBoolProperty("RuleFromBoolProperty for BOMItem.ValidateQuantity", 
            DefaultContexts.Save, "수량은 0보다 커야 합니다.")]
        [Browsable(false)]
        public bool ValidateQuantity => Quantity > 0;

        [RuleFromBoolProperty("RuleFromBoolProperty for BOMItem.ValidateItem", 
            DefaultContexts.Save, "품목을 선택해주세요.")]
        [Browsable(false)]
        public bool ValidateItem => Item != null;
        #endregion

        public override string ToString()
        {
            return DisplayName;
        }
    }
} 