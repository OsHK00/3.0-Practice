using System.Collections.Generic;
using System.Reflection;
using InControl;
using Modding;
using Modding.Menu;
using Modding.Menu.Config;
using UnityEngine;
using UnityEngine.UI;

namespace Practice_3_0
{
    public class PracticeMod : Mod, IGlobalSettings<GlobalModSettings>, ICustomMenuMod
    {
        internal static PracticeMod Instance;
        internal static GlobalModSettings Settings = new GlobalModSettings();
        internal static GameObject Inspect;

        private static readonly Dictionary<InControl.Key, string> KeyToUnity = new Dictionary<InControl.Key, string>();
        private static readonly Dictionary<string, InControl.Key> UnityToKey = new Dictionary<string, InControl.Key>();
        private static readonly Dictionary<InControl.Mouse, string> MouseToUnity = new Dictionary<InControl.Mouse, string>();
        private static readonly Dictionary<string, InControl.Mouse> UnityToMouse = new Dictionary<string, InControl.Mouse>();

        private static readonly MethodInfo ActionSetUpdate = typeof(PlayerActionSet).GetMethod(
            "Update",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(ulong), typeof(float) },
            null);

        private static ResetActionSet ResetActions;
        private bool _initialized;

        internal static void Critical(string msg) => Instance?.LogError("[3.0 Practice][CRITICAL] " + msg);

        static PracticeMod()
        {
            AddKeyMap(InControl.Key.A, "a");
            AddKeyMap(InControl.Key.B, "b");
            AddKeyMap(InControl.Key.C, "c");
            AddKeyMap(InControl.Key.D, "d");
            AddKeyMap(InControl.Key.E, "e");
            AddKeyMap(InControl.Key.F, "f");
            AddKeyMap(InControl.Key.G, "g");
            AddKeyMap(InControl.Key.H, "h");
            AddKeyMap(InControl.Key.I, "i");
            AddKeyMap(InControl.Key.J, "j");
            AddKeyMap(InControl.Key.K, "k");
            AddKeyMap(InControl.Key.L, "l");
            AddKeyMap(InControl.Key.M, "m");
            AddKeyMap(InControl.Key.N, "n");
            AddKeyMap(InControl.Key.O, "o");
            AddKeyMap(InControl.Key.P, "p");
            AddKeyMap(InControl.Key.Q, "q");
            AddKeyMap(InControl.Key.R, "r");
            AddKeyMap(InControl.Key.S, "s");
            AddKeyMap(InControl.Key.T, "t");
            AddKeyMap(InControl.Key.U, "u");
            AddKeyMap(InControl.Key.V, "v");
            AddKeyMap(InControl.Key.W, "w");
            AddKeyMap(InControl.Key.X, "x");
            AddKeyMap(InControl.Key.Y, "y");
            AddKeyMap(InControl.Key.Z, "z");
            AddKeyMap(InControl.Key.Key0, "0");
            AddKeyMap(InControl.Key.Key1, "1");
            AddKeyMap(InControl.Key.Key2, "2");
            AddKeyMap(InControl.Key.Key3, "3");
            AddKeyMap(InControl.Key.Key4, "4");
            AddKeyMap(InControl.Key.Key5, "5");
            AddKeyMap(InControl.Key.Key6, "6");
            AddKeyMap(InControl.Key.Key7, "7");
            AddKeyMap(InControl.Key.Key8, "8");
            AddKeyMap(InControl.Key.Key9, "9");
            AddKeyMap(InControl.Key.F1, "f1");
            AddKeyMap(InControl.Key.F2, "f2");
            AddKeyMap(InControl.Key.F3, "f3");
            AddKeyMap(InControl.Key.F4, "f4");
            AddKeyMap(InControl.Key.F5, "f5");
            AddKeyMap(InControl.Key.F6, "f6");
            AddKeyMap(InControl.Key.F7, "f7");
            AddKeyMap(InControl.Key.F8, "f8");
            AddKeyMap(InControl.Key.F9, "f9");
            AddKeyMap(InControl.Key.F10, "f10");
            AddKeyMap(InControl.Key.F11, "f11");
            AddKeyMap(InControl.Key.F12, "f12");
            AddKeyMap(InControl.Key.F13, "f13");
            AddKeyMap(InControl.Key.F14, "f14");
            AddKeyMap(InControl.Key.F15, "f15");
            AddKeyMap(InControl.Key.Space, "space");
            AddKeyMap(InControl.Key.Return, "return");
            AddKeyMap(InControl.Key.Escape, "escape");
            AddKeyMap(InControl.Key.Tab, "tab");
            AddKeyMap(InControl.Key.Backspace, "backspace");
            AddKeyMap(InControl.Key.Delete, "delete");
            AddKeyMap(InControl.Key.Insert, "insert");
            AddKeyMap(InControl.Key.Home, "home");
            AddKeyMap(InControl.Key.End, "end");
            AddKeyMap(InControl.Key.PageUp, "page up");
            AddKeyMap(InControl.Key.PageDown, "page down");
            AddKeyMap(InControl.Key.UpArrow, "up");
            AddKeyMap(InControl.Key.DownArrow, "down");
            AddKeyMap(InControl.Key.LeftArrow, "left");
            AddKeyMap(InControl.Key.RightArrow, "right");
            AddKeyMap(InControl.Key.LeftShift, "left shift");
            AddKeyMap(InControl.Key.RightShift, "right shift");
            AddKeyMap(InControl.Key.LeftControl, "left ctrl");
            AddKeyMap(InControl.Key.RightControl, "right ctrl");
            AddKeyMap(InControl.Key.LeftAlt, "left alt");
            AddKeyMap(InControl.Key.RightAlt, "right alt");
            AddKeyMap(InControl.Key.Numlock, "numlock");
            AddKeyMap(InControl.Key.CapsLock, "caps lock");
            AddKeyMap(InControl.Key.Clear, "clear");
            AddKeyMap(InControl.Key.Pad0, "[0]");
            AddKeyMap(InControl.Key.Pad1, "[1]");
            AddKeyMap(InControl.Key.Pad2, "[2]");
            AddKeyMap(InControl.Key.Pad3, "[3]");
            AddKeyMap(InControl.Key.Pad4, "[4]");
            AddKeyMap(InControl.Key.Pad5, "[5]");
            AddKeyMap(InControl.Key.Pad6, "[6]");
            AddKeyMap(InControl.Key.Pad7, "[7]");
            AddKeyMap(InControl.Key.Pad8, "[8]");
            AddKeyMap(InControl.Key.Pad9, "[9]");
            AddKeyMap(InControl.Key.PadPlus, "[+]");
            AddKeyMap(InControl.Key.PadMinus, "[-]");
            AddKeyMap(InControl.Key.PadMultiply, "[*]");
            AddKeyMap(InControl.Key.PadDivide, "[/]");
            AddKeyMap(InControl.Key.PadPeriod, "[.]");
            AddKeyMap(InControl.Key.PadEnter, "[enter]");
            AddKeyMap(InControl.Key.Backquote, "`");
            AddKeyMap(InControl.Key.Minus, "-");
            AddKeyMap(InControl.Key.Equals, "=");
            AddKeyMap(InControl.Key.LeftBracket, "[");
            AddKeyMap(InControl.Key.RightBracket, "]");
            AddKeyMap(InControl.Key.Backslash, "\\");
            AddKeyMap(InControl.Key.Semicolon, ";");
            AddKeyMap(InControl.Key.Quote, "'");
            AddKeyMap(InControl.Key.Comma, ",");
            AddKeyMap(InControl.Key.Period, ".");
            AddKeyMap(InControl.Key.Slash, "/");

            MouseToUnity[InControl.Mouse.LeftButton] = "mouse 0";
            MouseToUnity[InControl.Mouse.RightButton] = "mouse 1";
            MouseToUnity[InControl.Mouse.MiddleButton] = "mouse 2";
            foreach (var pair in MouseToUnity)
            {
                UnityToMouse[pair.Value] = pair.Key;
            }
        }

        public PracticeMod() : base("3.0 Practice")
        {
        }

        public override string GetVersion() => "1.0.0";

        public void OnLoadGlobal(GlobalModSettings s)
        {
            Settings = s ?? new GlobalModSettings();
        }

        public GlobalModSettings OnSaveGlobal() => Settings;

        public bool ToggleButtonInsideMenu => true;

        public MenuScreen GetMenuScreen(MenuScreen lastMenu, ModToggleDelegates? toggleDelegates)
        {
            EnsureResetActions();
            ApplySavedBinding();

            MenuBuilder builder = MenuUtils.CreateMenuBuilderWithBackButton("3.0 Practice", lastMenu, out MenuButton backButton);
            builder.CreateTitle("3.0 Practice", MenuTitleStyle.vanillaStyle);
            builder.AddContent(RegularGridLayout.CreateVerticalLayout(105f, Vector2.zero), area =>
            {
                MenuUtils.AddModMenuContent(GetMenuData(), area, lastMenu);
                KeybindContent.AddKeybind(area, "ResetToPlatsKey", ResetActions.ResetToPlats, new KeybindConfig
                {
                    Label = "Reset plats key",
                });
            });
            return builder.Build();
        }

        private List<IMenuMod.MenuEntry> GetMenuData()
        {
            return new List<IMenuMod.MenuEntry>
            {
                new IMenuMod.MenuEntry
                {
                    Name = "Remove portals",
                    Description = "no powers",
                    Values = new[] { "On", "Off" },
                    Saver = i => Settings.RemovePortals = i == 0,
                    Loader = () => Settings.RemovePortals ? 0 : 1,
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Sword speed",
                    Description = "multiplies the speed of the nail swords.",
                    Values = new[] { "Normal", "1.5x", "2x" },
                    Saver = i => Settings.SwordSpeedMultiplier = i,
                    Loader = () => Settings.SwordSpeedMultiplier,
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Restart from plats on death",
                    Description = "yeah",
                    Values = new[] { "On", "Off" },
                    Saver = i => Settings.RestartFromPlatsOnDeath = i == 0,
                    Loader = () => Settings.RestartFromPlatsOnDeath ? 0 : 1,
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Restart whole fight on death",
                    Description = "not finished).",
                    Values = new[] { "On", "Off" },
                    Saver = i => Settings.RestartOnDeath = i == 0,
                    Loader = () => Settings.RestartOnDeath ? 0 : 1,
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Reset Carefree RNG",
                    Description = "resets cfm to 90% chance on plat reset",
                    Values = new[] { "On", "Off" },
                    Saver = i => Settings.ResetCarefreeOnPlatReset = i == 0,
                    Loader = () => Settings.ResetCarefreeOnPlatReset ? 0 : 1,
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Show Carefree probability",
                    Description = "shows the Carefree Melody activation percentage",
                    Values = new[] { "On", "Off" },
                    Saver = i => Settings.ShowCarefreeChance = i == 0,
                    Loader = () => Settings.ShowCarefreeChance ? 0 : 1,
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Full soul on plat reset",
                    Description = "starts the new plat attempt with full soul",
                    Values = new[] { "On", "Off" },
                    Saver = i => Settings.FullSoulOnPlatReset = i == 0,
                    Loader = () => Settings.FullSoulOnPlatReset ? 0 : 1,
                },
                new IMenuMod.MenuEntry
                {
                    Name = "Show plat hits on reset",
                    Description = "shows the hits dealt (X/10) when the plats reset",
                    Values = new[] { "On", "Off" },
                    Saver = i => Settings.ShowPlatHitsOnReset = i == 0,
                    Loader = () => Settings.ShowPlatHitsOnReset ? 0 : 1,
                },
            };
        }

        private static void AddKeyMap(InControl.Key key, string unityName)
        {
            KeyToUnity[key] = unityName;
            UnityToKey[unityName] = key;
        }

        private static void EnsureResetActions()
        {
            if (ResetActions != null) return;
            ResetActions = new ResetActionSet();
            ResetActions.ResetToPlats.OnBindingsChanged += OnResetBindingChanged;
        }

        private static void ApplySavedBinding()
        {
            if (ResetActions == null) return;
            ResetActions.ResetToPlats.ClearBindings();
            InputHandler.KeyOrMouseBinding binding = ParseUnityName(Settings.ResetToPlatsKey);
            if (binding.Key == InControl.Key.None && binding.Mouse == InControl.Mouse.None) return;
            KeybindUtil.AddKeyOrMouseBinding(ResetActions.ResetToPlats, binding);
        }

        private static void OnResetBindingChanged()
        {
            if (ResetActions == null) return;
            Settings.ResetToPlatsKey = ToUnityName(KeybindUtil.GetKeyOrMouseBinding(ResetActions.ResetToPlats));
        }

        private static string ToUnityName(InputHandler.KeyOrMouseBinding binding)
        {
            if (binding.Key != InControl.Key.None)
            {
                string name;
                return KeyToUnity.TryGetValue(binding.Key, out name) ? name : string.Empty;
            }
            if (binding.Mouse != InControl.Mouse.None)
            {
                string name;
                return MouseToUnity.TryGetValue(binding.Mouse, out name) ? name : string.Empty;
            }
            return string.Empty;
        }

        private static InputHandler.KeyOrMouseBinding ParseUnityName(string unityName)
        {
            if (string.IsNullOrEmpty(unityName)) return default;
            InControl.Key key;
            if (UnityToKey.TryGetValue(unityName, out key)) return new InputHandler.KeyOrMouseBinding(key);
            InControl.Mouse mouse;
            if (UnityToMouse.TryGetValue(unityName, out mouse)) return new InputHandler.KeyOrMouseBinding(mouse);
            return default;
        }

        private sealed class ResetActionSet : PlayerActionSet
        {
            internal readonly PlayerAction ResetToPlats;

            internal ResetActionSet()
            {
                ResetToPlats = CreatePlayerAction("ResetToPlats");
            }
        }

        private sealed class ActionPump : MonoBehaviour
        {
            private ulong _tick;

            private void Update()
            {
                if (PracticeMod.ResetActions == null || ActionSetUpdate == null) return;
                try
                {
                    _tick++;
                    ActionSetUpdate.Invoke(PracticeMod.ResetActions, new object[] { _tick, Time.unscaledDeltaTime });
                }
                catch
                {
                }
            }
        }

        public override List<(string, string)> GetPreloadNames()
        {
            return new List<(string, string)>
            {
                ("GG_Workshop", "GG_Statue_Hornet/Inspect"),
            };
        }

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            if (preloadedObjects != null && preloadedObjects.TryGetValue("GG_Workshop", out var sceneObjects))
            {
                GameObject inspect;
                if (sceneObjects.TryGetValue("GG_Statue_Hornet/Inspect", out inspect))
                {
                    Inspect = inspect;
                }
            }
            DoInit();
        }

        public override void Initialize()
        {
            DoInit();
        }

        private void DoInit()
        {
            if (_initialized) return;
            _initialized = true;

            Instance = this;
            EnsureResetActions();

            GameObject pumpHost = new GameObject("3.0 Practice Keybind Pump");
            GameObject.DontDestroyOnLoad(pumpHost);
            pumpHost.AddComponent<ActionPump>();

            ModHooks.TakeHealthHook += OnTakeHealth;
            ModHooks.AfterTakeDamageHook += OnAfterTakeDamage;
            ModHooks.AfterSavegameLoadHook += _ => EnsureComponent();
            ModHooks.NewGameHook += EnsureComponent;

            EnsureComponent();
            CarefreeDisplay.Create();
            PlatsHitsDisplay.Create();
        }

        private static void EnsureComponent()
        {
            if (GameManager.instance == null) return;

            if (GameManager.instance.gameObject.GetComponent<BossPractice>() == null)
            {
                GameManager.instance.gameObject.AddComponent<BossPractice>();
            }
        }

        private static int OnAfterTakeDamage(int hazardType, int damageAmount)
        {
            CarefreeDisplay.Refresh();
            return damageAmount;
        }

        private static int OnTakeHealth(int damage)
        {
            if (damage <= 0) return damage;
            if (GameManager.instance == null || GameManager.instance.sceneName != "GG_Radiance") return damage;
            if (PlayerData.instance == null || BossPractice.Instance == null) return damage;

            bool lethal = damage >= PlayerData.instance.health;
            if (!lethal) return damage;

            // Lethal hit in the plats phase, catch it and reset to plats instead of dying
            if (BossPractice.GetCurrentPhase() >= 2 && Settings.RestartFromPlatsOnDeath)
            {
                BossPractice.Instance.StartCoroutine(BossPractice.Instance.ResetToPlats());
                return 0;
            }

            if (Settings.RestartOnDeath)
            {
                //this would take time, for now just reset to plats
            }

            return damage;
        }
    }
}