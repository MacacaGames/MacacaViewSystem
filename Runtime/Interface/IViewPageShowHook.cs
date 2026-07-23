using System.Threading;
using System.Threading.Tasks;

namespace MacacaGames.ViewSystem
{
    public sealed class ViewPageShowContext
    {
        public long RequestId { get; internal set; }
        public ViewPage ViewPage { get; internal set; }
        public bool IsOverlay { get; internal set; }
        public bool IsReplay { get; internal set; }
    }

    /// <summary>
    /// Project-level presentation hook around a ViewPage show operation. Implementations
    /// must not call back into ChangePage/ShowOverlayViewPage for the same request.
    /// </summary>
    public interface IViewPageShowHook
    {
        bool ShouldHandle(ViewPageShowContext context);
        Task BeforePrepareAsync(ViewPageShowContext context, CancellationToken cancellationToken);
        Task AfterReadyAsync(ViewPageShowContext context, CancellationToken cancellationToken);
        void OnAborted(ViewPageShowContext context);
    }

    /// <summary>
    /// Optional execution policy for a ViewPage show hook.
    /// Lower order values execute first. A timeout at or below zero waits indefinitely,
    /// which is intended for user-confirmed downloads whose duration can't be predicted.
    /// </summary>
    public interface IViewPageShowHookExecutionPolicy
    {
        int Order { get; }
        float TimeoutSeconds { get; }
    }
}
