using System;

namespace ArcadeCam.Helpers;

// Order is intentional: the configuration dropdown lists Hidden last.
public enum HudAttachment { Aircraft, Camera, Hidden }

internal static class HudAttachmentMigration
{
    internal static HudAttachment Parse(string value, HudAttachment fallback)
    {
        if (bool.TryParse(value, out bool visible))
            return visible ? HudAttachment.Aircraft : HudAttachment.Hidden;
        return Enum.TryParse(value, true, out HudAttachment mode)
               && Enum.IsDefined(typeof(HudAttachment), mode) ? mode : fallback;
    }
}