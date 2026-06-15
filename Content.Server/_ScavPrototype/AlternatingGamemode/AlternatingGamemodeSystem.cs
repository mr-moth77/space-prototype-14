using Content.Server.GameTicking;
using Content.Server.Voting.Managers;
using Content.Shared.GameTicking;
using Content.Shared._ScavPrototype.AlternatingGamemode;
using Robust.Shared.Configuration;

namespace Content.Server._ScavPrototype.AlternatingGamemode;

public sealed class AlternatingGamemodeSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IVoteManager _voteManager = default!;

    private bool _roundActuallyStarted = false;
    private bool _isNextForced = true;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);

        _cfg.OnValueChanged(AlternatingGamemodeCVars.AlternatingGamemodeEnabled, OnEnabledChanged, true);
    }

    private void OnEnabledChanged(bool enabled)
    {
        if (enabled)
        {
            if (_cfg.IsCVarRegistered("vote.auto_preset"))
            {
                _cfg.SetCVar("vote.auto_preset", false);
            }
            if (_cfg.IsCVarRegistered("vote.preset_enabled") && _cfg.GetCVar<bool>("vote.preset_enabled"))
            {
                _cfg.SetCVar("vote.preset_enabled", false);
            }
            
            if (_gameTicker.RunLevel == GameRunLevel.PreRoundLobby)
            {
                ProcessLobbyCycle();
            }
        }
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        if (!_cfg.GetCVar(AlternatingGamemodeCVars.AlternatingGamemodeEnabled))
            return;

        _roundActuallyStarted = true;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        if (!_cfg.GetCVar(AlternatingGamemodeCVars.AlternatingGamemodeEnabled))
        {
            _roundActuallyStarted = false;
            return;
        }

        if (_roundActuallyStarted)
        {
            _isNextForced = !_isNextForced;
        }

        _roundActuallyStarted = false;

        if (_gameTicker.RunLevel == GameRunLevel.PreRoundLobby)
        {
            ProcessLobbyCycle();
        }
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
    
    }

    private void ProcessLobbyCycle()
    {
        if (_isNextForced)
        {
            var preset = _cfg.GetCVar(AlternatingGamemodeCVars.AlternatingGamemodePreset);
            _gameTicker.SetGamePreset(preset);
        }
        else
        {
            _voteManager.CreateStandardVote(null, Shared.Voting.StandardVoteType.Preset);
        }
    }
}
