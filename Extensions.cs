using HutongGames.PlayMaker;
using UnityEngine;

namespace Practice_3_0
{
    internal static class Extensions
    {
        public static PlayMakerFSM LocateMyFSM(this GameObject go, string fsmName)
        {
            if (go == null) return null;
            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            for (int i = 0; i < fsms.Length; i++)
            {
                if (fsms[i].FsmName == fsmName) return fsms[i];
            }
            return null;
        }

        public static PlayMakerFSM LocateMyFSM(this Component comp, string fsmName)
        {
            return comp != null ? comp.gameObject.LocateMyFSM(fsmName) : null;
        }

        public static FsmState GetState(this PlayMakerFSM fsm, string stateName)
        {
            if (fsm == null || fsm.Fsm == null) return null;
            return fsm.Fsm.GetState(stateName);
        }

        public static T GetAction<T>(this PlayMakerFSM fsm, string stateName, int actionIndex) where T : FsmStateAction
        {
            FsmState state = fsm.GetState(stateName);
            if (state == null || state.Actions == null || actionIndex < 0 || actionIndex >= state.Actions.Length)
            {
                return null;
            }
            return state.Actions[actionIndex] as T;
        }

        public static GameObject Child(this GameObject go, string path)
        {
            if (go == null) return null;
            Transform t = go.transform.Find(path);
            return t != null ? t.gameObject : null;
        }
    }
}