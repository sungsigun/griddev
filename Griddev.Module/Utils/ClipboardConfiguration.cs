using System;
using System.Collections.Generic;

namespace Griddev.Module.Utils
{
    /// <summary>
    /// 클립보드 기능을 위한 설정 클래스
    /// </summary>
    public class ClipboardConfiguration
    {
        public List<ClipboardColumn> Columns { get; set; } = new List<ClipboardColumn>();
        public string UniqueKeyProperty { get; set; } // 중복 체크용 속성명
        public string NoPasteItemsMessage { get; set; }
        public string ExcelPasteInstructionMessage { get; set; }
        
        // 내부적으로 사용되는 클립보드 데이터
        internal List<Dictionary<string, object>> ClipboardData { get; set; }

        /// <summary>
        /// 간편한 설정 생성 메서드
        /// </summary>
        public static ClipboardConfiguration Create(string uniqueKey = null)
        {
            return new ClipboardConfiguration
            {
                UniqueKeyProperty = uniqueKey
            };
        }

        /// <summary>
        /// 컬럼 추가
        /// </summary>
        public ClipboardConfiguration AddColumn(string propertyName, Type propertyType, string displayName = null)
        {
            Columns.Add(new ClipboardColumn(propertyName, propertyType, displayName));
            return this;
        }

        /// <summary>
        /// 메시지 설정
        /// </summary>
        public ClipboardConfiguration SetMessages(string noPasteMessage = null, string excelMessage = null)
        {
            NoPasteItemsMessage = noPasteMessage;
            ExcelPasteInstructionMessage = excelMessage;
            return this;
        }
    }

    /// <summary>
    /// 클립보드에서 처리할 컬럼 정보
    /// </summary>
    public class ClipboardColumn
    {
        public string PropertyName { get; set; }
        public string DisplayName { get; set; }
        public Type PropertyType { get; set; }

        public ClipboardColumn(string propertyName, Type propertyType, string displayName = null)
        {
            PropertyName = propertyName;
            PropertyType = propertyType;
            DisplayName = displayName ?? propertyName;
        }
    }
} 