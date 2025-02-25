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

    public static Dictionary<CauseOfDeath, Sprite> Assets = new Dictionary<CauseOfDeath, Sprite>();
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

        var watch = System.Diagnostics.Stopwatch.StartNew();

        Assets.Add(CauseOfDeath.AloneInSpace, LoadSprite("AloneInSpace.png"));
        Assets.Add(CauseOfDeath.Arrowed, LoadSprite("Arrowed.png"));
        Assets.Add(CauseOfDeath.BlackHoled, LoadSprite("BlackHoled.png"));
        Assets.Add(CauseOfDeath.Buddha, LoadSprite("Buddha.png"));
        Assets.Add(CauseOfDeath.Clouds, LoadSprite("Clouds.png"));
        Assets.Add(CauseOfDeath.Drilled, LoadSprite("Drilled.png"));
        Assets.Add(CauseOfDeath.Drilling, LoadSprite("Drilling.png"));
        Assets.Add(CauseOfDeath.Drowned, LoadSprite("Drowned.png"));
        Assets.Add(CauseOfDeath.Electrocuted, LoadSprite("Electrocuted.png"));
        Assets.Add(CauseOfDeath.Exploded, LoadSprite("Exploded.png"));
        Assets.Add(CauseOfDeath.Froze, LoadSprite("Froze.png"));
        Assets.Add(CauseOfDeath.Invisible, LoadSprite("Invisible.png"));
        Assets.Add(CauseOfDeath.Leashed, LoadSprite("Leashed.png"));
        Assets.Add(CauseOfDeath.Macho, LoadSprite("Macho.png"));
        Assets.Add(CauseOfDeath.Meditating, LoadSprite("Meditating.png"));
        Assets.Add(CauseOfDeath.Sworded, LoadSprite("Sworded.png"));
        Assets.Add(CauseOfDeath.Rocked, LoadSprite("Rocked.png"));
        Assets.Add(CauseOfDeath.Rocking, LoadSprite("Rocking.png"));
        Assets.Add(CauseOfDeath.Rolled, LoadSprite("Rolled.png"));

        watch.Stop();

        Logger.LogInfo($"Plugin Dumb Ways to Die is loaded in {watch.ElapsedMilliseconds} ms!");

        Harmony harmony = new("lofen.dumbWaysToDie");
        harmony.PatchAll(typeof(Patch));
    }

    static private Sprite LoadSprite(string name)
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

        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 60);

        watch.Stop();

        Logger.LogDebug($"Loaded sprite '{name}' in {watch.ElapsedMilliseconds} ms");

        return sprite;
    }
}

public enum CauseOfDeath
{
    AloneInSpace,
    Arrowed,
    BlackHoled,
    Buddha,
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
    Sworded,
    Rocked,
    Rocking,
    Rolled,
}

[HarmonyPatch]
public class Patch
{
    public static Dictionary<int, CauseOfDeath> CausesOfDeath = new Dictionary<int, CauseOfDeath>();
    public static Dictionary<int, AbilitySelectCircle> AbilitySelectCircles = new Dictionary<int, AbilitySelectCircle>();
    public static Dictionary<int, bool> IsRocking = new Dictionary<int, bool>();
    public static Dictionary<int, bool> IsDrilling = new Dictionary<int, bool>();
    public static Dictionary<int, bool> IsMeditating = new Dictionary<int, bool>();

    static private void TrySetAlternateSprite(int id)
    {
        if (!CausesOfDeath.ContainsKey(id))
        {
            Plugin.Logger.LogDebug($"Not setting alternate sprite, no cause of death for player {id} yet");
            return;
        }

        if (!AbilitySelectCircles.ContainsKey(id))
        {
            Plugin.Logger.LogDebug($"Not setting alternate sprite, no ability select for player {id} yet");
            return;
        }

        CauseOfDeath causeOfDeath = CausesOfDeath[id];

        if (!Plugin.Assets.ContainsKey(causeOfDeath))
        {
            if (causeOfDeath != CauseOfDeath.Other)
                Plugin.Logger.LogWarning($"No asset found for: {causeOfDeath}");

            return;
        }

        Plugin.Logger.LogInfo($"Setting alternate sprite {causeOfDeath} for player {id}");

        AbilitySelectCircles[id].SetCharacterSprite(Plugin.Assets[causeOfDeath]);

        CausesOfDeath.Remove(id);
        AbilitySelectCircles.Remove(id);
    }

    static private void SetCauseOfDeath(int id, CauseOfDeath causeOfDeath, bool overrideOriginal = false)
    {
        if (GameSessionHandler.HasGameEnded())
        {
            // Bopls are sometimes randomly killed after the games ends
            Plugin.Logger.LogDebug($"Not setting cause of death {causeOfDeath} for player {id}, game is over");
            return;
        }

        Player p = PlayerHandler.Get().GetPlayer(id);

        if (p.CauseOfDeath != global::CauseOfDeath.Other && !overrideOriginal)
        {
            Plugin.Logger.LogDebug($"Not setting cause of death {causeOfDeath} for player {id}, already has standard cause of death {p.CauseOfDeath}");
            return;
        }

        if (p.IsAlive)
        {
            // This someimes triggers if we have clones or revives
            Plugin.Logger.LogDebug($"Not settings cause of death {causeOfDeath} for player {id}, not actually dead yet");
            return;
        }

        Plugin.Logger.LogDebug($"Setting cause of death {causeOfDeath} for player {id}");

        CausesOfDeath[id] = causeOfDeath;

        TrySetAlternateSprite(id);
    }

    static private CauseOfDeath DetermineStateBeforeDeath(int id, bool isLeashed)
    {
        CauseOfDeath causeOfDeath = CauseOfDeath.Other;

        Player player = PlayerHandler.Get().GetPlayer(id);
        IsRocking.TryGetValue(player.Id, out bool isRocking);
        IsMeditating.TryGetValue(player.Id, out bool isMeditating);
        IsDrilling.TryGetValue(player.Id, out bool isDrilling);

        if (GameTime.IsTimeStopped() && player.isProtectedFromTimeStop)
        {
            causeOfDeath = CauseOfDeath.Buddha;
        }
        else if (player.isInvisible)
        {
            causeOfDeath = CauseOfDeath.Invisible;
        }
        else if (isLeashed)
        {
            causeOfDeath = CauseOfDeath.Leashed;
        }
        else if (player.InMachoThrow)
        {
            causeOfDeath = CauseOfDeath.Macho;
        }
        else if (isRocking)
        {
            causeOfDeath = CauseOfDeath.Rocking;
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
        IsRocking.Clear();
        IsDrilling.Clear();
        IsMeditating.Clear();
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
        bool isLeashed = ___body.ropeBody != null;
        __state = DetermineStateBeforeDeath(___playerIdHolder.GetPlayerId(), isLeashed);
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
                if (go.ToString().Contains("invisibleHitbox"))
                {
                    causeOfDeath = CauseOfDeath.Drilled;
                }
            }
            else
            {
                causeOfDeath = CauseOfDeath.Exploded;
            }
        }
        else if (go.layer == LayerMask.NameToLayer("Projectile") && t.CompareTag("projectile"))
        {
            causeOfDeath = CauseOfDeath.Arrowed;
        }
        else if (go.layer == LayerMask.NameToLayer("LethalTerrain") && t.CompareTag("explosion"))
        {
            causeOfDeath = CauseOfDeath.Sworded;
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
        __state = CauseOfDeath.Other;
        bool isLeashed = false;

        if (__instance.gameObject.layer != LayerMask.NameToLayer("Player"))
            return;

        PlayerCollision pc = __instance.GetComponent<PlayerCollision>();
        if (pc != null)
        {
            PlayerBody body = (PlayerBody)Traverse.Create(pc).Field("body").GetValue();
            isLeashed = body.ropeBody != null;
        }

        BounceBall bb = __instance.GetComponent<BounceBall>();
        if (bb != null)
        {
            isLeashed = bb.ropeBody != null;
        }

        IPlayerIdHolder c = __instance.GetComponent<IPlayerIdHolder>();
        if (c == null)
            return;

        int id = c.GetPlayerId();

        __state = DetermineStateBeforeDeath(id, isLeashed);
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

        CauseOfDeath causeOfDeath = CauseOfDeath.Other;

        if (__state != CauseOfDeath.Other)
        {
            causeOfDeath = __state;
        }
        else if (Constants.leveltype != LevelType.space && ___fixTrans.position.y < SceneBounds.WaterHeight)
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

        SetCauseOfDeath(idh.GetPlayerId(), CauseOfDeath.BlackHoled);
    }

    [HarmonyPatch(typeof(BounceBall), nameof(BounceBall.OnEnterAbility))]
    [HarmonyPostfix]
    public static void BounceBallOnEnterPost(ref PlayerInfo ___playerInfo)
    {
        IsRocking[___playerInfo.playerId] = true;
    }

    [HarmonyPatch(typeof(BounceBall), nameof(BounceBall.ExitAbility), new Type[] { typeof(AbilityExitInfo) })]
    [HarmonyPostfix]
    public static void BounceBallExitPost(ref PlayerInfo ___playerInfo)
    {
        IsRocking[___playerInfo.playerId] = false;
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

