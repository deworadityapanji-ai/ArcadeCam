using System;
using System.Collections;
using System.Collections.Generic;
using ChaseCamPlus.Helpers;
using HarmonyLib;
using Rewired;

namespace ChaseCamPlus.Patches;

// Same approach as YawOnMouse / TargetCamControl: inject user-assignable InputActions into Rewired's
// user data before the input manager reads it, so the bindings show up in the game's own Flight
// Controls screen.
[HarmonyPatch(typeof(InputManager_Base), "Awake")]
static class RewiredAwakePatches
{
    private const string TargetCategory = "flight";

    private class ActionDefinition
    {
        public string Name;
        public string Descriptive;
        public Action<int> Store;
    }

    private static readonly ActionDefinition[] Actions =
    {
        new ActionDefinition
        {
            Name = Plugin.ACTION_FREELOOK,
            Descriptive = "Chase Cam Free Look",
            Store = id => Plugin.FreeLookActionId = id
        },
        new ActionDefinition
        {
            Name = Plugin.ACTION_TOGGLE,
            Descriptive = "Chase Cam Toggle",
            Store = id => Plugin.ToggleActionId = id
        },
        new ActionDefinition
        {
            Name = Plugin.ACTION_CINEMATIC,
            Descriptive = "Cinematic Chase Cam",
            Store = id => Plugin.CinematicActionId = id
        },
        new ActionDefinition
        {
            Name = Plugin.ACTION_COCKPIT_HOLD,
            Descriptive = "Cockpit View (Hold)",
            Store = id => Plugin.CockpitHoldActionId = id
        }
    };

    [HarmonyPrefix]
    static void Prefix(InputManager_Base __instance)
    {
        try
        {
            var userData = __instance.userData;
            if (userData == null) return;

            var categories = GetField<IList>(userData, "actionCategories");
            var actions = GetField<IList>(userData, "actions");
            if (categories == null || actions == null) return;

            object targetCat = null;
            foreach (var category in categories)
            {
                var name = GetProp<string>(category, "name");
                if (string.Equals(name, TargetCategory, StringComparison.OrdinalIgnoreCase))
                {
                    targetCat = category;
                    break;
                }
            }

            if (targetCat == null)
            {
                Plugin.Logger.LogWarning(
                    "Flight category not found — actions will not be registered");
                return;
            }

            var usedIds = new HashSet<int>();
            var existingByName = new Dictionary<string, int>();
            int highestId = 0;

            foreach (var a in actions)
            {
                var id = GetProp<int>(a, "id");
                usedIds.Add(id);
                if (id > highestId) highestId = id;

                var name = GetProp<string>(a, "name");
                if (!string.IsNullOrEmpty(name))
                    existingByName[name] = id;
            }

            int targetCatId = GetProp<int>(targetCat, "id");
            var catMap = GetField<object>(userData, "actionCategoryMap");
            var addActionMethod = catMap != null
                ? AccessTools.Method(catMap.GetType(), "AddAction", new[] { typeof(int), typeof(int) })
                : null;

            foreach (ActionDefinition definition in Actions)
            {
                // Already registered (e.g. the input manager woke up twice); reuse its id.
                if (existingByName.TryGetValue(definition.Name, out int existingId))
                {
                    definition.Store(existingId);
                    continue;
                }

                // Stable across sessions and load orders, so the user's binding survives installing
                // or removing other mods. Sequential allocation only as a last resort.
                int id = StableActionId.For(definition.Name);
                if (usedIds.Contains(id))
                {
                    int fallback = ++highestId;
                    Plugin.Logger.LogWarning(
                        $"Preferred action id {id} for {definition.Name} is taken; falling back to "
                        + $"{fallback}. That id is order-dependent, so a binding made now may not "
                        + "survive installing or removing other mods.");
                    id = fallback;
                }
                else if (id > highestId)
                {
                    highestId = id;
                }

                var actionType = typeof(InputAction);
                var action = (InputAction)Activator.CreateInstance(actionType, true);

                SetProp(actionType, action, "id", id);
                SetProp(actionType, action, "name", definition.Name);
                SetProp(actionType, action, "type", InputActionType.Button);
                SetProp(actionType, action, "descriptiveName", definition.Descriptive);
                SetProp(actionType, action, "categoryId", targetCatId);
                SetField(actionType, action, "_userAssignable", true);

                actions.Add(action);
                usedIds.Add(id);
                addActionMethod?.Invoke(catMap, new object[] { targetCatId, id });

                definition.Store(id);
                Plugin.Logger.LogInfo($"Registered {definition.Descriptive} in Flight category (id {id})");
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Action registration: {e}");
        }
    }

    private static T GetProp<T>(object instance, string name) =>
        (T)(AccessTools.Property(instance.GetType(), name)?.GetValue(instance) ?? default(T));

    private static void SetProp<T>(Type type, object instance, string name, T value) =>
        AccessTools.Property(type, name)?.SetValue(instance, value, null);

    private static T GetField<T>(object instance, string name) =>
        (T)(AccessTools.Field(instance.GetType(), name)?.GetValue(instance) ?? default(T));

    private static void SetField<T>(Type type, object instance, string name, T value) =>
        AccessTools.Field(type, name)?.SetValue(instance, value);
}
