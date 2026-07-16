namespace MacacaGames.ViewSystem
{
    /// <summary>
    /// Optional lifecycle for cleanup that should happen after the leave transition has
    /// completed, immediately before a ViewElement is disabled and returned to a pool.
    /// </summary>
    public interface IViewElementPoolLifeCycle
    {
        void OnBeforeReturnToPool();
    }
}
