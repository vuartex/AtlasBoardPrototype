using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Makes AtlasBoard's local/private gameplay genuinely usable when no online
/// Atlas identity exists.
///
/// - Startup identity failure no longer forces the user to manually press
///   Continue just to play locally.
/// - PRIVATE TABLE routes to the existing local lobby when the client has no
///   authenticated online identity, so LocalHuman/Bot matches need no server.
/// - Online/Public/Store/Career authority is NOT faked. Those paths continue
///   to require their real online services.
///
/// This component does not grant XP, wins, achievements, wallet currency or
/// cloud match history for offline play.
/// </summary>
public sealed class AtlasBoardOfflineModeBootstrap : MonoBehaviour
{
    private static AtlasBoardOfflineModeBootstrap instance;
    private bool offlineStartupPending;
    private bool offlineStartupActivated;
    private bool privateLocalRouteLogged;
    private float nextScanAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject host =
            new GameObject("AtlasBoard_Offline_Mode_Bootstrap");

        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardOfflineModeBootstrap>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        AtlasBoardFrontendErrorCodes.PublicCodeRaised -= HandlePublicCode;
        AtlasBoardFrontendErrorCodes.PublicCodeRaised += HandlePublicCode;
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        AtlasBoardFrontendErrorCodes.PublicCodeRaised -= HandlePublicCode;
        instance = null;
    }

    private void HandlePublicCodeRaised(
        string code,
        string message)
    {
        if (code == AtlasBoardFrontendErrorCodes.SessionPreparationFailed ||
            code == AtlasBoardFrontendErrorCodes.RequestTimedOut)
        {
            offlineStartupPending = true;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScanAt)
            return;

        nextScanAt = Time.unscaledTime + 0.10f;

        bool identityReady = HasOnlineIdentity();

        if (identityReady)
        {
            if (offlineStartupActivated)
            {
                offlineStartupActivated = false;
                RestoreConnectivityChecks();
                Debug.Log(
                    "ATB_SESSION_MODE online_identity_restored=true");
            }

            return;
        }

        if (offlineStartupPending)
            ActivateOfflineStartup();

        RoutePrivateTableToLocalLobby();
    }

    private static bool HasOnlineIdentity()
    {
        AtlasBoardLobbyRuntimeBridge bridge =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardLobbyRuntimeBridge>(
                    FindObjectsInactive.Include);

        return bridge != null &&
               !string.IsNullOrWhiteSpace(bridge.CurrentAccountId);
    }

    private void ActivateOfflineStartup()
    {
        AtlasBoardLoadingExperience loading =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardLoadingExperience>(
                    FindObjectsInactive.Include);

        if (loading == null)
            return;

        offlineStartupPending = false;
        offlineStartupActivated = true;

        InvokeNoArgs(
            loading,
            "ContinueWithoutBlocking");

        SetPrivateField(
            loading,
            "startupComplete",
            true);

        // Offline/private-local mode is intentional. Do not immediately turn
        // the absence of internet into a second startup toast.
        SetPrivateField(
            loading,
            "nextConnectivityCheckAt",
            float.PositiveInfinity);

        Debug.Log(
            "ATB_SESSION_MODE mode=LocalOffline " +
            "startup_nonblocking=true rewards=false cloud_history=false");
    }

    private static void RestoreConnectivityChecks()
    {
        AtlasBoardLoadingExperience loading =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardLoadingExperience>(
                    FindObjectsInactive.Include);

        if (loading == null)
            return;

        SetPrivateField(
            loading,
            "nextConnectivityCheckAt",
            Time.unscaledTime);
    }

    private void RoutePrivateTableToLocalLobby()
    {
        AtlasBoardPrivateLobbyUIController privateLobby =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardPrivateLobbyUIController>(
                    FindObjectsInactive.Include);

        AtlasBoardMainMenuController mainMenu =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardMainMenuController>(
                    FindObjectsInactive.Include);

        if (privateLobby == null ||
            mainMenu == null ||
            !PrivateGameObjectIsActive(
                privateLobby,
                "roomEntryOverlay"))
        {
            return;
        }

        // The online private room chooser was opened, but there is no online
        // identity. Route immediately to the existing local lobby instead of
        // attempting Cloud Functions. This keeps LocalHuman + Bot gameplay
        // completely offline and deliberately non-rewarded.
        InvokeNoArgs(
            privateLobby,
            "CancelRoomEntry");

        mainMenu.OpenPrivateLobbyAfterRoomChoice();

        if (!privateLocalRouteLogged)
        {
            privateLocalRouteLogged = true;
            Debug.Log(
                "ATB_SESSION_MODE mode=LocalOffline source=PRIVATE_TABLE " +
                "transport=local_only rewards=false anti_cheat=not_required");
        }
    }

    private static bool PrivateGameObjectIsActive(
        object owner,
        string fieldName)
    {
        if (owner == null)
            return false;

        FieldInfo field =
            owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

        GameObject value =
            field == null
                ? null
                : field.GetValue(owner) as GameObject;

        return value != null && value.activeInHierarchy;
    }

    private static bool InvokeNoArgs(
        object owner,
        string methodName)
    {
        if (owner == null)
            return false;

        MethodInfo method =
            owner.GetType().GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

        if (method == null)
            return false;

        try
        {
            method.Invoke(owner, null);
            return true;
        }
        catch (Exception error)
        {
            Debug.LogWarning(
                "ATB_OFFLINE_ROUTE_FAILED method=" +
                methodName +
                " type=" + error.GetType().Name);
            return false;
        }
    }

    private static void SetPrivateField(
        object owner,
        string fieldName,
        object value)
    {
        if (owner == null)
            return;

        FieldInfo field =
            owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

        if (field == null)
            return;

        try
        {
            field.SetValue(owner, value);
        }
        catch (Exception error)
        {
            Debug.LogWarning(
                "ATB_OFFLINE_FIELD_WRITE_FAILED field=" +
                fieldName +
                " type=" + error.GetType().Name);
        }
    }
}
