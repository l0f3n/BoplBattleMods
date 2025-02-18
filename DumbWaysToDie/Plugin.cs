using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
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

    private void Start()
    {
        string basePath = Path.GetDirectoryName(((BaseUnityPlugin)this).Info.Location);
        string assetsPath = Path.Combine(basePath, "Assets");
        if (!Directory.Exists(assetsPath))
        {
            Logger.LogError("Assets folder not found");
            return;
        }

        AssetsPath = assetsPath;

        Assets.Add(CauseOfDeath.Drilled, LoadSprite("Drilled.png"));
        Assets.Add(CauseOfDeath.Drowned, LoadSprite("Drowned.png"));
        Assets.Add(CauseOfDeath.Electrocuted, LoadSprite("Electrocuted.png"));
        Assets.Add(CauseOfDeath.Exploded, LoadSprite("Exploded.png"));
        Assets.Add(CauseOfDeath.Froze, LoadSprite("Froze.png"));
        Assets.Add(CauseOfDeath.Leashed, LoadSprite("Leashed.png"));
        Assets.Add(CauseOfDeath.Outside, LoadSprite("Outside.png"));
        Assets.Add(CauseOfDeath.PiercedByArrow, LoadSprite("PiercedByArrow.png"));
        Assets.Add(CauseOfDeath.PiercedBySword, LoadSprite("PiercedBySword.png"));
        Assets.Add(CauseOfDeath.Rocked, LoadSprite("Rocked.png"));
        Assets.Add(CauseOfDeath.Rolled, LoadSprite("Rolled.png"));
    }

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin Dumb Ways to Die is loaded!");

        Harmony harmony = new("lofen.dumbWaysToDie");
        harmony.PatchAll(typeof(Patch));
    }

    static private Texture2D LoadTexture(string name)
    {
        // TODO: Should we load the assets from SVGs instead?
        string path = Path.Combine(AssetsPath, name);
        byte[] array = File.ReadAllBytes(path);
        Texture2D val = new Texture2D(2, 2);
        ImageConversion.LoadImage(val, array);
        return val;
    }

    static private Sprite LoadSprite(string name)
    {
        Texture2D texture = LoadTexture(name);
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
}

public enum CauseOfDeath
{
    // Aged
    // Blackholed
    Drilled,
    Drowned,
    Electrocuted,
    Exploded,
    Froze,
    Leashed,
    Other,
    Outside,
    // Penetrated,
    PiercedByArrow,
    PiercedBySword,
    Rocked,
    Rolled,
}

[HarmonyPatch]
public class Patch
{
    public static Dictionary<int, AbilitySelectController> Controllers = new Dictionary<int, AbilitySelectController>();

    static private void SetAlternativeSprite(int id, CauseOfDeath causeOfDeath, bool overrideOriginal = false)
    {
        /*FileLog.Log($"Cause of death: {causeOfDeath.ToString()}");*/

        // Player has a standard cause of death, ignore our special ones
        Player p = PlayerHandler.Get().GetPlayer(id);
        if (p.CauseOfDeath != global::CauseOfDeath.Other && !overrideOriginal)
            return;

        // We use Other as a fallback, so ignore it in that case
        if (!Plugin.Assets.ContainsKey(causeOfDeath))
            return;

        // This should never happen, but we rather ignore this case than crash
        if (!Controllers.ContainsKey(id))
            return;

        Controllers[id].SetCharacterSprite(Plugin.Assets[causeOfDeath]);
    }

    [HarmonyPatch(typeof(AbilitySelectController), "SetPlayer")]
    [HarmonyPostfix]
    public static void SetPlayerPost(AbilitySelectController __instance, int id)
    {
        Controllers[id] = __instance;
    }

    [HarmonyPatch(typeof(PlayerCollision), "KillPlayerOnCollision")]
    [HarmonyPrefix]
    public static void KillPlayerPre(ref CauseOfDeath __state, PlayerBody ___body)
    {
        __state = CauseOfDeath.Other;

        if (___body.ropeBody != null)
        {
            __state = CauseOfDeath.Leashed;
        }
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

        SetAlternativeSprite(id, causeOfDeath, overrideOriginal);
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

        __state = CauseOfDeath.Other;
        if (body.ropeBody != null)
        {
            __state = CauseOfDeath.Leashed;
        }
    }

    [HarmonyPatch(typeof(DestroyIfOutsideSceneBounds), "selfDestruct")]
    [HarmonyPostfix]
    public static void selfDestructPost(DestroyIfOutsideSceneBounds __instance, CauseOfDeath __state, FixTransform ___fixTrans)
    {
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
            // TODO: Different depending on if we are in space or not
            causeOfDeath = CauseOfDeath.Outside;
        }

        SetAlternativeSprite(id, causeOfDeath, overrideOriginal: true);
    }

    // TODO: Timestop
    // TODO: Penetrated
    // TODO: Blackhole 
}

