using BepInEx;
using BepInEx.Logging;
using BoplFixedMath;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace FlashingGrenades;

[BepInPlugin("lofen.flashinggrenades", "Flashing Grenades", MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
        
    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin Flashing Grenades is loaded!");

        Harmony harmony = new("lofen.flashinggrenades");
        harmony.PatchAll(typeof(Patch));
    }
}

[HarmonyPatch]
public class Patch
{
    // This is a memory leak since we never get rid of any entries in these
    // dictionaries. This might cause the program to lag after a long play
    // session. Hopes it not a problem though.
    private static readonly Dictionary<Grenade, Fix> elapsedTimes = new();
    private static readonly Dictionary<Grenade, Fix> switchTimes = new();

    private static readonly Color OnColor = Color.gray;
    private static readonly Color OffColor = Color.white;

    public static Fix GetElapsedTime(Grenade instance)
    {
        return elapsedTimes.TryGetValue(instance, out Fix animationTime) ? animationTime : Fix.Zero;
    }

    public static void SetElapsedTime(Grenade instance, Fix elapsedTime)
    {
        elapsedTimes[instance] = elapsedTime;
    }

    public static Fix GetSwitchTime(Grenade instance)
    {
        return switchTimes.TryGetValue(instance, out Fix switchTime) ? switchTime : Fix.Zero;
    }

    public static void SetSwitchTime(Grenade instance, Fix switchTime)
    {
        switchTimes[instance] = switchTime;
    }

    [HarmonyPatch(typeof(Grenade), nameof(Grenade.Init))]
    [HarmonyPostfix]
    public static void GrenadePostInit(ref Grenade __instance)
    {
        SpriteRenderer sr = __instance.GetComponent<SpriteRenderer>();
        sr.color = OffColor;
        SetSwitchTime(__instance, __instance.detonationTime - (Fix)2.0);
    }

    [HarmonyPatch(typeof(Grenade), "UpdateSim")]
    [HarmonyPostfix]
    public static void GrenadePostUpdateSim(Fix simDeltaTime, ref Grenade __instance, ref Fix ___detonationTime)
    {
      SetElapsedTime(__instance, GetElapsedTime(__instance) + simDeltaTime * GameTime.PlayerTimeScale);
      Fix elapsedTime = GetElapsedTime(__instance);

      if (elapsedTime >= GetSwitchTime(__instance))
      {
        SpriteRenderer sr = __instance.GetComponent<SpriteRenderer>();
        if (sr.color == OnColor)
          sr.color = OffColor;
        else
          sr.color = OnColor;

        // Im not sure the update method is even called this often, but whatever
        // I would have loved to access the builtin Update() method on
        // MonoBehaviour objects, but I cant for some reason. UpdateSim() is
        // good enough.
        Fix timeLeft = __instance.detonationTime - elapsedTime;
        if (timeLeft <= (Fix)0.25)
          SetSwitchTime(__instance, elapsedTime + (Fix)0.025);
        else if (timeLeft <= (Fix)0.5)
          SetSwitchTime(__instance, elapsedTime + (Fix)0.05);
        else if (timeLeft <= (Fix)1.0)
          SetSwitchTime(__instance, elapsedTime + (Fix)0.1);
        else if (timeLeft <= (Fix)2.0)
          SetSwitchTime(__instance, elapsedTime + (Fix)0.2);
        else
          SetSwitchTime(__instance, __instance.detonationTime - (Fix)2.0);
      }
    }
}
