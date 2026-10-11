using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Timers;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Modding;
using UnityEngine;

namespace Practice_3_0
{
    // Thanks to Early for the EPP mod, it was a big help.
    internal static class BossRef
    {
        // Any Radiance mods types are internal and live in a separate dll
        // so i poke at them through reflection
        private const string AnyRadianceAssembly = "AnyRadiance";

        internal static readonly Type RadianceType = Type.GetType("AnyRadiance.Radiance.Radiance, " + AnyRadianceAssembly);
        internal static readonly Type PortalManagerType = Type.GetType("AnyRadiance.Radiance.PortalManager, " + AnyRadianceAssembly);
        internal static readonly Type SpikedPlatformType = Type.GetType("AnyRadiance.Radiance.SpikedPlatform, " + AnyRadianceAssembly);
        internal static readonly Type TrailNailType = Type.GetType("AnyRadiance.Radiance.TrailNail, " + AnyRadianceAssembly);

        private static FieldInfo _phaseField;
        private static FieldInfo _logicField;
        private static MethodInfo _startPhase1Death;
        private static MethodInfo _startPhase2Death;
        private static MethodInfo _startPhase3Death;
        private static MethodInfo _phase1Death;
        private static MethodInfo _upInstant;
        private static MethodInfo _downInstant;
        private static MethodInfo _excludeTeleport;
        private static FieldInfo _portalTimerField;
        private static PropertyInfo _anyRadianceInstance;
        private static FieldInfo _gameObjectsField;

        internal static FieldInfo PhaseField => _phaseField ?? (_phaseField = RadianceType?.GetField("_phase", BindingFlags.NonPublic | BindingFlags.Instance));
        internal static FieldInfo LogicField => _logicField ?? (_logicField = RadianceType?.GetField("_logic", BindingFlags.NonPublic | BindingFlags.Static));
        internal static MethodInfo StartPhase1Death => _startPhase1Death ?? (_startPhase1Death = RadianceType?.GetMethod("StartPhase1Death", BindingFlags.NonPublic | BindingFlags.Instance));
        internal static MethodInfo StartPhase2Death => _startPhase2Death ?? (_startPhase2Death = RadianceType?.GetMethod("StartPhase2Death", BindingFlags.NonPublic | BindingFlags.Instance));
        internal static MethodInfo StartPhase3Death => _startPhase3Death ?? (_startPhase3Death = RadianceType?.GetMethod("StartPhase3Death", BindingFlags.NonPublic | BindingFlags.Instance));
        internal static MethodInfo Phase1Death => _phase1Death ?? (_phase1Death = RadianceType?.GetMethod("Phase1Death", BindingFlags.NonPublic | BindingFlags.Instance));
        internal static MethodInfo UpInstant => _upInstant ?? (_upInstant = SpikedPlatformType?.GetMethod("UpInstant", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        internal static MethodInfo DownInstant => _downInstant ?? (_downInstant = SpikedPlatformType?.GetMethod("DownInstant", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        internal static MethodInfo ExcludeTeleport => _excludeTeleport ?? (_excludeTeleport = PortalManagerType?.GetMethod("ExcludeTeleport", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        internal static FieldInfo PortalTimerField => _portalTimerField ?? (_portalTimerField = PortalManagerType?.GetField("_randomizePortalPositionsTimer", BindingFlags.NonPublic | BindingFlags.Instance));

        internal static Coroutine GetLogic()
        {
            FieldInfo f = LogicField;
            return f != null ? (Coroutine)f.GetValue(null) : null;
        }

        internal static void SetLogic(Coroutine c)
        {
            FieldInfo f = LogicField;
            if (f != null) f.SetValue(null, c);
        }

        internal static void SetPhase(object radiance, byte value)
        {
            FieldInfo f = PhaseField;
            if (f != null) f.SetValue(radiance, value);
        }

        internal static GameObject GetGameObject(string name)
        {
            try
            {
                // AnyRadiance.Instance publishes the objects it builds at runtime as a (name -> GameObject) dict.
                // We don't reference its dll, so resolve the type by name first.
                Type anyRadiance = Type.GetType("AnyRadiance.AnyRadiance, " + AnyRadianceAssembly);
                if (anyRadiance == null) return null;

                if (_anyRadianceInstance == null)
                {
                    _anyRadianceInstance = anyRadiance.GetProperty("Instance",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (_anyRadianceInstance == null) return null;

                object instance = _anyRadianceInstance.GetValue(null, null);
                if (instance == null) return null;

                if (_gameObjectsField == null)
                {
                    _gameObjectsField = anyRadiance.GetField("GameObjects", BindingFlags.Public | BindingFlags.Instance);
                }
                if (_gameObjectsField == null) return null;

                var dict = _gameObjectsField.GetValue(instance) as Dictionary<string, GameObject>;
                if (dict == null) return null;

                GameObject go;
                return dict.TryGetValue(name, out go) ? go : null;
            }
            catch (Exception e)
            {
                PracticeMod.Instance?.LogError("GetGameObject(" + name + ") failed: " + e);
                return null;
            }
        }

        internal static GameObject GetPortalsPrefab()
        {
            return GetGameObject("Portals");
        }

        internal static byte GetPhase(Component radiance)
        {
            FieldInfo f = PhaseField;
            return f != null && f.GetValue(radiance) is byte b ? b : (byte)0;
        }
    }

    internal static class CarefreeInjection
    {
        private static FieldInfo _hitsField;

        private static FieldInfo HitsField
        {
            get
            {
                if (_hitsField == null)
                {
                    // Counts how many hits we took since the last Carefree Melody proc.
                    // 7 is the last index -> max proc chance (90.9%).
                    _hitsField = typeof(HeroController).GetField("hitsSinceShielded",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }
                return _hitsField;
            }
        }

        internal static void SetMax()
        {
            if (!PracticeMod.Settings.ResetCarefreeOnPlatReset) return;
            if (HitsField == null || HeroController.instance == null) return;
            try
            {
                HitsField.SetValue(HeroController.instance, 7);
            }
            catch (Exception e)
            {
                PracticeMod.Instance?.LogError("Carefree injection failed: " + e);
            }
        }
    }

    public class BossPractice : MonoBehaviour
    {
        internal static BossPractice Instance;
        private static bool _reloading;
        private bool _resetting;
        private readonly Dictionary<int, bool> _swordWaiting = new Dictionary<int, bool>();
        private Vector3 _a1PitPos;
        private bool _a1PitPosCached;
        private const int MaxResetRetries = 3;
        private const int RadianceGraceFrames = 300;
        private const int MaxReloadRetries = 2;
        private static readonly Vector3 Phase1PitPos = new Vector3(61.77f, 18.7f, 0f);
        private static readonly Vector3 PlatsPitPos = new Vector3(61.77f, 30f, 0f);
        private static int _reloadRetryCount;
        private float _portalCheck;
        private float _portalRemovedLog;
        private float _resetKeyCooldown;
        private Vector3 _a1HeroPos;
        private bool _a1HeroFacingRight = true;
        private string _widePlatState;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            TryKeys();
        }

        private static void TryKeys()
        {
            // checks if the binded key in settings file is actually an inputif not then clears the bind
            try { Input.GetKeyDown(PracticeMod.Settings.ResetToPlatsKey); }
            catch { PracticeMod.Settings.ResetToPlatsKey = ""; }
        }

        private void OnEnable()
        {
            ModHooks.SceneChanged += OnSceneChanged;
            On.BossSceneController.DoDreamReturn += OnDoDreamReturn;
        }

        private void OnDisable()
        {
            ModHooks.SceneChanged -= OnSceneChanged;
            On.BossSceneController.DoDreamReturn -= OnDoDreamReturn;
            _resetting = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private static void OnSceneChanged(string targetScene)
        {
            _reloading = false;
            if (Instance != null)
            {
                Instance._swordWaiting.Clear();
                Instance._widePlatState = null;
            }
            if (targetScene == "GG_Radiance" && Instance != null)
            {
                Instance.StartCoroutine(Instance.SetupBoss());
            }
        }

        private static void OnDoDreamReturn(On.BossSceneController.orig_DoDreamReturn orig, BossSceneController self)
        {
            _reloading = false;
            orig(self);
        }

        private void Update()
        {
            if (GameManager.instance == null || GameManager.instance.sceneName != "GG_Radiance") return;

            CapturePitFromScene();

            if (PracticeMod.Settings.RemovePortals)
            {
                _portalCheck -= Time.unscaledDeltaTime;
                if (_portalCheck <= 0f)
                {
                    _portalCheck = 0.25f;
                    GameObject existing = GameObject.Find("Portals(Clone)");
                    if (existing != null)
                    {
                        SafeDestroyPortals(existing);
                        _portalRemovedLog = Time.unscaledTime + 2f;
                        PracticeMod.Instance?.Log("3.0 Practice: destroyed Portals(Clone)");
                    }
                    else if (Time.unscaledTime >= _portalRemovedLog)
                    {
                        _portalRemovedLog = Time.unscaledTime + 10f;
                        PracticeMod.Instance?.LogError("3.0 Practice: Remove portals is on but Portals(Clone) is active (reloading=" + _reloading + ", resetting=" + _resetting + ")");
                    }
                }
            }

            if (!_resetting && Time.unscaledTime >= _resetKeyCooldown &&
                !string.IsNullOrEmpty(PracticeMod.Settings.ResetToPlatsKey) &&
                Input.GetKeyDown(PracticeMod.Settings.ResetToPlatsKey))
            {
                _resetKeyCooldown = Time.unscaledTime + 1f;

                int phase = GetCurrentPhase();
                PracticeMod.Instance?.Log("[3.0 Practice][Reset] key pressed (phase=" + phase +
                    ", reloading=" + _reloading + ", resetting=" + _resetting + ")");

                if (PracticeMod.Settings.RestartFromPlatsOnDeath && phase >= 2)
                {
                    StartCoroutine(ResetToPlats());
                }
                else
                {
                    ResetWholeFight();
                }
            }

            if (_reloading || _resetting) return;

            float factor = 1f + 0.5f * PracticeMod.Settings.SwordSpeedMultiplier;
            if (factor <= 1.0001f) return;

            // Speed up the bosss swords
            var rbs = UnityEngine.Object.FindObjectsOfType<Rigidbody2D>();
            for (int i = 0; i < rbs.Length; i++)
            {
                Rigidbody2D rb = rbs[i];
                if (!rb.name.Contains("Radiant Nail")) continue;
                if (!rb.gameObject.activeInHierarchy) continue;
                if (BossRef.TrailNailType != null && rb.GetComponent(BossRef.TrailNailType) != null) continue;

                int id = rb.gameObject.GetInstanceID();
                bool moving = rb.velocity.sqrMagnitude > 0.25f;

                bool waiting;
                if (!_swordWaiting.TryGetValue(id, out waiting)) waiting = true;

                if (moving)
                {
                    if (waiting)
                    {
                        rb.velocity = rb.velocity * factor;
                        _swordWaiting[id] = false;
                    }
                }
                else
                {
                    _swordWaiting[id] = true;
                }
            }
        }

        private void CapturePitFromScene()
        {
            GameObject pit = GameObject.Find("Abyss Pit") ?? BossRef.GetGameObject("Abyss Pit");
            if (pit == null) return;

            float y = pit.transform.position.y;
            if (_a1PitPosCached && y >= _a1PitPos.y) return;

            if (y < 29f)
            {
                _a1PitPos = pit.transform.position;
                _a1PitPosCached = true;
            }
        }

        private IEnumerator SetupBoss()
        {
            CapturePitFromScene();

            if (BossRef.RadianceType == null) yield break;

            GameObject boss = null;
            for (int i = 0; i < 600; i++)
            {
                GameObject candidate = GameObject.Find("Absolute Radiance");
                if (candidate != null && HeroController.instance != null && candidate.transform.parent != null)
                {
                    boss = candidate;
                    break;
                }
                yield return null;
            }
            if (boss == null)
            {
                PracticeMod.Critical("Absolute Radiance scene never became ready (boss/hero/parent missing) after GG_Radiance load");
                RetryReload();
                yield break;
            }

            MonoBehaviour radiance = boss.GetComponent(BossRef.RadianceType) as MonoBehaviour;
            if (radiance == null)
            {
                // before re-adding the component ourselves, to avoid duplicates.
                for (int grace = 0; grace < RadianceGraceFrames && radiance == null; grace++)
                {
                    radiance = boss.GetComponent(BossRef.RadianceType) as MonoBehaviour;
                    yield return null;
                }
            }

            if (radiance == null)
            {
                PracticeMod.Critical("AnyRadiance NOT applied on player, re-adding Radiance component manually");
                boss.AddComponent(BossRef.RadianceType);
                radiance = boss.GetComponent(BossRef.RadianceType) as MonoBehaviour;
                if (radiance == null)
                {
                    PracticeMod.Critical("Could not re-add Radiance component to Absolute Radiance");
                    RetryReload();
                    yield break;
                }
            }

            var all = boss.GetComponents(BossRef.RadianceType);
            if (all.Length > 1)
            {
                PracticeMod.Instance?.LogError("Found " + all.Length + " Radiance components on Absolute Radiance, destroying the duplicates");
                for (int i = all.Length - 1; i >= 1; i--) Destroy(all[i]);
                radiance = all[0] as MonoBehaviour;
            }

            float waited = 0;
            while (waited < 2f && BossRef.GetLogic() == null)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (BossRef.GetLogic() == null)
            {
                PracticeMod.Critical("Radiance logic never started");
                RetryReload();
                yield break;
            }

            CachePhase1Snapshot();
            CarefreeInjection.SetMax();
        }

        // Remember what the phase-1 hero position and the wide plats FSM state look like,
        // so the reset can put everything back exactly the way it was :moai:
        private void CachePhase1Snapshot()
        {
            if (HeroController.instance != null)
            {
                _a1HeroPos = HeroController.instance.transform.position;
                _a1HeroFacingRight = HeroController.instance.transform.localScale.x >= 0;
            }

            GameObject platSets = GameObject.Find("Plat Sets");
            if (platSets != null)
            {
                GameObject wide = platSets.Child("Climb Set/Radiant Plat Wide (2)");
                if (wide != null)
                {
                    PlayMakerFSM fsm = wide.LocateMyFSM("radiant_plat");
                    if (fsm != null && fsm.Fsm != null && fsm.Fsm.ActiveState != null)
                    {
                        _widePlatState = fsm.Fsm.ActiveState.Name;
                    }
                }
            }
        }

        internal IEnumerator ResetToPhase1()
        {
            if (_resetting) yield break;
            _resetting = true;
            try
            {
                if (BossRef.RadianceType == null) yield break;
                GameObject boss = GameObject.Find("Absolute Radiance");
                if (boss == null) yield break;
                MonoBehaviour radiance = boss.GetComponent(BossRef.RadianceType) as MonoBehaviour;
                if (radiance == null) yield break;

                yield return ResetToPhase1(boss, radiance);
            }
            finally
            {
                _resetting = false;
            }
        }

        internal IEnumerator ResetToPlats()
        {
            CarefreeInjection.SetMax();
            CapturePlatsHits();
            PracticeMod.Instance?.Log("[3.0 Practice][Reset] plat reset triggered (phase=" + GetCurrentPhase() + ")");
            yield return ResetToPhase1();

            _resetting = false;
        }

        private static void CapturePlatsHits()
        {
            if (!PracticeMod.Settings.ShowPlatHitsOnReset) return;
            if (GetCurrentPhase() != 2) return;

            GameObject boss = GameObject.Find("Absolute Radiance");
            if (boss == null) return;
            HealthManager hm = boss.GetComponent<HealthManager>();
            if (hm == null) return;

            // Phase 2 boss has 500 hp and each nail hit deals 50 -> 10 hits total.
            int hits = Mathf.Max(0, (500 - hm.hp) / 50);
            PlatsHitsDisplay.Show(Mathf.Min(hits, 10), 10);
        }

        internal static byte GetCurrentPhase()
        {
            if (BossRef.RadianceType == null) return 0;
            GameObject boss = GameObject.Find("Absolute Radiance");
            if (boss == null) return 0;
            Component radiance = boss.GetComponent(BossRef.RadianceType);
            if (radiance == null) return 0;
            return BossRef.GetPhase(radiance);
        }

        private bool TryResetToPhase1(GameObject boss, MonoBehaviour radiance, int attempt)
        {
            try
            {
                // Kill the boss's AI + whatever attack coroutines are running.
                radiance.StopAllCoroutines();
                DetachDeathHandlers(radiance);

                // Pretend we never left phase 1.
                BossRef.SetPhase(radiance, 1);

                HealthManager hm = boss.GetComponent<HealthManager>();
                if (hm != null)
                {
                    hm.hp = 2000;
                    hm.IsInvincible = false;
                }

                RestoreBossToGround(boss);
                RestoreArenaToPhase1();
                RestoreHeroToA1();

                // Rerun the boss own p1 death sequence, which puts it back on the
                // ground plat and sets up the whole arena again
                object iterator = BossRef.Phase1Death.Invoke(radiance, null);
                radiance.StartCoroutine((IEnumerator)iterator);

                try
                {
                    StartCoroutine(KeepVoidDown());
                    StartCoroutine(PortalAndInvincibilityWatchdog(radiance));
                }
                catch (Exception e)
                {
                    PracticeMod.Instance?.LogError("[3.0 Practice][ResetToPhase1] watchdog failed on attempt " + attempt + ": " + e);
                }
                return true;
            }
            catch (Exception e)
            {
                PracticeMod.Instance?.LogError("[3.0 Practice][ResetToPhase1] attempt " + attempt + " of " + MaxResetRetries + " failed: " + e);
                return false;
            }
        }

        private IEnumerator ResetToPhase1(GameObject boss, MonoBehaviour radiance)
        {
            if (BossRef.Phase1Death == null)
            {
                PracticeMod.Critical("AnyRadiance Phase1Death reflection is null; boss is NOT AnyRadiance");
                LoadBossInLoop();
                yield break;
            }

            for (int attempt = 1; attempt <= MaxResetRetries; attempt++)
            {
                if (TryResetToPhase1(boss, radiance, attempt))
                {
                    yield return null;
                    yield break;
                }
                yield return null;
            }

            PracticeMod.Critical("All " + MaxResetRetries + " plat-reset attempts failed, falling back to LoadBossInLoop");
            LoadBossInLoop();
        }

        private IEnumerator PortalAndInvincibilityWatchdog(MonoBehaviour radiance)
        {
            // After a reset the hero is left invincible on purpose
            // Once the boss climbs back up to phase 2, drop the invincibility and kill portals if nedded
            WaitForSeconds wait = new WaitForSeconds(0.1f);
            float elapsed = 0;
            while (!_reloading && elapsed < 12f)
            {
                elapsed += 0.1f;
                yield return wait;

                object value = BossRef.PhaseField?.GetValue(radiance);
                if (value is byte phase && phase >= 2)
                {
                    if (PracticeMod.Settings.RemovePortals)
                    {
                        GameObject existing = GameObject.Find("Portals(Clone)");
                        if (existing != null) SafeDestroyPortals(existing);
                    }
                    PlayerData.instance.isInvincible = false;
                    yield break;
                }
            }

            if (!_reloading && PlayerData.instance != null)
            {
                PracticeMod.Critical("Boss did not reach phase 2 within 12s of reset; boss may be stuck??? idk");
                PlayerData.instance.isInvincible = false;
            }
        }

        private void DetachDeathHandlers(MonoBehaviour target)
        {
            // the boss hooks HealthManager.Die to trigger its phase transitions
            MethodInfo[] handlers =
            {
                BossRef.StartPhase1Death,
                BossRef.StartPhase2Death,
                BossRef.StartPhase3Death,
            };
            for (int i = 0; i < handlers.Length; i++)
            {
                if (handlers[i] == null) continue;
                try
                {
                    var handler = (On.HealthManager.hook_Die)Delegate.CreateDelegate(
                        typeof(On.HealthManager.hook_Die), target, handlers[i]);
                    On.HealthManager.Die -= handler;
                }
                catch (Exception e)
                {
                    PracticeMod.Instance?.LogError("DetachDeathHandlers failed for " + handlers[i].Name + ": " + e);
                }
            }
        }

        private void RestoreBossToGround(GameObject boss)
        {
            boss.transform.position = new Vector3(60.63f, 28.3f, 0.006f);

            MeshRenderer renderer = boss.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = true;

            PolygonCollider2D collider = boss.GetComponent<PolygonCollider2D>();
            if (collider != null) collider.enabled = true;

            GameObject legs = BossRef.GetGameObject("Legs");
            if (legs != null) legs.SetActive(true);

            Rigidbody2D rb = boss.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = Vector2.zero;
        }

        private void RestoreArenaToPhase1()
        {
            Deactivate("White Flash");
            Deactivate("Roar Wave Stun");
            Deactivate("Statue Death Fader");
            Deactivate("Final Explode Statue");
            Deactivate("Stun Eye Glow");
            Deactivate("Ascend Set");

            if (GameCameras.instance != null && GameCameras.instance.cameraShakeFSM != null)
            {
                FsmVariables shakeVars = GameCameras.instance.cameraShakeFSM.FsmVariables;
                if (shakeVars != null)
                {
                    FsmBool rumbleMed = shakeVars.GetFsmBool("RumblingMed");
                    if (rumbleMed != null) rumbleMed.Value = false;
                    FsmBool rumbleBig = shakeVars.GetFsmBool("RumblingBig");
                    if (rumbleBig != null) rumbleBig.Value = false;
                }
            }

            PinVoidDown();

            GameObject portals = GameObject.Find("Portals(Clone)");
            if (portals != null) SafeDestroyPortals(portals);

            GameObject platSets = BossRef.GetGameObject("Plat Sets") ?? GameObject.Find("Plat Sets");
            if (platSets != null)
            {
                var fsms = platSets.GetComponentsInChildren<PlayMakerFSM>(true);
                for (int i = 0; i < fsms.Length; i++)
                {
                    PlayMakerFSM fsm = fsms[i];
                    if (fsm.transform.parent != null && fsm.transform.parent.name == "Ascend Set") continue;
                    fsm.SendEvent("DISAPPEAR");

                    if (BossRef.SpikedPlatformType != null && BossRef.DownInstant != null)
                    {
                        Component sp = fsm.GetComponent(BossRef.SpikedPlatformType);
                        if (sp != null) BossRef.DownInstant.Invoke(sp, null);
                    }
                }

                GameObject wide = platSets.Child("Climb Set/Radiant Plat Wide (2)");
                if (wide != null)
                {
                    PlayMakerFSM wideFsm = wide.LocateMyFSM("radiant_plat");
                    if (wideFsm != null && !string.IsNullOrEmpty(_widePlatState))
                    {
                        try
                        {
                            wideFsm.Fsm.SetState(_widePlatState);
                        }
                        catch (Exception e)
                        {
                            PracticeMod.Instance?.LogError("RestoreArenaToPhase1 could not restore wide plat state: " + e);
                        }
                    }
                }
            }

            Deactivate("CamLock A2");
            Deactivate("CamLocks Ascend");
            Deactivate("CamLock Death1");
            Activate("CamLock A1");
            Activate("CamLock Main");

            GameObject finalHazard = GameObject.Find("Final Hazard Plat");
            if (finalHazard != null) finalHazard.SetActive(false);
        }

        private void PinVoidDown()
        {
            GameObject pit = GameObject.Find("Abyss Pit") ?? BossRef.GetGameObject("Abyss Pit");
            if (pit == null) return;

            iTween.Stop(pit);

            if (!_a1PitPosCached)
            {
                _a1PitPos = Phase1PitPos;
                _a1PitPosCached = true;
                PracticeMod.Instance?.LogError("3.0 Practice: pit cache empty, falling back to " + Phase1PitPos);
            }

            pit.transform.position = _a1PitPos;

            DamageHero dh = pit.GetComponentInChildren<DamageHero>();
            if (dh != null) dh.damageDealt = 2;
        }

        private IEnumerator KeepVoidDown()
        {
            const float window = 12f;
            WaitForSeconds tick = new WaitForSeconds(0.25f);
            float elapsed = 0f;
            bool logged = false;

            while (!_reloading && elapsed < window)
            {
                elapsed += 0.25f;
                yield return tick;

                if (GetCurrentPhase() >= 2) break;

                PinVoidDown();

                if (!logged)
                {
                    logged = true;
                    GameObject pit = GameObject.Find("Abyss Pit") ?? BossRef.GetGameObject("Abyss Pit");
                    PracticeMod.Instance?.Log("[3.0 Practice][Reset] void pinned at y=" +
                        (pit != null ? pit.transform.position.y.ToString() : "?") +
                        " (target y=" + _a1PitPos.y + ")");
                }
            }

            PracticeMod.Instance?.Log("[3.0 Practice][Reset] void pinning finished after " + elapsed + "s (phase=" + GetCurrentPhase() + ")");

            yield return HandVoidBackToGame();
        }

        private IEnumerator HandVoidBackToGame()
        {
            WaitForSeconds tick = new WaitForSeconds(0.25f);
            float waited = 0f;

            while (!_reloading && GetCurrentPhase() < 2 && waited < 15f)
            {
                waited += 0.25f;
                yield return tick;
            }

            if (_reloading) yield break;

            byte phase = GetCurrentPhase();

            if (phase >= 3)
            {
                PracticeMod.Instance?.Log("[3.0 Practice][Reset] phase " + phase + ", leaving the void to the game");
                yield break;
            }

            GameObject pit = GameObject.Find("Abyss Pit") ?? BossRef.GetGameObject("Abyss Pit");
            if (pit == null) yield break;

            float y = pit.transform.position.y;
            if (y >= PlatsPitPos.y - 0.01f)
            {
                PracticeMod.Instance?.Log("[3.0 Practice][Reset] void already up at y=" + y + ", nothing to hand back");
                yield break;
            }

            iTween.MoveTo(pit, new Vector3(PlatsPitPos.x, PlatsPitPos.y, PlatsPitPos.z), 4f);
            PracticeMod.Instance?.Log("[3.0 Practice][Reset] void handed back to the game (y=" + y + " -> " + PlatsPitPos.y + ", phase=" + phase + ")");
        }

        private static void Deactivate(string objectName)
        {
            GameObject go = BossRef.GetGameObject(objectName) ?? GameObject.Find(objectName);
            if (go != null) go.SetActive(false);
        }

        private static void Activate(string objectName)
        {
            GameObject go = BossRef.GetGameObject(objectName) ?? GameObject.Find(objectName);
            if (go != null) go.SetActive(true);
        }

        private void RestoreHeroToA1()
        {
            if (HeroController.instance == null || PlayerData.instance == null) return;

            HeroController hc = HeroController.instance;
            hc.ClearMPSendEvents();
            hc.MaxHealth();
            if (PracticeMod.Settings.FullSoulOnPlatReset)
            {
                PlayerData pd = PlayerData.instance;
                pd.soulLimited = false;
                pd.maxMP = 99;
                pd.MPCharge = 99;
                pd.MPReserve = Mathf.Min(pd.MPReserveMax, pd.MPReserveCap);
                hc.SetMPCharge(99);
            }
            else
            {
                hc.SetMPCharge(0);
            }
            hc.AcceptInput();

            // Back to the spot where we started p1 
            Vector3 stand = _a1HeroPos.Equals(Vector3.zero) ? new Vector3(60, 22, 0.006f) : _a1HeroPos;
            hc.transform.position = stand;

            Vector3 scale = hc.transform.localScale;
            hc.transform.localScale = new Vector3(
                _a1HeroFacingRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x),
                scale.y,
                scale.z);

            Rigidbody2D rb = hc.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0;
            }
            hc.SetHazardRespawn(stand, true);
            hc.enterWithoutInput = false;

            // Keep the hero alive through the transition
            PlayerData.instance.isInvincible = true;
        }

        private void RebuildPortals()
        {
            if (PracticeMod.Settings.RemovePortals)
            {
                GameObject existing = GameObject.Find("Portals(Clone)");
                if (existing != null) SafeDestroyPortals(existing);
                return;
            }

            if (GameObject.Find("Portals(Clone)") != null) return;

            GameObject prefab = BossRef.GetPortalsPrefab();
            if (prefab == null) return;

            GameObject portals = Instantiate(prefab);

            if (BossRef.PortalManagerType == null || BossRef.SpikedPlatformType == null || BossRef.ExcludeTeleport == null)
            {
                return;
            }

            Component pm = portals.AddComponent(BossRef.PortalManagerType);
            var spikedPlats = Resources.FindObjectsOfTypeAll(BossRef.SpikedPlatformType);
            for (int i = 0; i < spikedPlats.Length; i++)
            {
                Component sp = (Component)spikedPlats[i];
                foreach (Transform child in sp.transform)
                {
                    if (child.name.Contains("Plat Spike"))
                    {
                        BossRef.ExcludeTeleport.Invoke(pm, new object[] { child.gameObject });
                    }
                }
            }
        }

        private static void SafeDestroyPortals(GameObject portals)
        {
            // The PortalManagers randomization Timer would keep firing on a destroyed object,
            // so stop/dispose it before deleting the portals
            if (BossRef.PortalManagerType != null && BossRef.PortalTimerField != null)
            {
                Component pm = portals.GetComponent(BossRef.PortalManagerType);
                if (pm != null)
                {
                    var timer = BossRef.PortalTimerField.GetValue(pm) as Timer;
                    if (timer != null)
                    {
                        try { timer.Stop(); } catch { }
                        try { timer.Dispose(); } catch { }
                    }
                }
            }
            Destroy(portals);
        }

        internal static void ResetWholeFight(bool userInitiated = true)
        {
            if (Instance == null) return;
            if (GameManager.instance == null || GameManager.instance.sceneName != "GG_Radiance") return;

            _reloading = true;
            if (userInitiated) _reloadRetryCount = 0;
            Instance._swordWaiting.Clear();
            if (PracticeMod.Settings.ResetCarefreeOnPlatReset) CarefreeInjection.SetMax();
            Instance.StartCoroutine(ReloadSequence());
            Instance.StartCoroutine(ClearStuckReloadFlag());
        }

        private static IEnumerator ClearStuckReloadFlag()
        {
            yield return new WaitForSeconds(10f);
            if (!_reloading) yield break;

            PracticeMod.Critical("Reload did not complete within 10s, force-clearing the reload flag");
            _reloading = false;
        }

        private static void RetryReload()
        {
            if (_reloadRetryCount >= MaxReloadRetries)
            {
                PracticeMod.Critical("Boss setup failed " + _reloadRetryCount + " times, giving up on reloading");
                _reloading = false;
                return;
            }

            _reloadRetryCount++;
            PracticeMod.Instance?.Log("[3.0 Practice][Reset] boss setup failed, reloading the scene (retry " + _reloadRetryCount + "/" + MaxReloadRetries + ")");
            ResetWholeFight(false);
        }

        private static IEnumerator ReloadSequence()
        {
            yield return null;
            LoadBossInLoop();
        }

        internal static void LoadBossInLoop()
        {
            if (GameManager.instance == null) return;

            string sceneToLoad = GameManager.instance.GetSceneNameString();
            if (sceneToLoad != "GG_Radiance") return;
            _reloading = true;

            if (sceneToLoad == "GG_Radiance")
            {
                // Clear out any leftover nails from the previous attempt
                var rbs = Resources.FindObjectsOfTypeAll<Rigidbody2D>();
                for (int i = 0; i < rbs.Length; i++)
                {
                    GameObject go = rbs[i].gameObject;
                    if (!go.name.Contains("Radiant Nail(Clone)")) continue;
                    PolygonCollider2D col = go.GetComponent<PolygonCollider2D>();
                    if (col != null) col.enabled = false;
                    MeshRenderer mr = go.GetComponent<MeshRenderer>();
                    if (mr != null) mr.enabled = false;
                    rbs[i].isKinematic = false;
                    go.SetActive(false);
                }
            }

            LoadBossScene(sceneToLoad);
        }

        internal static void LoadBossScene(string sceneToLoad)
        {
            if (GameManager.instance == null || HeroController.instance == null || PlayerData.instance == null) return;

            GameManager gm = GameManager.instance;
            HeroController hc = HeroController.instance;

            CleanLeftoversBeforeReload();

            PlayerData.instance.dreamReturnScene = "GG_Workshop";
            PlayMakerFSM.BroadcastEvent("BOX DOWN DREAM");
            PlayMakerFSM.BroadcastEvent("CONVO CANCEL");

            GameObject inspect = PracticeMod.Inspect;
            if (inspect != null)
            {
                PlayMakerFSM bossUI = inspect.LocateMyFSM("GG Boss UI");
                if (bossUI != null)
                {
                    CreateObject create = bossUI.GetAction<CreateObject>("Transition", 0);
                    if (create != null && create.gameObject != null)
                    {
                        GameObject transition = create.gameObject.Value;
                        if (transition != null)
                        {
                            PlayMakerFSM[] childFsms = transition.GetComponentsInChildren<PlayMakerFSM>();
                            for (int i = 0; i < childFsms.Length; i++)
                            {
                                childFsms[i].SendEvent("GG TRANSITION OUT");
                            }
                        }
                    }
                }
            }

            hc.ClearMPSendEvents();
            gm.TimePasses();
            gm.ResetSemiPersistentItems();
            hc.enterWithoutInput = true;
            hc.AcceptInput();

            int bossLevel = 2;
            if (BossSceneController.Instance != null) bossLevel = BossSceneController.Instance.BossLevel;

            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = sceneToLoad,
                EntryGateName = "door_dreamEnter",
                EntryDelay = 0,
                Visualization = GameManager.SceneLoadVisualizations.GodsAndGlory,
                PreventCameraFadeOut = true,
            });

            if (Instance != null) Instance.StartCoroutine(FixSoul(bossLevel));
        }

        private static void CleanLeftoversBeforeReload()
        {
            PlayerData.instance.isInvincible = false;

            GameObject portals = GameObject.Find("Portals(Clone)");
            if (portals != null) SafeDestroyPortals(portals);

            if (BossRef.RadianceType == null) return;

            var bosses = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < bosses.Length; i++)
            {
                GameObject go = bosses[i];
                if (go == null || go.name != "Absolute Radiance") continue;

                var comps = go.GetComponents(BossRef.RadianceType);
                for (int c = comps.Length - 1; c >= 0; c--) Destroy(comps[c]);
                Destroy(go);
            }
        }

        private static IEnumerator FixSoul(int bossLevel)
        {
            yield return null;
            yield return null;
            yield return new WaitForSeconds(1f);
            if (HeroController.instance != null)
            {
                HeroController.instance.AddMPCharge(1);
                HeroController.instance.AddMPCharge(-1);
            }
            if (BossSceneController.Instance != null)
            {
                BossSceneController.Instance.BossLevel = bossLevel;
            }
        }
    }
}
