using System;
using System.Reflection;
using System.Threading;
using UnityEngine;

/// <summary>
/// Device-only landscape lock and Android 13+ system Back callback.
/// Uses existing AtlasBoard UI entrypoints; does not change desktop input,
/// network transport, production credentials or online lobby authority.
/// </summary>
public sealed class AtlasBoardMobilePlatformQA : MonoBehaviour
{
    private static AtlasBoardMobilePlatformQA instance;
    private float nextOrientationCheck;

#if UNITY_ANDROID && !UNITY_EDITOR
    private static int pendingSystemBack;
    private static AndroidJavaObject backDispatcher;
    private static AndroidBackCallback backCallback;
    private bool callbackRegistered;
    private static int nativeRegistrationState;
    private bool nativeRegistrationReported;

    [UnityEngine.Scripting.Preserve]
    private sealed class AndroidBackCallback : AndroidJavaProxy
    {
        public AndroidBackCallback() : base("android.window.OnBackInvokedCallback") { }
        [UnityEngine.Scripting.Preserve]
        public void onBackInvoked()
        {
            // Android calls this on its UI thread; Unity objects are only
            // accessed in Update on the game thread.
            Interlocked.Exchange(ref pendingSystemBack, 1);
        }
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        LockLandscape();
        if (instance == null)
        {
            GameObject host = new GameObject("AtlasBoard_Mobile_Platform_QA");
            DontDestroyOnLoad(host);
            host.AddComponent<AtlasBoardMobilePlatformQA>();
        }
#endif
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        LockLandscape();
        Debug.Log("ATB_MOBILE_LANDSCAPE_LOCK orientation=LandscapeLeft device=" +
            SystemInfo.deviceModel + " resolution=" + Screen.width + "x" + Screen.height);
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        RegisterAndroidBack();
#endif
    }

    private static void LockLandscape()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeRight = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
#endif
    }

    private void Update()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        if (Time.unscaledTime >= nextOrientationCheck)
        {
            nextOrientationCheck = Time.unscaledTime + 2f;
            if (Screen.orientation != ScreenOrientation.LandscapeLeft)
                LockLandscape();
        }
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!nativeRegistrationReported)
        {
            int result = Interlocked.CompareExchange(ref nativeRegistrationState, 0, 0);
            if (result != 0)
            {
                nativeRegistrationReported = true;
                callbackRegistered = result == 1;
                Debug.Log(result == 1 ? "ATB_ANDROID_BACK_NATIVE_REGISTERED" :
                    "ATB_ANDROID_BACK_NATIVE_UNAVAILABLE registration_failed fallback=qa_back_button");
            }
        }
        if (Interlocked.Exchange(ref pendingSystemBack, 0) != 0)
        {
            if (!HandleAndroidBack())
                Debug.LogWarning("ATB_ANDROID_BACK_UNHANDLED keep_current_screen");
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void RegisterAndroidBack()
    {
        try
        {
            using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                int api = version.GetStatic<int>("SDK_INT");
                if (api < 33)
                {
                    Debug.Log("ATB_ANDROID_BACK_NATIVE_UNAVAILABLE api=" + api +
                        " fallback=existing_escape_handlers");
                    return;
                }
            }
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity == null)
                    throw new InvalidOperationException("currentActivity unavailable");
                backDispatcher = activity.Call<AndroidJavaObject>("getOnBackInvokedDispatcher");
                if (backDispatcher == null)
                    throw new InvalidOperationException("back dispatcher unavailable");
                backCallback = new AndroidBackCallback();
                AndroidJavaObject dispatcher = backDispatcher;
                AndroidBackCallback callback = backCallback;
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        dispatcher.Call("registerOnBackInvokedCallback", 0, callback);
                        Interlocked.Exchange(ref nativeRegistrationState, 1);
                    }
                    catch (Exception)
                    {
                        Interlocked.Exchange(ref nativeRegistrationState, -1);
                    }
                }));
            }
        }
        catch (Exception error)
        {
            callbackRegistered = false;
            Debug.LogWarning("ATB_ANDROID_BACK_NATIVE_UNAVAILABLE type=" +
                error.GetType().Name + " fallback=existing_escape_handlers");
        }
    }

    private static T FindScene<T>() where T : Component
    {
        T[] items = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include);
        foreach (T item in items)
            if (item != null && item.gameObject.scene.IsValid())
                return item;
        return null;
    }

    private static bool FieldRootActive(Component owner, string name)
    {
        if (owner == null) return false;
        FieldInfo field = owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        GameObject root = field == null ? null : field.GetValue(owner) as GameObject;
        return root != null && root.activeInHierarchy;
    }

    private static bool CallExisting(Component owner, string methodName)
    {
        if (owner == null) return false;
        MethodInfo method = owner.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, Type.EmptyTypes, null);
        if (method == null) return false;
        try
        {
            method.Invoke(owner, null);
            Debug.Log("ATB_ANDROID_BACK_ROUTE handler=" + methodName);
            return true;
        }
        catch (Exception error)
        {
            Debug.LogWarning("ATB_ANDROID_BACK_ROUTE_FAILED handler=" + methodName +
                " exception=" + error.GetType().Name);
            return false;
        }
    }

    private static bool HandleAndroidBack()
    {
        // Route within the existing UI, never force a scene reload, delete a
        // multiplayer seat or quit while a match is active.
        AtlasBoardPrivateLobbyUIController privateLobby =
            FindScene<AtlasBoardPrivateLobbyUIController>();
        if (FieldRootActive(privateLobby, "gameSettingsOverlay"))
            return CallExisting(privateLobby, "CloseLobbyGameSettings");
        if (FieldRootActive(privateLobby, "roomEntryOverlay"))
            return CallExisting(privateLobby, "CancelRoomEntry");

        AtlasBoardPublicLobbyBrowserController browser =
            FindScene<AtlasBoardPublicLobbyBrowserController>();
        if (FieldRootActive(browser, "joinByCodePromptRoot"))
            return CallExisting(browser, "HideJoinByCodePrompt");
        if (FieldRootActive(browser, "passwordPromptRoot"))
            return CallExisting(browser, "HidePasswordPrompt");
        if (FieldRootActive(browser, "browserRoot"))
            return CallExisting(browser, "BackToMainMenu");

        AtlasBoardLeaveFlowController leave =
            FindScene<AtlasBoardLeaveFlowController>();
        if (FieldRootActive(leave, "leaveConfirmationRoot"))
            return CallExisting(leave, "CancelLeaveMatch");
        if (FieldRootActive(leave, "pauseRoot"))
            return CallExisting(leave, "ResumeMatch");

        AtlasBoardMainMenuController main = FindScene<AtlasBoardMainMenuController>();
        if (FieldRootActive(main, "modalPanel"))
            return CallExisting(main, "CloseModal");
        if (FieldRootActive(main, "lobbyPanel"))
            return CallExisting(main, "ShowMainMenu");

        // Esc-blocked gameplay UI remains owned by its original controls.
        AtlasBoardEscapeBlocker[] blockers =
            UnityEngine.Object.FindObjectsByType<AtlasBoardEscapeBlocker>(
                FindObjectsInactive.Include);
        foreach (AtlasBoardEscapeBlocker blocker in blockers)
        {
            if (blocker != null && blocker.gameObject.activeInHierarchy)
            {
                Debug.Log("ATB_ANDROID_BACK_BLOCKED_BY_ACTIVE_MODAL");
                return true;
            }
        }
        if (leave != null)
            return CallExisting(leave, "OpenPauseMenu");
        return false;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        if (callbackRegistered && backDispatcher != null && backCallback != null)
        {
            try
            {
                using (AndroidJavaClass unityPlayer =
                    new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                    unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    AndroidJavaObject dispatcher = backDispatcher;
                    AndroidBackCallback callback = backCallback;
                    activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                    {
                        try { dispatcher.Call("unregisterOnBackInvokedCallback", callback); }
                        catch (Exception) { }
                    }));
                }
            }
            catch (Exception) { /* Activity teardown must not break exit. */ }
        }
        // Activity owns the native callback lifetime after OnDestroy.
        // Avoid disposing a dispatcher before an asynchronous UI-thread unregister.
        backDispatcher = null;
        backCallback = null;
    }
#endif
}
