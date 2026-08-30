using UnityEngine;

public class AtlasBoardOnlineFoundation : MonoBehaviour
{
    [Header("Cross-platform foundation")]
    [SerializeField]
    private bool crossplayEnabled = true;

    [SerializeField]
    private AtlasSessionAuthorityMode authorityMode =
        AtlasSessionAuthorityMode.HostAuthoritative;

    [Header("Reconnect / AFK policy")]
    [SerializeField, Min(30)]
    private int reconnectWindowSeconds =
        AtlasOnlineDefaults.ReconnectWindowSeconds;

    [SerializeField, Min(1f)]
    private float humanRollTimeoutSeconds =
        AtlasOnlineDefaults.HumanRollTimeoutSeconds;

    [SerializeField, Min(1)]
    private int afkConsecutiveAutoRollLimit =
        AtlasOnlineDefaults.AfkConsecutiveAutoRollLimit;

    [Header("Version compatibility")]
    [SerializeField, Min(1)]
    private int protocolVersion =
        AtlasOnlineDefaults.ProtocolVersion;

    [SerializeField, Min(1)]
    private int rulesVersion =
        AtlasOnlineDefaults.RulesVersion;

    private AtlasSessionStateMachine sessionState;

    public bool CrossplayEnabled => crossplayEnabled;
    public AtlasSessionAuthorityMode AuthorityMode => authorityMode;
    public int ReconnectWindowSeconds => reconnectWindowSeconds;
    public float HumanRollTimeoutSeconds => humanRollTimeoutSeconds;
    public int AfkConsecutiveAutoRollLimit => afkConsecutiveAutoRollLimit;
    public int ProtocolVersion => protocolVersion;
    public int RulesVersion => rulesVersion;
    public AtlasSessionStateMachine SessionState => sessionState;

    private void Awake()
    {
        crossplayEnabled = AtlasCrossplayPreference.Enabled;
    }

    public AtlasSessionStateMachine CreateLocalStateModel(
        AtlasRoomDescriptor room)
    {
        sessionState = new AtlasSessionStateMachine
        {
            ReconnectWindowSeconds = reconnectWindowSeconds,
            AfkAutoRollLimit = afkConsecutiveAutoRollLimit
        };

        room.CrossplayMode = crossplayEnabled
            ? AtlasCrossplayMode.CrossPlatform
            : AtlasCrossplayMode.SamePlatformOnly;
        room.AuthorityMode = authorityMode;
        room.ProtocolVersion = protocolVersion;
        room.RulesVersion = rulesVersion;

        sessionState.Initialize(room);
        return sessionState;
    }

#if UNITY_EDITOR
    public void EditorConfigureDefaults()
    {
        crossplayEnabled = true;
        authorityMode = AtlasSessionAuthorityMode.HostAuthoritative;
        reconnectWindowSeconds = AtlasOnlineDefaults.ReconnectWindowSeconds;
        humanRollTimeoutSeconds = AtlasOnlineDefaults.HumanRollTimeoutSeconds;
        afkConsecutiveAutoRollLimit = AtlasOnlineDefaults.AfkConsecutiveAutoRollLimit;
        protocolVersion = AtlasOnlineDefaults.ProtocolVersion;
        rulesVersion = AtlasOnlineDefaults.RulesVersion;
    }

    public void EditorConfigureTurnPolicy(
        float newHumanRollTimeoutSeconds,
        int newAfkLimit)
    {
        humanRollTimeoutSeconds =
            Mathf.Max(1f, newHumanRollTimeoutSeconds);

        afkConsecutiveAutoRollLimit =
            Mathf.Max(1, newAfkLimit);
    }
#endif
}
