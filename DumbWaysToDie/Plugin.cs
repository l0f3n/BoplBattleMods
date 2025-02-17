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

    private void Start()
    {
        string basePath = Path.GetDirectoryName(((BaseUnityPlugin)this).Info.Location);
        string assetsPath = Path.Combine(basePath, "Assets");
        if (!Directory.Exists(assetsPath))
        {
            Logger.LogError("Assets folder not found");
            return;
        }

        Assets.Add(CauseOfDeath.Drowned, LoadSprite(Path.Combine(assetsPath, "Drowned.png")));
        Assets.Add(CauseOfDeath.Electrocuted, LoadSprite(Path.Combine(assetsPath, "Electrocuted.png")));
        Assets.Add(CauseOfDeath.Exploded, LoadSprite(Path.Combine(assetsPath, "Exploded.png")));
        Assets.Add(CauseOfDeath.Froze, LoadSprite(Path.Combine(assetsPath, "Froze.png")));
        Assets.Add(CauseOfDeath.Outside, LoadSprite(Path.Combine(assetsPath, "Outside.png")));
        Assets.Add(CauseOfDeath.PiercedByArrow, LoadSprite(Path.Combine(assetsPath, "PiercedByArrow.png")));
        Assets.Add(CauseOfDeath.PiercedBySword, LoadSprite(Path.Combine(assetsPath, "PiercedBySword.png")));
    }

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin Dumb Ways to Die is loaded!");

        Harmony harmony = new("lofen.dumbWaysToDie");
        harmony.PatchAll(typeof(Patch));
    }

    static private Texture2D LoadTexture(string path)
    {
        byte[] array = File.ReadAllBytes(path);
        Texture2D val = new Texture2D(2, 2);
        ImageConversion.LoadImage(val, array);
        return val;
    }

    static private Sprite LoadSprite(string path)
    {
        Texture2D texture = LoadTexture(path);
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
}

public enum CauseOfDeath
{
    // Aged
    // Blackholed
    Drowned,
    Electrocuted,
    Exploded,
    Froze,
    Other,
    Outside,
    // Penetrated,
    // Pet,
    PiercedByArrow,
    PiercedBySword,
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

        // TODO: Is color handled correctly here? Probably, but i will keep this todo here just in case
        Controllers[id].SetCharacterSprite(Plugin.Assets[causeOfDeath]);
    }

    [HarmonyPatch(typeof(AbilitySelectController), "SetPlayer")]
    [HarmonyPostfix]
    public static void SetPlayerPost(AbilitySelectController __instance, int id)
    {
        Controllers[id] = __instance;
    }

    [HarmonyPatch(typeof(PlayerCollision), "KillPlayerOnCollision")]
    [HarmonyPostfix]
    public static void KillPlayerPost(bool __result, IPlayerIdHolder ___playerIdHolder, ref CollisionInformation collision)
    {
        // Player did not actually die
        if (!__result)
            return;

        int id = ___playerIdHolder.GetPlayerId();
        FixTransform t = collision.colliderPP.fixTrans;
        GameObject go = t.gameObject;

        /*FileLog.Log($"Player {id} died:");*/
        /*FileLog.Log($"  Layer: {LayerMask.LayerToName(go.layer)}");*/
        /*FileLog.Log($"  Tag:   {t.tag}");*/

        CauseOfDeath causeOfDeath = CauseOfDeath.Other;

        if (go.layer == LayerMask.NameToLayer("Explosion"))
        {
            if (t.CompareTag("ChainLightning"))
            {
                causeOfDeath = CauseOfDeath.Electrocuted;
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

        SetAlternativeSprite(id, causeOfDeath);
    }

    [HarmonyPatch(typeof(DestroyIfOutsideSceneBounds), "selfDestruct")]
    [HarmonyPostfix]
    public static void PostSelfDestruct(DestroyIfOutsideSceneBounds __instance, FixTransform ___fixTrans)
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
        else
        {
            causeOfDeath = CauseOfDeath.Outside;
        }

        SetAlternativeSprite(id, causeOfDeath, overrideOriginal: true);
    }

    // TODO: Pet
    // TODO: Timestop
    // TODO: Penetrated
    // TODO: Blackhole 
}

