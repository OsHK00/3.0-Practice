using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Practice_3_0
{
    internal class CarefreeDisplay : MonoBehaviour
    {
        private static readonly float[] Percentages = { 0f, 10.1f, 20.2f, 30.3f, 50.5f, 70.7f, 80.8f, 90.9f };

        private static readonly FieldInfo HitsField = typeof(HeroController)
            .GetField("hitsSinceShielded", BindingFlags.NonPublic | BindingFlags.Instance);

        private string _text = "";
        private GUIStyle _style;

        internal static CarefreeDisplay Instance { get; private set; }

        internal static void Create()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("PracticeCarefreeDisplay");
            DontDestroyOnLoad(go);
            go.AddComponent<CarefreeDisplay>();
        }

        private void Awake()
        {
            Instance = this;
            StartCoroutine(RefreshLoop());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private IEnumerator RefreshLoop()
        {
            WaitForSeconds wait = new WaitForSeconds(0.5f);
            while (true)
            {
                Refresh();
                yield return wait;
            }
        }

        internal static void Refresh()
        {
            if (Instance == null) return;
            if (HitsField == null || HeroController.instance == null) return;

            int hits = (int)HitsField.GetValue(HeroController.instance);
            hits = Mathf.Clamp(hits, 0, Percentages.Length - 1);
            float pct = Percentages[hits];
            Instance._text = pct == 0f ? "Jackpot\n0%" : "Jackpot\n" + pct.ToString("F1") + "%";
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_text)) return;
            if (!PracticeMod.Settings.ShowCarefreeChance) return;
            if (GameManager.instance == null || GameManager.instance.sceneName != "GG_Radiance") return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperRight
                };
            }

            float w = 250f;
            float h = 80f;
            float x = Screen.width - w - 10f;
            float y = 10f;

            _style.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
            GUI.Label(new Rect(x + 2, y + 2, w, h), _text, _style);

            _style.normal.textColor = new Color(1f, 1f, 1f, 1f);
            GUI.Label(new Rect(x, y, w, h), _text, _style);
        }
    }
}