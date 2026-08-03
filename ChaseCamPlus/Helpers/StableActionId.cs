namespace ChaseCamPlus.Helpers;

/// <summary>
/// Rewired persists the player's bindings keyed by numeric action id, so an id that shifts between
/// sessions silently reassigns whatever the user bound. Allocating with "highest existing id + 1"
/// — the recipe most Nuclear Option mods inherited from TargetCamControl — does exactly that: the
/// number depends on plugin load order, so installing or removing an unrelated mod can hand our id
/// to someone else's action.
///
/// Deriving the id from the action name instead makes it stable forever, independent of load order
/// and of which other mods are present. Collisions become a birthday problem over the range rather
/// than a near-certainty, and the caller falls back to sequential allocation if the slot is taken.
/// </summary>
public static class StableActionId
{
    // Deliberately close to the id space the game and other mods already use (the shared recipe
    // starts around 850) rather than somewhere exotic like 2_000_000. Rewired is expected to key
    // actions by dictionary, but staying in the same order of magnitude means it cannot matter.
    private const int RangeStart = 1000;
    private const int RangeSize = 9000;

    /// <summary>
    /// FNV-1a over the action name. Written out rather than using <c>string.GetHashCode</c>, which
    /// is not contractually stable across processes or runtimes — the whole point here is that the
    /// number is identical every launch, forever.
    /// </summary>
    public static int For(string actionName)
    {
        unchecked
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;

            uint hash = offsetBasis;
            foreach (char c in actionName)
            {
                hash ^= c;
                hash *= prime;
            }

            return RangeStart + (int)(hash % RangeSize);
        }
    }
}
