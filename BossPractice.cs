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
        private float _portalCheck;
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
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private static void OnSceneChanged(string targetScene)
        {
            _reloading = false;
            if (Instance != null) Instance._swordWaiting.Clear();
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
            if (_reloading || _resetting) return;
            if (GameManager.instance == null || GameManager.instance.sceneName != "GG_Radiance") return;

            CapturePitFromScene();

            if (PracticeMod.Settings.RemovePortals)
            {
                _portalCheck -= Time.unscaledDeltaTime;
                if (_portalCheck <= 0f)
                {
                    _portalCheck = 0.25f;
                    GameObject existing = GameObject.Find("Portals(Clone)");
                    if (existing != null) SafeDestroyPortals(existing);
                }
            }

            if (!string.IsNullOrEmpty(PracticeMod.Settings.ResetToPlatsKey) && GetCurrentPhase() >= 2)
            {
                if (Input.GetKeyDown(PracticeMod.Settings.ResetToPlatsKey))
                {
                    StartCoroutine(ResetToPlats());
                }
            }

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
            // Grab the pit's phase-1 position once (the boss moves it up in phase 3),
            // so we can put it back down when resetting to plats
            if (_a1PitPosCached) return;
            GameObject pit = GameObject.Find("Abyss Pit");
            if (pit != null && pit.transform.position.y < 29f)
            {
                _a1PitPos = pit.transform.position;
                _a1PitPosCached = true;
            }
        }

        private IEnumerator SetupBoss()
        {
            CapturePitFromScene();
            yield return null;
            yield return null;

            if (BossRef.RadianceType == null) yield break;

            GameObject boss = null;
            for (int i = 0; i < 600; i++)
            {
                GameObject candidate = GameObject.Find("Absolute Radiance");
                if (candidate != null && candidate.GetComponent(BossRef.RadianceType) != null)
                {
                    boss = candidate;
                    break;
                }
                yield return null;
            }
            if (boss == null) yield break;

            MonoBehaviour radiance = boss.GetComponent(BossRef.RadianceType) as MonoBehaviour;
            if (radiance == null) yield break;

            float waited = 0;
            while (waited < 2f && BossRef.GetLogic() == null)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
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
            yield return ResetToPhase1();
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

        private bool TryResetToPhase1(GameObject boss, MonoBehaviour radiance)
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

                if (BossRef.Phase1Death == null)
                {
                    LoadBossInLoop();
                    return false;
                }

                // Rerun the boss own p1 death sequence, which puts it back on the
                // ground plat and sets up the whole arena again
                object iterator = BossRef.Phase1Death.Invoke(radiance, null);
                radiance.StartCoroutine((IEnumerator)iterator);
                StartCoroutine(PortalAndInvincibilityWatchdog(radiance));
                return true;
            }
            catch (Exception e)
            {
                PracticeMod.Instance?.LogError("ResetToPhase1 failed: " + e);
                try { LoadBossInLoop(); } catch { }
                return false;
            }
        }

        private IEnumerator ResetToPhase1(GameObject boss, MonoBehaviour radiance)
        {
            if (!TryResetToPhase1(boss, radiance)) yield break;
            yield return null;
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

            GameObject pit = GameObject.Find("Abyss Pit");
            if (pit != null)
            {
                // Cancel the tween that dragged the pit up in phase 3, then lower it back
                iTween.Stop(pit);
                if (_a1PitPosCached)
                {
                    pit.transform.position = _a1PitPos;
                    PracticeMod.Instance?.Log("3.0 Practice: reset lowered void to " + _a1PitPos);
                }
                else
                {
                    PracticeMod.Instance?.LogError("3.0 Practice: pit position not cached, void NOT lowered (current y=" + pit.transform.position.y + ")");
                }
                // Phase-1 pit is just a hazard, not instant death like in phase 3.
                DamageHero dh = pit.GetComponentInChildren<DamageHero>();
                if (dh != null) dh.damageDealt = 2;
            }

            GameObject portals = GameObject.Find("Portals(Clone)");
            if (portals != null) SafeDestroyPortals(portals);

            var attacks = UnityEngine.Object.FindObjectsOfType<GameObject>(true);
            for (int i = 0; i < attacks.Length; i++)
            {
                GameObject a = attacks[i];
                if (a.name.Contains("Radiant Orb") || a.name.Contains("Radiant Nail") ||
                    a.name.Contains("Radiant Beam") || a.name.Contains("Beam Orb"))
                {
                    a.SetActive(false);
                }
            }

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

        private void SafeDestroyPortals(GameObject portals)
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

        internal static void ReloadBoss()
        {
            if (Instance == null) return;
            _reloading = true;
            Instance.StartCoroutine(ReloadSequence());
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