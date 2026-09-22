using System;

namespace Practice_3_0
{
    [Serializable]
    public class GlobalModSettings
    {
        public bool RemovePortals = false;
        public int SwordSpeedMultiplier = 0;
        public bool RestartFromPlatsOnDeath = false;
        public bool RestartOnDeath = false;
        public bool ResetCarefreeOnPlatReset = false;
        public bool ShowCarefreeChance = false;
        public bool FullSoulOnPlatReset = false;
        public bool ShowPlatHitsOnReset = true;
        public string ResetToPlatsKey = "";
    }
}