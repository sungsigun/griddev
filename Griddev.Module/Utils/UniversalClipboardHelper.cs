using DevExpress.ExpressApp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Griddev.Module.Utils
{
    /// <summary>
    /// 클립보드 서비스 인터페이스 - 플랫폼별 구현 분리
    /// </summary>
    public interface IClipboardService
    {
        void SetText(string text);
        string GetText();
        bool ContainsText();
    }

    /// <summary>
    /// 범용 클립보드 기능을 위한 헬퍼 클래스 (Module 프로젝트용)
    /// </summary>
    public static class UniversalClipboardHelper
    {
        // 간단한 메모리 클립보드 (임시용)
        private static List<Dictionary<string, object>> _clipboardData = new List<Dictionary<string, object>>();

        /// <summary>
        /// 데이터를 클립보드에 복사 (메모리 클립보드 사용)
        /// </summary>
        public static void CopyToSystemClipboard(List<Dictionary<string, object>> data, ClipboardConfiguration config)
        {
            try
            {
                _clipboardData = data?.ToList() ?? new List<Dictionary<string, object>>();
                System.Diagnostics.Debug.WriteLine($"[복사] {_clipboardData.Count}개 항목 복사됨");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"클립보드 복사 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 클립보드에서 데이터를 파싱 (메모리 클립보드 사용)
        /// </summary>
        public static List<Dictionary<string, object>> ParseFromSystemClipboard(ClipboardConfiguration config)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[클립보드 파싱] {_clipboardData.Count}개 항목 파싱됨");
                return _clipboardData?.ToList() ?? new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"클립보드 파싱 실패: {ex.Message}");
                return new List<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// 클립보드 데이터로부터 새로운 객체들을 생성
        /// </summary>
        public static List<object> CreateItemsFromClipboard(List<Dictionary<string, object>> clipboardData, 
            ClipboardConfiguration config, IObjectSpace objectSpace, Type objectType)
        {
            var result = new List<object>();

            foreach (var data in clipboardData)
            {
                var newItem = objectSpace.CreateObject(objectType);
                
                foreach (var column in config.Columns)
                {
                    if (data.ContainsKey(column.PropertyName))
                    {
                        var property = newItem.GetType().GetProperty(column.PropertyName);
                        if (property != null && property.CanWrite)
                        {
                            try
                            {
                                var value = ConvertValue(data[column.PropertyName], property.PropertyType);
                                property.SetValue(newItem, value);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"속성 설정 실패: {column.PropertyName} = {data[column.PropertyName]}, 오류: {ex.Message}");
                            }
                        }
                    }
                }

                result.Add(newItem);
            }

            return result;
        }

        /// <summary>
        /// 값을 지정된 타입으로 변환
        /// </summary>
        private static object ConvertValue(object value, Type targetType)
        {
            try
            {
                if (value == null)
                    return null;

                if (targetType == typeof(string))
                    return value.ToString();
                
                if (targetType == typeof(decimal) || targetType == typeof(decimal?))
                {
                    if (decimal.TryParse(value.ToString(), out var d))
                        return d;
                    return targetType == typeof(decimal?) ? (decimal?)null : 0m;
                }
                
                if (targetType == typeof(int) || targetType == typeof(int?))
                {
                    if (int.TryParse(value.ToString(), out var i))
                        return i;
                    return targetType == typeof(int?) ? (int?)null : 0;
                }
                
                if (targetType == typeof(bool) || targetType == typeof(bool?))
                {
                    if (bool.TryParse(value.ToString(), out var b))
                        return b;
                    return targetType == typeof(bool?) ? (bool?)null : false;
                }

                if (targetType.IsEnum)
                {
                    if (Enum.TryParse(targetType, value.ToString(), true, out var enumValue))
                        return enumValue;
                    return null;
                }

                // 기본적으로 문자열로 반환
                return value.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"값 변환 실패: {value} -> {targetType.Name}, 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 클립보드가 비어있는지 확인
        /// </summary>
        public static bool IsEmpty => _clipboardData.Count == 0;

        /// <summary>
        /// 클립보드 비우기
        /// </summary>
        public static void Clear()
        {
            _clipboardData.Clear();
        }
    }
} 