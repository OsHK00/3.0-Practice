using UnityEngine;

namespace Practice_3_0
{
    internal class PlatsHitsDisplay : MonoBehaviour
    {
        private const float DisplayDuration = 3f;

        private string _text = "";
        private float _shownAt = float.MinValue;
        private GUIStyle _style;

        internal static PlatsHitsDisplay Instance { get; private set; }

        internal static void Create()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("PracticePlatsHitsDisplay");
            DontDestroyOnLoad(go);
            go.AddComponent<PlatsHitsDisplay>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        internal static void Show(int hits, int total)
        {
            if (Instance == null) return;
            Instance._text = hits + "/" + total;
            Instance._shownAt = Time.unscaledTime;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_text)) return;
            if (!PracticeMod.Settings.ShowPlatHitsOnReset) return;
            if (Time.unscaledTime - _shownAt > DisplayDuration) return;
            if (GameManager.instance == null || GameManager.instance.sceneName != "GG_Radiance") return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 48,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            float w = 300f;
            float h = 100f;
            float x = (Screen.width - w) / 2f;
            float y = (Screen.height - h) / 2f;

            _style.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
            GUI.Label(new Rect(x + 2, y + 2, w, h), _text, _style);

            _style.normal.textColor = new Color(1f, 1f, 1f, 1f);
            GUI.Label(new Rect(x, y, w, h), _text, _style);
        }
    }
}