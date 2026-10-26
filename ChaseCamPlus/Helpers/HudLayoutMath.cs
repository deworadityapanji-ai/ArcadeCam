using System;
using UnityEngine;

namespace ArcadeCam.Helpers;

internal static class HudLayoutMath
{
    // A left-bank aircraft has its up vector pointing left on screen. Its top
    // instruments must follow left too. Euler z subtraction is unreliable off-axis.
    internal static float BankDegrees(Vector3 cameraLocalUp, Vector3 cameraLocalRight)
    {
        const float radiansToDegrees = 180f / (float)Math.PI;
        if (cameraLocalUp.x * cameraLocalUp.x + cameraLocalUp.y * cameraLocalUp.y > 0.0001f)
            return (float)Math.Atan2(-cameraLocalUp.x, cameraLocalUp.y) * radiansToDegrees;
        return (float)Math.Atan2(cameraLocalRight.y, cameraLocalRight.x) * radiansToDegrees;
    }

    internal static Vector3 PlaceOffset(Vector3 neutralOffset, HudAttachment mode, float bankDegrees)
    {
        if (mode != HudAttachment.Aircraft) return neutralOffset;
        double radians = bankDegrees * Math.PI / 180.0;
        float sin = (float)Math.Sin(radians), cos = (float)Math.Cos(radians);
        return new Vector3(neutralOffset.x * cos - neutralOffset.y * sin,
            neutralOffset.x * sin + neutralOffset.y * cos, neutralOffset.z);
    }

    // HUDApp.AppType: HUD=0, HMD=1, MFD=2. Helmet duplicates use another reference
    // frame and must never be transformed as though they belonged to HUDCenter.
    internal static bool IsFlightInstrument(int appType) => appType == 0;
}