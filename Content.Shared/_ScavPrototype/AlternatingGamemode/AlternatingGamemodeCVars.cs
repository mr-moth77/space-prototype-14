using Robust.Shared.Configuration;

namespace Content.Shared._ScavPrototype.AlternatingGamemode;

[CVarDefs]
public sealed class AlternatingGamemodeCVars
{
    /// <summary>
    /// Enables or disables the alternating gamemode system.
    /// </summary>
    public static readonly CVarDef<bool> AlternatingGamemodeEnabled =
        CVarDef.Create("game.alternating_gamemode_enabled", false, CVar.SERVERONLY);

    /// <summary>
    /// The preset to force every other round.
    /// </summary>
    public static readonly CVarDef<string> AlternatingGamemodePreset =
        CVarDef.Create("game.alternating_gamemode_preset", "greenshift", CVar.SERVERONLY);
}
