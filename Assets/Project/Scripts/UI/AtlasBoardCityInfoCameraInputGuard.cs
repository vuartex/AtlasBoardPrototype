using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class AtlasBoardCityInfoCameraInputGuard :
    MonoBehaviour
{
    private AtlasBoardCityInformationPanel cityInformationPanel;
    private BoardCameraController boardCameraController;

    private bool cameraDisabledByGuard;

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
                AtlasBoardCityInfoCameraInputGuard>() != null)
        {
            return;
        }

        new GameObject(
                "AtlasBoard_CityInfoCameraInputGuard")
            .AddComponent<
                AtlasBoardCityInfoCameraInputGuard>();
    }

    private void Update()
    {
        ResolveReferences();

        bool shouldBlock =
            IsPointerOverCityInformation();

        if (shouldBlock)
        {
            DisableCameraInput();
        }
        else
        {
            RestoreCameraInput();
        }
    }

    private void OnDisable()
    {
        RestoreCameraInput();
    }

    private void OnDestroy()
    {
        RestoreCameraInput();
    }

    private void ResolveReferences()
    {
        if (cityInformationPanel == null)
        {
            cityInformationPanel =
                UnityEngine.Object.FindAnyObjectByType<
                    AtlasBoardCityInformationPanel>();
        }

        if (boardCameraController == null)
        {
            boardCameraController =
                UnityEngine.Object.FindAnyObjectByType<
                    BoardCameraController>(
                        FindObjectsInactive.Include);
        }
    }

    private bool IsPointerOverCityInformation()
    {
        if (cityInformationPanel == null ||
            !cityInformationPanel.IsOpen ||
            Mouse.current == null ||
            EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointer =
            new PointerEventData(
                EventSystem.current)
            {
                position =
                    Mouse.current.position
                        .ReadValue()
            };

        List<RaycastResult> results =
            new List<RaycastResult>();

        EventSystem.current.RaycastAll(
            pointer,
            results);

        Transform cityRoot =
            cityInformationPanel.transform;

        foreach (RaycastResult result
                 in results)
        {
            if (result.gameObject == null)
            {
                continue;
            }

            Transform hit =
                result.gameObject.transform;

            if (hit == cityRoot ||
                hit.IsChildOf(
                    cityRoot))
            {
                return true;
            }
        }

        return false;
    }

    private void DisableCameraInput()
    {
        if (cameraDisabledByGuard ||
            boardCameraController == null ||
            !boardCameraController.enabled)
        {
            return;
        }

        boardCameraController.enabled =
            false;

        cameraDisabledByGuard =
            true;
    }

    private void RestoreCameraInput()
    {
        if (!cameraDisabledByGuard)
        {
            return;
        }

        if (boardCameraController != null)
        {
            boardCameraController.enabled =
                true;
        }

        cameraDisabledByGuard =
            false;
    }
}
