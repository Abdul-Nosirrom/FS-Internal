namespace FS.RuntimeDebug
{
    public interface IDebugProvider
    {
        public string DebugName { get; }
        public void OnDebugGUI();
        public void OnDebugDraw();
    }
}