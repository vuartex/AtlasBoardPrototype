using System.Reflection;
using UnityEngine;

[DefaultExecutionOrder(14000)]
[DisallowMultipleComponent]
public sealed class AtlasBoardTabletWorldInteractionGuard :
    MonoBehaviour
{
    private TabletUIManager tabletManager;
    private AtlasBoardCityInformationPanel cityInformationPanel;
    private bool cityInputDisabledByGuard;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (UnityEngine.Object.FindAnyObjectByType<
                BoardPath>() == null)
        {
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardTabletWorldInteractionGuard>() != null)
        {
            return;
        }

        new GameObject(
                "AtlasBoard_TabletWorldInteractionGuard")
            .AddComponent<
                AtlasBoardTabletWorldInteractionGuard>();
    }

    private void Update()
    {
        RefreshBlockState();
    }

    private void LateUpdate()
    {
        // TabletUIManager determines shell visibility in LateUpdate.
        // Re-check after it so a tablet opened this frame cannot leave the
        // board City click layer visible for one rendered frame.
        RefreshBlockState();
    }

    private void OnDisable()
    {
        RestoreCityInputIfNeeded();
    }

    private void OnDestroy()
    {
        RestoreCityInputIfNeeded();
    }

    private void RefreshBlockState()
    {
        ResolveReferences();

        if (cityInformationPanel == null)
        {
            return;
        }

        bool tabletOpen =
            IsTabletOpen();

        if (tabletOpen)
        {
            cityInformationPanel.Hide();

            if (cityInformationPanel.enabled)
            {
                cityInformationPanel.enabled =
                    false;

                cityInputDisabledByGuard =
                    true;
            }

            return;
        }

        RestoreCityInputIfNeeded();
    }

    private void ResolveReferences()
    {
        if (tabletManager == null)
        {
            tabletManager =
                UnityEngine.Object.FindAnyObjectByType<
                    TabletUIManager>();
        }

        if (cityInformationPanel == null)
        {
            cityInformationPanel =
                UnityEngine.Object.FindAnyObjectByType<
                    AtlasBoardCityInformationPanel>();
        }
    }

    private bool IsTabletOpen()
    {
        if (tabletManager == null)
        {
            return false;
        }

        GameObject tabletRoot =
            ReadPrivateField<GameObject>(
                tabletManager,
                "tabletRoot");

        GameObject currentPanel =
            ReadPrivateField<GameObject>(
                tabletManager,
                "currentPanel");

        return
            (currentPanel != null &&
             currentPanel.activeSelf) ||
            (tabletRoot != null &&
             tabletRoot.activeSelf);
    }

    private void RestoreCityInputIfNeeded()
    {
        if (!cityInputDisabledByGuard)
        {
            return;
        }

        if (cityInformationPanel != null)
        {
            cityInformationPanel.enabled =
                true;
        }

        cityInputDisabledByGuard =
            false;
    }

    private static T ReadPrivateField<T>(
        object target,
        string fieldName)
        where T : class
    {
        if (target == null)
        {
            return null;
        }

        FieldInfo field =
            target.GetType()
                .GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

        return field != null
            ? field.GetValue(
                target) as T
            : null;
    }
}
