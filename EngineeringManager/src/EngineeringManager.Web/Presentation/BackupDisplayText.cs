using EngineeringManager.Application.Backups;
using EngineeringManager.Domain.DataExchange;

namespace EngineeringManager.Web.Presentation;

public static class BackupDisplayText
{
    public static string Kind(BackupKind value) => value switch
    {
        BackupKind.Settings => "设置备份",
        BackupKind.Full => "完整备份",
        _ => "未知备份类型"
    };

    public static string ScheduleMode(BackupScheduleMode value) => value switch
    {
        BackupScheduleMode.Disabled => "停用",
        BackupScheduleMode.Interval => "按间隔",
        BackupScheduleMode.FixedTime => "固定时间",
        _ => "未知执行方式"
    };

    public static string TaskStatus(DataExchangeTaskStatus value) => value switch
    {
        DataExchangeTaskStatus.Pending => "排队中",
        DataExchangeTaskStatus.PreviewReady => "预览待确认",
        DataExchangeTaskStatus.Running => "处理中",
        DataExchangeTaskStatus.Completed => "已完成",
        DataExchangeTaskStatus.Failed => "失败",
        _ => "未知状态"
    };

    public static string TargetStatus(BackupTargetStatus value) => value switch
    {
        BackupTargetStatus.NotConfigured => "未配置",
        BackupTargetStatus.Pending => "处理中",
        BackupTargetStatus.Succeeded => "成功",
        BackupTargetStatus.Failed => "失败",
        _ => "未知状态"
    };
}
