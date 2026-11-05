namespace ArcadeCam.Helpers;

public enum CameraPreset { Custom, NuclearOptionPlus, AceCombat, AceCombat8, WarThunder, Orbit }

// These are inspired starting points based on the requested feel, not game emulation.
internal sealed class PresetValues
{
    internal bool LevelHorizon;
    internal LevelCameraStyle LevelStyle;
    internal bool Centering;
    internal float Force;
    internal float CenteringDelay;
    internal bool SpeedMotion;
    internal bool PitchFollow;
    internal HudAttachment Instruments;
    internal float Distance;
    internal static PresetValues For(CameraPreset preset)
    {
        bool ace = preset == CameraPreset.AceCombat || preset == CameraPreset.AceCombat8;
        return new PresetValues {
            LevelHorizon = preset == CameraPreset.WarThunder || preset == CameraPreset.Orbit,
            LevelStyle = preset == CameraPreset.Orbit ? LevelCameraStyle.Orbit : LevelCameraStyle.Cinematic,
            Centering = ace, Force = ace ? 0.95f : 0.8f,
            CenteringDelay = ace ? 0.55f : 0.05f,
            SpeedMotion = ace, PitchFollow = preset == CameraPreset.AceCombat8,
            Instruments = preset == CameraPreset.NuclearOptionPlus ? HudAttachment.Aircraft : HudAttachment.Camera,
            Distance = ace ? 0.8f : 1f
        };
    }
}
