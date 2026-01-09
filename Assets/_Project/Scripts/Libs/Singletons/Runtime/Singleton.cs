
namespace FS.Patterns
{
    public abstract class Singleton<T> where T : class, new()
    {
        private static T _instance;
        public static T Instance => _instance ??= new T();

        public static bool HasInstance => _instance != null;
        public static T TryGetInstance => _instance;

        protected Singleton()
        {
            InitializeSingleton();
        }

        protected virtual void InitializeSingleton() {}
    }
}