using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using System.Drawing;

namespace Griddev.Module.BusinessObjects
{
    // 모니터링 상태 열거형
    public enum MonitoringStatus
    {
        [XafDisplayName("정상")]
        Normal = 0,
        
        [XafDisplayName("주의")]
        Warning = 1,
        
        [XafDisplayName("위험")]
        Critical = 2,
        
        [XafDisplayName("장애")]
        Fault = 3,
        
        [XafDisplayName("정지")]
        Stopped = 4
    }

    [DefaultClassOptions]
    [NavigationItem("예지보전")]
    [DefaultProperty("DisplayName")]
    [ImageName("BO_Report")]
    [XafDisplayName("설비 모니터링")]
    
    // 상태별 색상 구분
    [Appearance("StatusNormal", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Status = 0", FontColor = "Green")]
    [Appearance("StatusWarning", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Status = 1", FontColor = "Orange")]
    [Appearance("StatusCritical", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Status = 2", FontColor = "Red", FontStyle = FontStyle.Bold)]
    [Appearance("StatusFault", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Status = 3", FontColor = "DarkRed", FontStyle = FontStyle.Bold)]
    [Appearance("StatusStopped", AppearanceItemType = "ViewItem", TargetItems = "*", 
        Context = "ListView", Criteria = "Status = 4", FontColor = "Gray")]
    
    public class EquipmentMonitoring : BaseObject
    {
        public EquipmentMonitoring(Session session) : base(session) { }

        private Equipment _equipment;
        private DateTime _monitoringDate;
        private MonitoringStatus _status;
        private decimal? _overallHealth;
        private string _monitoringType;
        private TimeSpan _operatingTime;
        private decimal? _efficiency;
        private string _remarks;
        private Employee _inspector;

        #region 기본 속성들
        [Association("Equipment-EquipmentMonitorings")]
        [RuleRequiredField(DefaultContexts.Save)]
        [Index(0)]
        public Equipment Equipment
        {
            get => _equipment;
            set => SetPropertyValue(nameof(Equipment), ref _equipment, value);
        }

        [Index(1)]
        [RuleRequiredField(DefaultContexts.Save)]
        public DateTime MonitoringDate
        {
            get => _monitoringDate;
            set => SetPropertyValue(nameof(MonitoringDate), ref _monitoringDate, value);
        }

        [Index(2)]
        [Size(100)]
        public string MonitoringType
        {
            get => _monitoringType;
            set => SetPropertyValue(nameof(MonitoringType), ref _monitoringType, value);
        }

        [Index(3)]
        public MonitoringStatus Status
        {
            get => _status;
            set => SetPropertyValue(nameof(Status), ref _status, value);
        }

        [Index(4)]
        [ModelDefault("DisplayFormat", "{0:N1}%")]
        [ModelDefault("EditMask", "f1")]
        public decimal? OverallHealth
        {
            get => _overallHealth;
            set => SetPropertyValue(nameof(OverallHealth), ref _overallHealth, value);
        }

        [Index(5)]
        [ModelDefault("DisplayFormat", "{0:N1}%")]
        [ModelDefault("EditMask", "f1")]
        public decimal? Efficiency
        {
            get => _efficiency;
            set => SetPropertyValue(nameof(Efficiency), ref _efficiency, value);
        }

        [Index(6)]
        public TimeSpan OperatingTime
        {
            get => _operatingTime;
            set => SetPropertyValue(nameof(OperatingTime), ref _operatingTime, value);
        }

        [Index(7)]
        public Employee Inspector
        {
            get => _inspector;
            set => SetPropertyValue(nameof(Inspector), ref _inspector, value);
        }

        [Index(8)]
        [Size(SizeAttribute.Unlimited)]
        public string Remarks
        {
            get => _remarks;
            set => SetPropertyValue(nameof(Remarks), ref _remarks, value);
        }
        #endregion

        #region 연관 관계 (디테일2)
        [Association("EquipmentMonitoring-SensorReadings")]
        [DevExpress.Xpo.Aggregated]
        [System.ComponentModel.DisplayName("센서 측정값")]
        public XPCollection<SensorReading> SensorReadings
        {
            get => GetCollection<SensorReading>(nameof(SensorReadings));
        }

        [Association("EquipmentMonitoring-AlarmHistories")]
        [DevExpress.Xpo.Aggregated]
        [System.ComponentModel.DisplayName("알람 이력")]
        public XPCollection<AlarmHistory> AlarmHistories
        {
            get => GetCollection<AlarmHistory>(nameof(AlarmHistories));
        }

        [Association("EquipmentMonitoring-PredictionResults")]
        [DevExpress.Xpo.Aggregated]
        [System.ComponentModel.DisplayName("예측 결과")]
        public XPCollection<PredictionResult> PredictionResults
        {
            get => GetCollection<PredictionResult>(nameof(PredictionResults));
        }
        #endregion

        #region 계산된 속성들
        [Browsable(false)]
        public string DisplayName => $"{Equipment?.EquipmentName} - {MonitoringDate:yyyy-MM-dd}";

        [Index(9)]
        [ModelDefault("AllowEdit", "False")]
        public int SensorCount => SensorReadings?.Count ?? 0;

        [Index(10)]
        [ModelDefault("AllowEdit", "False")]
        public int AlarmCount => AlarmHistories?.Count ?? 0;

        [Index(11)]
        [ModelDefault("AllowEdit", "False")]
        public bool HasCriticalAlarms => AlarmHistories?.Any(a => a.Severity == AlarmSeverity.Critical) ?? false;
        #endregion

        #region 메서드들
        public override void AfterConstruction()
        {
            base.AfterConstruction();
            MonitoringDate = DateTime.Now;
            Status = MonitoringStatus.Normal;
            MonitoringType = "일반점검";
            OverallHealth = 100M;
            Efficiency = 100M;
        }

        public void UpdateOverallHealth()
        {
            if (SensorReadings?.Count > 0)
            {
                var avgHealth = SensorReadings
                    .Where(s => s.HealthScore.HasValue)
                    .Average(s => s.HealthScore.Value);
                OverallHealth = (decimal?)avgHealth;
            }
        }

        public void CalculateEfficiency()
        {
            // 실제 가동시간 대비 계획 가동시간 비율
            var plannedTime = TimeSpan.FromHours(8); // 8시간 기준
            if (plannedTime.TotalHours > 0)
            {
                Efficiency = (decimal)(OperatingTime.TotalHours / plannedTime.TotalHours * 100);
            }
        }

        protected override void OnSaving()
        {
            base.OnSaving();
            if (!Session.IsObjectsLoading)
            {
                UpdateOverallHealth();
                CalculateEfficiency();
            }
        }

        public override string ToString()
        {
            return DisplayName;
        }
        #endregion
    }
} 