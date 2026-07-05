namespace Futurem.Sourcing.Api.Entities;

public class SystemSetting : BaseEntity
{
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public string SettingGroup { get; set; } = "general";
    public string ValueType { get; set; } = "text";
    public bool IsSystem { get; set; }
}
