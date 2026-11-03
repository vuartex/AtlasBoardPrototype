#if UNITY_EDITOR
/// <summary>
/// Retired one-off Account QA build menu.
/// AtlasBoard's existing Two-Client Guest builder and Live Spark launcher
/// remain the only standard standalone Guest QA build/launch path.
/// Keep this compatibility class as a harmless placeholder so replacing a
/// previously-installed package removes its obsolete "AtlasBoard" menu.
/// No MenuItem and no second build output are registered here.
/// </summary>
public static class AtlasBoardAccountQaWindowsBuilder
{
}
#endif
