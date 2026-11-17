namespace UnityGameFramework.Runtime
{
    /// <summary>
    /// 游戏入口。
    /// </summary>
    public static class GameEntry
    {
        public const float LogicDeltaTime = FrameworkEntry.LogicDeltaTime;

        public static BaseComponent Base => GetComponent<BaseComponent>();
        public static ConfigComponent Config => GetComponent<ConfigComponent>();
        public static DataNodeComponent DataNode => GetComponent<DataNodeComponent>();
        public static DataTableComponent DataTable => GetComponent<DataTableComponent>();
        public static DownloadComponent Download => GetComponent<DownloadComponent>();
        public static EntityComponent Entity => GetComponent<EntityComponent>();
        public static EventComponent Event => GetComponent<EventComponent>();
        public static FileSystemComponent FileSystem => GetComponent<FileSystemComponent>();
        public static FsmComponent Fsm => GetComponent<FsmComponent>();
        public static LocalizationComponent Localization => GetComponent<LocalizationComponent>();
        public static NetworkComponent Network => GetComponent<NetworkComponent>();
        public static ObjectPoolComponent ObjectPool => GetComponent<ObjectPoolComponent>();
        public static ProcedureComponent Procedure => GetComponent<ProcedureComponent>();
        public static ResourceComponent Resource => GetComponent<ResourceComponent>();
        public static SceneComponent Scene => GetComponent<SceneComponent>();
        public static SettingComponent Setting => GetComponent<SettingComponent>();
        public static UIComponent UI => GetComponent<UIComponent>();
        public static WebRequestComponent WebRequest => GetComponent<WebRequestComponent>();

        public static T GetComponent<T>() where T : GameFrameworkComponent => FrameworkEntry.GetComponent<T>();
        public static GameFrameworkComponent GetComponent(System.Type type) => FrameworkEntry.GetComponent(type);
        public static GameFrameworkComponent GetComponent(string typeName) => FrameworkEntry.GetComponent(typeName);
        public static void Shutdown(ShutdownType shutdownType) => FrameworkEntry.Shutdown(shutdownType);
    }
}
