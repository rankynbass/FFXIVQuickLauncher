using System;

namespace XIVLauncher.Common;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class SettingsDescriptionAttribute : Attribute
{
    public string FriendlyName { get; set; }

    public string Description { get; set; }

    public SettingsDescriptionAttribute(string friendlyName, string description)
    {
        this.FriendlyName = friendlyName;
        this.Description = description;
    }
}