using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DumbWaysToDie;

[BepInPlugin("lofen.dumbWaysToDie", "Dumb Ways to Die", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    public static Dictionary<CauseOfDeath, Texture2D> Assets = new Dictionary<CauseOfDeath, Texture2D>();
    public static string AssetsPath;

    private void Awake()
    {
        Logger = base.Logger;

        string basePath = Path.GetDirectoryName(((BaseUnityPlugin)this).Info.Location);
        string assetsPath = Path.Combine(basePath, "Assets");

        if (!Directory.Exists(assetsPath))
        {
            Logger.LogError("Assets folder not found!");
        }

        AssetsPath = assetsPath;

        Assets.Add(CauseOfDeath.Age, LoadTexture("Age.png"));
        Assets.Add(CauseOfDeath.AloneInSpace, LoadTexture("AloneInSpace.png"));
        Assets.Add(CauseOfDeath.BlackHole, LoadTexture("BlackHole.jpg"));
        Assets.Add(CauseOfDeath.Clouds, LoadTexture("Clouds.png"));
        Assets.Add(CauseOfDeath.Drilled, LoadTexture("Drilled.png"));
        Assets.Add(CauseOfDeath.Drilling, LoadTexture("Drilling.png"));
        Assets.Add(CauseOfDeath.Drowned, LoadTexture("Drowned.png"));
        Assets.Add(CauseOfDeath.Electrocuted, LoadTexture("Electrocuted.png"));
        Assets.Add(CauseOfDeath.Exploded, LoadTexture("Exploded.png"));
        Assets.Add(CauseOfDeath.Froze, LoadTexture("Froze.png"));
        Assets.Add(CauseOfDeath.Invisible, LoadTexture("Invisible.png"));
        Assets.Add(CauseOfDeath.Leashed, LoadTexture("Leashed.png"));
        Assets.Add(CauseOfDeath.Macho, LoadTexture("Macho.png"));
        Assets.Add(CauseOfDeath.Meditating, LoadTexture("Meditating.png"));
        Assets.Add(CauseOfDeath.PiercedByArrow, LoadTexture("PiercedByArrow.png"));
        Assets.Add(CauseOfDeath.PiercedBySword, LoadTexture("PiercedBySword.png"));
        Assets.Add(CauseOfDeath.Rocked, LoadTexture("Rocked.png"));
        Assets.Add(CauseOfDeath.Rolled, LoadTexture("Rolled.png"));

        Logger.LogInfo($"Plugin Dumb Ways to Die is loaded!");

        Harmony harmony = new("lofen.dumbWaysToDie");
        harmony.PatchAll(typeof(Patch));
    }

    static private Texture2D LoadTexture(string name)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        string path = Path.Combine(AssetsPath, name);
        byte[] bytes = File.ReadAllBytes(path);

        Texture2D tex = new Texture2D(2, 2);
        ImageConversion.LoadImage(tex, bytes);

        // Doing all of this is probably useless, but im too lazy to delete it
        Texture2D newTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, true);
        newTex.name = name;
        newTex.filterMode = FilterMode.Bilinear;
        newTex.SetPixels(tex.GetPixels());
        newTex.Apply();

        watch.Stop();

        Logger.LogInfo($"Loaded texture '{newTex.name}' in {watch.ElapsedMilliseconds} ms");

        return newTex;
    }
}

public enum CauseOfDeath
{
    Age,
    AloneInSpace,
    BlackHole,
    Clouds,
    Drilled,
    Drilling,
    Drowned,
    Electrocuted,
    Exploded,
    Froze,
    Invisible,
    Leashed,
    Macho,
    Meditating,
    Other,
    PiercedByArrow,
    PiercedBySword,
    Rocked,
    Rolled,
}

[HarmonyPatch]
public class Patch
{
    public static Dictionary<int, CauseOfDeath> CausesOfDeath = new Dictionary<int, CauseOfDeath>();
    public static Dictionary<int, AbilitySelectCircle> AbilitySelectCircles = new Dictionary<int, AbilitySelectCircle>();
    public static Dictionary<int, bool> IsDrilling = new Dictionary<int, bool>();
    public static Dictionary<int, bool> IsMeditating = new Dictionary<int, bool>();

    /*public static Color RawKillerColor = new Color(0, 0, 1);*/
    /**/
    /*static private Texture2D Recolor(Texture2D tex, Color oldColor, Color newColor)*/
    /*{*/
    /*    Texture2D newTex = new Texture2D(tex.width, tex.height, tex.format, true);*/
    /*    newTex.name = tex.name;*/
    /*    newTex.filterMode = tex.filterMode;*/
    /*    Color[] pixels = tex.GetPixels();*/
    /*    for (int i = 0; i < pixels.Length; i++)*/
    /*    {*/
    /*        if (pixels[i] == oldColor)*/
    /*            pixels[i] = newColor;*/
    /*    }*/
    /*    newTex.SetPixels(pixels);*/
    /*    newTex.Apply();*/
    /*    return newTex;*/
    /*}*/

    static private void TrySetAlternateSprite(int id)
    {
        if (!CausesOfDeath.ContainsKey(id))
        {
            Plugin.Logger.LogDebug($"No cause of death for player {id} yet");
            return;
        }

        if (!AbilitySelectCircles.ContainsKey(id))
        {
            Plugin.Logger.LogDebug($"No ability select for player {id} yet");
            return;
        }

        CauseOfDeath causeOfDeath = CausesOfDeath[id];

        if (!Plugin.Assets.ContainsKey(causeOfDeath))
        {
            if (causeOfDeath != CauseOfDeath.Other)
                Plugin.Logger.LogWarning($"No asset found for: {causeOfDeath}");

            return;
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();

        Texture2D tex = Plugin.Assets[causeOfDeath];
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 60);

        AbilitySelectCircles[id].SetCharacterSprite(sprite);

        CausesOfDeath.Remove(id);
        AbilitySelectCircles.Remove(id);

        watch.Stop();

        Plugin.Logger.LogInfo($"Set sprite '{tex.name}' for player {id} in {watch.ElapsedMilliseconds} ms");
    }

    static private void SetCauseOfDeath(int id, CauseOfDeath causeOfDeath, bool overrideOriginal = false)
    {
        if (GameSessionHandler.HasGameEnded())
        {
            Plugin.Logger.LogDebug($"Refusing to set cause of death {causeOfDeath} for player {id}, game is over");
            return;
        }

        // Player has a standard cause of death, ignore our special ones
        Player p = PlayerHandler.Get().GetPlayer(id);
        if (p.CauseOfDeath != global::CauseOfDeath.Other && !overrideOriginal)
            return;

        Plugin.Logger.LogDebug($"Setting cause of death {causeOfDeath} for player {id}");

        CausesOfDeath[id] = causeOfDeath;

        TrySetAlternateSprite(id);
    }

    static private CauseOfDeath DetermineStateBeforeDeath(int id, PlayerBody body)
    {
        CauseOfDeath causeOfDeath = CauseOfDeath.Other;

        Player player = PlayerHandler.Get().GetPlayer(id);
        IsMeditating.TryGetValue(player.Id, out bool isMeditating);
        IsDrilling.TryGetValue(player.Id, out bool isDrilling);

        if (GameTime.IsTimeStopped() && player.isProtectedFromTimeStop)
        {
            causeOfDeath = CauseOfDeath.Age;
        }
        else if (player.isInvisible)
        {
            causeOfDeath = CauseOfDeath.Invisible;
        }
        else if (body.ropeBody != null)
        {
            causeOfDeath = CauseOfDeath.Leashed;
        }
        else if (player.InMachoThrow)
        {
            causeOfDeath = CauseOfDeath.Macho;
        }
        else if (isMeditating)
        {
            causeOfDeath = CauseOfDeath.Meditating;
        }
        else if (isDrilling)
        {
            causeOfDeath = CauseOfDeath.Drilling;
        }

        return causeOfDeath;
    }

    [HarmonyPatch(typeof(GameSessionHandler), "StartSpawnPlayersRoutine")]
    [HarmonyPrefix]
    public static void GameSessionHandlerStartSpawnPlayersRoutinePre()
    {
        Plugin.Logger.LogDebug("Resetting...");
        CausesOfDeath.Clear();
        AbilitySelectCircles.Clear();
    }

    [HarmonyPatch(typeof(AbilitySelectCircle), "SetPlayer")]
    [HarmonyPostfix]
    public static void AbilitySelectCircleSetPlayerPost(AbilitySelectCircle __instance, int id, bool isWinner)
    {
        Plugin.Logger.LogDebug($"Setting ability select circle for player {id} (Winner: {isWinner})");
        AbilitySelectCircles[id] = __instance;
        TrySetAlternateSprite(id);
    }

    [HarmonyPatch(typeof(PlayerCollision), "KillPlayerOnCollision")]
    [HarmonyPrefix]
    public static void KillPlayerPre(ref CauseOfDeath __state, PlayerBody ___body, IPlayerIdHolder ___playerIdHolder)
    {
        __state = DetermineStateBeforeDeath(___playerIdHolder.GetPlayerId(), ___body);
    }

    [HarmonyPatch(typeof(PlayerCollision), "KillPlayerOnCollision")]
    [HarmonyPostfix]
    public static void KillPlayerPost(bool __result, CauseOfDeath __state, IPlayerIdHolder ___playerIdHolder, ref CollisionInformation collision)
    {
        // Player did not actually die
        if (!__result)
            return;

        int id = ___playerIdHolder.GetPlayerId();
        FixTransform t = collision.colliderPP.fixTrans;
        GameObject go = t.gameObject;

        /*FileLog.Log($"Player {id} died:");*/
        /*FileLog.Log($"  Layer: {LayerMask.LayerToName(go.layer)}");*/
        /*FileLog.Log($"  Tag: {t.tag}");*/

        CauseOfDeath causeOfDeath = CauseOfDeath.Other;
        bool overrideOriginal = false;

        if (__state != CauseOfDeath.Other)
        {
            causeOfDeath = __state;
            overrideOriginal = true;
        }
        else if (go.layer == LayerMask.NameToLayer("Explosion"))
        {
            if (t.CompareTag("ChainLightning"))
            {
                causeOfDeath = CauseOfDeath.Electrocuted;
            }
            else if (t.CompareTag("explosion"))
            {
                causeOfDeath = CauseOfDeath.Drilled;
            }
            else
            {
                causeOfDeath = CauseOfDeath.Exploded;
            }
        }
        else if (go.layer == LayerMask.NameToLayer("Projectile") && t.CompareTag("projectile"))
        {
            causeOfDeath = CauseOfDeath.PiercedByArrow;
        }
        else if (go.layer == LayerMask.NameToLayer("LethalTerrain") && t.CompareTag("explosion"))
        {
            causeOfDeath = CauseOfDeath.PiercedBySword;
        }
        else if (go.layer == LayerMask.NameToLayer("Player") && t.CompareTag("Ability"))
        {
            Ability ability = go.GetComponent<Ability>();
            if (ability.ToString().Contains("Rock"))
            {
                causeOfDeath = CauseOfDeath.Rocked;
            }
            else if (ability.ToString().Contains("Roll"))
            {
                causeOfDeath = CauseOfDeath.Rolled;
            }
        }

        SetCauseOfDeath(id, causeOfDeath, overrideOriginal);
    }

    [HarmonyPatch(typeof(DestroyIfOutsideSceneBounds), "selfDestruct")]
    [HarmonyPrefix]
    public static void SelfDestructPre(DestroyIfOutsideSceneBounds __instance, ref CauseOfDeath __state)
    {
        PlayerCollision pc = __instance.GetComponent<PlayerCollision>();
        if (pc == null)
            return;

        PlayerBody body = (PlayerBody)Traverse.Create(pc).Field("body").GetValue();
        if (body == null)
            return;

        IPlayerIdHolder c = __instance.GetComponent<IPlayerIdHolder>();
        if (c == null)
            return;

        int id = c.GetPlayerId();

        __state = DetermineStateBeforeDeath(id, body);
    }

    [HarmonyPatch(typeof(DestroyIfOutsideSceneBounds), "selfDestruct")]
    [HarmonyPostfix]
    public static void selfDestructPost(DestroyIfOutsideSceneBounds __instance, CauseOfDeath __state, FixTransform ___fixTrans)
    {
        if (__instance.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;

        IPlayerIdHolder c = __instance.GetComponent<IPlayerIdHolder>();
        if (c == null)
            return;

        int id = c.GetPlayerId();

        /*FileLog.Log($"Player {id} outside bounds!");*/

        CauseOfDeath causeOfDeath = CauseOfDeath.Other;

        if (Constants.leveltype != LevelType.space && ___fixTrans.position.y < SceneBounds.WaterHeight)
        {
            if (Constants.leveltype == LevelType.snow)
            {
                causeOfDeath = CauseOfDeath.Froze;
            }
            else
            {
                causeOfDeath = CauseOfDeath.Drowned;
            }
        }
        else if (__state != CauseOfDeath.Other)
        {
            causeOfDeath = __state;
        }
        else
        {
            if (Constants.leveltype == LevelType.grass)
            {
                causeOfDeath = CauseOfDeath.Clouds;
            }
            else
            {
                causeOfDeath = CauseOfDeath.AloneInSpace;
            }
        }

        SetCauseOfDeath(id, causeOfDeath, overrideOriginal: true);
    }

    [HarmonyPatch(typeof(BlackHole), "OnCollide")]
    [HarmonyPostfix]
    public static void onCollidePost(BlackHole __instance, ref CollisionInformation collision)
    {
        if (collision.layer != LayerMask.NameToLayer("Player"))
            return;

        PlayerCollision pc = collision.colliderPP.fixTrans.gameObject.GetComponent<PlayerCollision>();
        if (pc == null)
            return;

        IPlayerIdHolder idh = (IPlayerIdHolder)Traverse.Create(pc).Field("playerIdHolder").GetValue();
        if (idh == null)
            return;

        SetCauseOfDeath(idh.GetPlayerId(), CauseOfDeath.BlackHole);
    }

    [HarmonyPatch(typeof(CastSpell), nameof(CastSpell.OnEnterAbility))]
    [HarmonyPostfix]
    public static void CastOnEnterPost(ref PlayerInfo ___playerInfo)
    {
        IsMeditating[___playerInfo.playerId] = true;
    }

    [HarmonyPatch(typeof(CastSpell), nameof(CastSpell.ExitAbility), new Type[] { typeof(AbilityExitInfo) })]
    [HarmonyPostfix]
    public static void CastExitPost(ref PlayerInfo ___playerInfo)
    {
        IsMeditating[___playerInfo.playerId] = false;
    }

    [HarmonyPatch(typeof(Drill), nameof(Drill.OnEnterAbility))]
    [HarmonyPostfix]
    public static void DrillOnEnterPost(ref PlayerInfo ___playerInfo)
    {
        IsDrilling[___playerInfo.playerId] = true;
    }

    [HarmonyPatch(typeof(Drill), nameof(Drill.ExitAbility), new Type[] { typeof(AbilityExitInfo) })]
    [HarmonyPostfix]
    public static void DrillExitPost(ref PlayerInfo ___playerInfo)
    {
        IsDrilling[___playerInfo.playerId] = false;
    }

}

