using GameMain.UnityGameFramework.Scripts.Runtime;

namespace UnityGameFramework.Runtime
{
    // Game components live in the hot assembly; the framework component registry is AOT.
    public static class HotGameEntry
    {
        public static HeroComponent HeroManager => GameEntry.GetComponent<HeroComponent>();
        public static ChatAIComponent ChatAI => GameEntry.GetComponent<ChatAIComponent>();
        public static SoundComponent Sound => GameEntry.GetComponent<SoundComponent>();
        public static DebuggerComponent Debugger => GameEntry.GetComponent<DebuggerComponent>();
    }
}
