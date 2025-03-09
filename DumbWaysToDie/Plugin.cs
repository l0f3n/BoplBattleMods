using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace DumbWaysToDie;

public enum CauseOfDeath
{
    Other,
    Arrowed,
    BlackHole,
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
    Meteored,
    Rocked,
    Rocking,
    Rolled,
    Space,
    Sworded,
}

[BepInPlugin("lofen.dumbWaysToDie", "Dumb Ways to Die", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    internal static ConfigEntry<bool> Debug;
    internal static ConfigEntry<float> ScaleFactor;

    public static Dictionary<CauseOfDeath, DeathSpriteCreator> Assets = new Dictionary<CauseOfDeath, DeathSpriteCreator>();
    public static string AssetsPath;

    private void Awake()
    {
        Logger = base.Logger;

        Debug = Config.Bind("General", "Debug mode", false, "Always reload textures from files.");
        ScaleFactor = Config.Bind("General", "Sprite Scale Factor", 1.1f, "Factor to scale custom sprites by");

        string basePath = Path.GetDirectoryName(((BaseUnityPlugin)this).Info.Location);
        string assetsPath = Path.Combine(basePath, "Assets");

        if (!Directory.Exists(assetsPath))
        {
            Logger.LogError("Assets folder not found!");
        }

        AssetsPath = assetsPath;

        var watch = System.Diagnostics.Stopwatch.StartNew();

        AddDeathSpriteCreator(CauseOfDeath.Arrowed, BlendMode.Killer);
        AddDeathSpriteCreator(CauseOfDeath.BlackHole);
        AddDeathSpriteCreator(CauseOfDeath.Buddha);
        AddDeathSpriteCreator(CauseOfDeath.Clouds);
        AddDeathSpriteCreator(CauseOfDeath.Drilled);
        AddDeathSpriteCreator(CauseOfDeath.Drilling);
        AddDeathSpriteCreator(CauseOfDeath.Drowned);
        AddDeathSpriteCreator(CauseOfDeath.Electrocuted);
        AddDeathSpriteCreator(CauseOfDeath.Exploded);
        AddDeathSpriteCreator(CauseOfDeath.Froze);
        AddDeathSpriteCreator(CauseOfDeath.Invisible);
        AddDeathSpriteCreator(CauseOfDeath.Leashed);
        AddDeathSpriteCreator(CauseOfDeath.Macho);
        AddDeathSpriteCreator(CauseOfDeath.Meditating);
        AddDeathSpriteCreator(CauseOfDeath.Meteored, BlendMode.Blend);
        AddDeathSpriteCreator(CauseOfDeath.Rocked, BlendMode.Victim);
        AddDeathSpriteCreator(CauseOfDeath.Rocking);
        AddDeathSpriteCreator(CauseOfDeath.Rolled, BlendMode.Blend);
        AddDeathSpriteCreator(CauseOfDeath.Sworded);
        AddDeathSpriteCreator(CauseOfDeath.Space);

        watch.Stop();

        Logger.LogInfo($"Plugin Dumb Ways to Die was loaded in {watch.ElapsedMilliseconds} ms!");

        Harmony harmony = new("lofen.dumbWaysToDie");
        harmony.PatchAll(typeof(Patch));
    }

    static private void AddDeathSpriteCreator(CauseOfDeath causeOfDeath, BlendMode blendMode = BlendMode.Blend)
    {
        Assets.Add(causeOfDeath, new DeathSpriteCreator(causeOfDeath.ToString(), blendMode));
    }
}

public enum BlendMode
{
    Killer,
    Victim,
    Blend,
}

public class DeathSpriteCreator
{
    private string name;
    private BlendMode blendMode;

    private Color[] vpxs;
    private Color[] kpxs;

    private int width;
    private int height;

    private static float MAX_BRIGHTNESS = Color.green.grayscale;

    public DeathSpriteCreator(string name, BlendMode blendMode)
    {
        this.name = name;
        this.blendMode = blendMode;
        LoadAllTextures();
    }

    private void LoadAllTextures()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        vpxs = LoadPixels($"{name}");
        kpxs = LoadPixels($"{name}-Killer");

        watch.Stop();
        Plugin.Logger.LogDebug($"Loaded textures for {name} in {watch.ElapsedMilliseconds} ms");
    }

    private Color[] LoadPixels(string name)
    {
        Texture2D tex = LoadTexture(name);
        return tex?.GetPixels() ?? new Color[width * height];
    }

    private Texture2D LoadTexture(string name)
    {
        string path = Path.Combine(Plugin.AssetsPath, $"{name}.png");

        if (!File.Exists(path))
            return null;

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);
        ImageConversion.LoadImage(tex, bytes);

        if (width == 0 || height == 0)
        {
            width = tex.width;
            height = tex.height;
        }
        else if (tex.width != width || tex.height != height)
        {
            Plugin.Logger.LogWarning($"Texture {path} has wrong dimensions {tex.width}x{tex.height}, expected {width}x{height}");
        }

        // Doing all of this is probably useless, but im too lazy to delete it
        Texture2D newTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, true);
        newTex.name = name;
        newTex.filterMode = FilterMode.Bilinear;
        newTex.SetPixels(tex.GetPixels());

        return newTex;
    }

    private static bool IsGreen(Color c)
    {
        return c.g > c.r && c.g > c.b;
    }

    public Sprite Create(Color victimColor, Color? maybeKillerColor)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
        tex.name = name;
        tex.filterMode = FilterMode.Bilinear;

        if (Plugin.Debug.Value)
        {
            LoadAllTextures();
        }

        Color killerColor = maybeKillerColor ?? new Color(1, 1, 1, 1);

        Color[] pixels = new Color[width * height];

        Parallel.For(0, pixels.Length, i =>
        {
            if (vpxs[i].a == 0 && kpxs[i].a == 0)
                return;

            Color v = IsGreen(vpxs[i]) ? Color.Lerp(Color.clear, victimColor, vpxs[i].grayscale / MAX_BRIGHTNESS) : vpxs[i];
            Color k = IsGreen(kpxs[i]) ? Color.Lerp(Color.clear, killerColor, kpxs[i].grayscale / MAX_BRIGHTNESS) : kpxs[i];

            if (blendMode == BlendMode.Victim && v.a != 0)
            {
                pixels[i] = v;
            }
            else if (blendMode == BlendMode.Killer && k.a != 0)
            {
                pixels[i] = k;
            }
            else
            {
                pixels[i] = Color.Lerp(v, k, (k.a / (v.a + k.a)));
            }
        });

        tex.SetPixels(pixels);
        tex.Apply();

        // FullRect for performance: https://discussions.unity.com/t/any-way-to-speed-up-sprite-create/700273/12
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 60, 0, SpriteMeshType.FullRect);

        Plugin.Logger.LogDebug($"Created sprite {name} in {watch.ElapsedMilliseconds} ms");

        return sprite;
    }
}

[HarmonyPatch]
public class Patch
{
    public static Dictionary<int, CauseOfDeath> CausesOfDeath = new Dictionary<int, CauseOfDeath>();
    public static Dictionary<int, AbilitySelectCircle> AbilitySelectCircles = new Dictionary<int, AbilitySelectCircle>();
    public static Dictionary<int, int> Killers = new Dictionary<int, int>();

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
            Plugin.Logger.LogError($"Not setting alternate sprite, no death sprite creator for cause of death {causeOfDeath}");
            return;
        }

        Plugin.Logger.LogInfo($"Setting alternate sprite {causeOfDeath} for player {id}");

        Color victimColor = PlayerHandler.Get().GetPlayer(id).Color.GetColor("_ShadowColor");

        // We rely on the fact that player ids start from 1
        Killers.TryGetValue(id, out int killerId);
        Color? killerColor = PlayerHandler.Get().GetPlayer(killerId)?.Color.GetColor("_ShadowColor");

        Sprite sprite = Plugin.Assets[causeOfDeath].Create(victimColor, killerColor);
        AbilitySelectCircle abc = AbilitySelectCircles[id];

        abc.SetCharacterSprite(sprite);

        // We do the coloring manually, disable default material
        Image[] characterImages = (Image[])Traverse.Create(abc.loser).Field("characterImages").GetValue();
        characterImages[2].material = null;
        characterImages[2].rectTransform.localScale = new Vector3(Plugin.ScaleFactor.Value, Plugin.ScaleFactor.Value, 1);

        Image character = (Image)Traverse.Create(abc.winner).Field("character").GetValue();
        character.material = null;

        // Probably not necessary, but just to be safe
        CausesOfDeath.Remove(id);
        AbilitySelectCircles.Remove(id);
        Killers.Remove(id);
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
            Plugin.Logger.LogDebug($"Not setting cause of death {causeOfDeath} for player {id}, not actually dead yet");
            return;
        }

        if (causeOfDeath == CauseOfDeath.Other)
        {
            Plugin.Logger.LogDebug($"Not setting cause of death {causeOfDeath} for player {id}, no specific cause of death set");
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
        Killers.Clear();
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
                // Lightning between tesla coils
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
            overrideOriginal = true; // The sprite shows you that you shot yourself
            Killers[id] = go.GetComponent<Projectile>().GetComponent<IPlayerIdHolder>().GetPlayerId();
        }
        else if (go.layer == LayerMask.NameToLayer("LethalTerrain"))
        {
            if (t.CompareTag("explosion"))
            {
                causeOfDeath = CauseOfDeath.Sworded;
            }
            else if (t.CompareTag("ChainLightning"))
            {
                // Actual tesla coil
                causeOfDeath = CauseOfDeath.Electrocuted;
            }
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
            else if (ability.ToString().Contains("Meteor"))
            {
                causeOfDeath = CauseOfDeath.Meteored;
            }

            Killers[id] = ability.GetComponent<IPlayerIdHolder>().GetPlayerId();
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
                causeOfDeath = CauseOfDeath.Space;
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

        int id = collision.colliderPP.fixTrans.gameObject.GetComponent<IPlayerIdHolder>().GetPlayerId();

        SetCauseOfDeath(id, CauseOfDeath.BlackHole);
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

