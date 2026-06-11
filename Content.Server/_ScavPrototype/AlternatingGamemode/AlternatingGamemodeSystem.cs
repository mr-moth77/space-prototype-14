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
    private bool _isNextForced = true; // First round is always greenshift

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
            // Suppress vote.auto_preset if it exists
            if (_cfg.IsCVarRegistered("vote.auto_preset"))
            {
                _cfg.SetCVar("vote.auto_preset", false);
            }
            // Suppress vote.preset_enabled as requested
            if (_cfg.IsCVarRegistered("vote.preset_enabled") && _cfg.GetCVar<bool>("vote.preset_enabled"))
            {
                _cfg.SetCVar("vote.preset_enabled", false);
            }
            
            // If we are currently in the lobby, we might need to trigger the cycle immediately.
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

        // If the previous round actually started successfully, advance the cycle
        if (_roundActuallyStarted)
        {
            _isNextForced = !_isNextForced;
        }
        // If it didn't start successfully (e.g. immediate restart due to lack of players for a voted mode),
        // we do NOT advance the cycle, so the vote will happen again.

        _roundActuallyStarted = false;

        // The RunLevel is already PreRoundLobby during RoundRestartCleanupEvent in GameTicker.RestartRound().
        if (_gameTicker.RunLevel == GameRunLevel.PreRoundLobby)
        {
            ProcessLobbyCycle();
        }
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        // We handle lobby cycle in OnRoundRestartCleanup and Initialize now.
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
            // Start the preset vote
            _voteManager.CreateStandardVote(null, Shared.Voting.StandardVoteType.Preset);
        }
    }
}
