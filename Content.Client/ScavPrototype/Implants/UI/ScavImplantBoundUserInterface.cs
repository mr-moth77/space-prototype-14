using Content.Shared.ScavPrototype.Implants;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.ScavPrototype.Implants.UI;

[UsedImplicitly]
public sealed class ScavImplantBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ScavImplantWindow? _window;

    public ScavImplantBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ScavImplantWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null)
            return;

        if (state is ScavImplantUpdateMessage updateMsg)
        {
            _window.UpdateState(updateMsg);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Dispose();
    }
}
