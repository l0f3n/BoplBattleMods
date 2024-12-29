using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BoplFixedMath;
using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Streaks;

[BepInPlugin("lofen.streaks", "Streaks", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    internal static ConfigEntry<int> MinStreakThresh;
    internal static ConfigEntry<int> MaxStreakThresh;
    internal static ConfigEntry<int> MaxWinThresh;

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin Streaks is loaded!");

        MinStreakThresh = Config.Bind("General", "Minimum streak threshold", 3, "Only show streaks at least this long.");
        MaxStreakThresh = Config.Bind("General", "Maximum streak threshold", 100, "Only show streaks less than this long.");
        MaxWinThresh = Config.Bind("General", "Maximum win threshold", 100, "Only show streaks when player has less than this amount of wins.");

        Harmony harmony = new("lofen.streaks");
        harmony.PatchAll(typeof(Patch));
    }
}

[HarmonyPatch]
public class Patch
{
    public static Dictionary<int, int> WinStreaks = new Dictionary<int, int>();
    public static Dictionary<int, int> LoseStreaks = new Dictionary<int, int>();

    [HarmonyPatch(typeof(Player), nameof(Player.WinAGame))]
    [HarmonyPostfix]
    public static void WinAGame(ref Player __instance)
    {
        LoseStreaks.Remove(__instance.Id);

        if (WinStreaks.ContainsKey(__instance.Id))
            WinStreaks[__instance.Id] = WinStreaks[__instance.Id] + 1;
        else
            WinStreaks.Add(__instance.Id, 1);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.Kill))]
    [HarmonyPostfix]
    public static void Kill(ref Player __instance)
    {
        if (__instance.stillAliveThisRound)
            return;

        WinStreaks.Remove(__instance.Id);

        if (LoseStreaks.ContainsKey(__instance.Id))
            LoseStreaks[__instance.Id] = LoseStreaks[__instance.Id] + 1;
        else
            LoseStreaks.Add(__instance.Id, 1);
    }

    [HarmonyPatch(typeof(AbilitySelectController), "Init")]
    [HarmonyPostfix]
    public static void LoserPostInit(ref TextMeshProUGUI ___ScoreText)
    {
      LoseStreaks.Clear();
      ___ScoreText.outlineWidth = 0.25f;
      ___ScoreText.outlineColor = new Color32(0, 0, 0, 255);
    }

    [HarmonyPatch(typeof(AbilitySelectController), "Update")]
    [HarmonyPrefix]
    public static bool LoserPreUpdate(ref TextMeshProUGUI ___ScoreText, ref Player ___player)
    {
        // Avoid warnings when regular update tries to convert this text to int
        if (___ScoreText.text != $"{___player.GamesWon}")
            ___ScoreText.text = $"{___player.GamesWon}";

        return true;
    }

    [HarmonyPatch(typeof(AbilitySelectController), "Update")]
    [HarmonyPostfix]
    public static void LoserPostUpdate(ref TextMeshProUGUI ___ScoreText, ref Player ___player)
    {
        string target = $"{___player.GamesWon}";
        if (GetStreak(___player, LoseStreaks, out int streak))
            target += $"<color=red><sup>{streak}</sup></color>";

        if (___ScoreText.text != target)
            ___ScoreText.text = target;
    }

    [HarmonyPatch(typeof(AbilitySelectWinner), "Init")]
    [HarmonyPostfix]
    public static void WinnerPostInit(ref TextMeshProUGUI ___ScoreText)
    {
      WinStreaks.Clear();
      ___ScoreText.outlineWidth = 0.25f;
      ___ScoreText.outlineColor = new Color32(0, 0, 0, 255);
    }

    [HarmonyPatch(typeof(AbilitySelectWinner), "Update")]
    [HarmonyPrefix]
    public static bool WinnerPreUpdate(ref TextMeshProUGUI ___ScoreText, ref Player ___player)
    {
        string target = $"{___player.GamesWon}";
        if (GetStreak(___player, WinStreaks, out int streak))
            target += $"<color=green><sup>{streak}</sup></color>";

        if (___ScoreText.text != target)
            ___ScoreText.text = target;

        return false;
    }

    public static bool GetStreak(Player p, Dictionary<int, int> streaks, out int streak)
    {
        streak = 0;
        if (streaks.ContainsKey(p.Id))
            streak = streaks[p.Id];

        // Returns whether we should display this streak
        return p.GamesWon < Plugin.MaxWinThresh.Value
            && Plugin.MinStreakThresh.Value <= streak
            && streak < Plugin.MaxStreakThresh.Value;
    }
}

