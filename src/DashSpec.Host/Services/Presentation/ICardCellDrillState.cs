using DashSpec.Core.Runtime;

namespace DashSpec.Host.Services.Presentation;

public interface ICardCellDrillState
{
    CardCellDrillOverlay? Get(string cardId);

    void Set(string cardId, CardCellDrillOverlay overlay);

    void Clear(string cardId);
}
