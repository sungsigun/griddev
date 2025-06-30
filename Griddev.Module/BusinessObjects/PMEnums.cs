namespace Griddev.Module.BusinessObjects
{
    // 예지보전 관련 열거형 모음
    public enum MaintenancePriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    public enum PMPlanStatus
    {
        Draft,
        Approved,
        InProgress,
        Closed,
        Canceled
    }

    public enum PMTaskStatus
    {
        Pending,
        Working,
        Done
    }

    public enum PMTaskType
    {
        Inspection,
        Lubrication,
        Replacement,
        Calibration,
        Cleaning
    }
} 