using System.Threading;
using System.Threading.Tasks;

namespace MacacaGames.ViewSystem
{
    /// <summary>
    /// Optional readiness source for asynchronous visual/data work which is not covered by
    /// ViewElement.OnBeforeShowAsync. A page presentation hook can remain covered until all
    /// sources finish or the page readiness timeout is reached.
    /// </summary>
    public interface IViewPageReadySource
    {
        Task WaitUntilReadyAsync(CancellationToken cancellationToken);
    }
}
