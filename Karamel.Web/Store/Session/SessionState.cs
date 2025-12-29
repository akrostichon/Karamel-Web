using Fluxor;
using Karamel.Web.Models;

namespace Karamel.Web.Store.Session;

[FeatureState]
public record SessionState
{
    public Models.Session? CurrentSession { get; init; }
    public bool IsInitialized { get; init; }
}
