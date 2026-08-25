#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardCameraCollisionV1Setup
{
    private const string ObstacleLayerName =
        "CameraObstacle";

    [MenuItem(
        "Atlas Board/Camera/Install Camera Collision v1")]
    public static void InstallCameraCollision()
    {
        int layer =
            EnsureLayer(
                ObstacleLayerName);

        if (layer < 0)
        {
            Debug.LogError(
                "Could not create/find CameraObstacle layer. " +
                "No free User Layer slot was available.");

            return;
        }

        Camera mainCamera =
            Camera.main;

        if (mainCamera == null)
        {
            mainCamera =
                Object.FindAnyObjectByType<
                    Camera>();
        }

        if (mainCamera == null)
        {
            Debug.LogError(
                "Main Camera was not found.");

            return;
        }

        GameObject boardRoot =
            GameObject.Find(
                "BoardRoot");

        if (boardRoot == null)
        {
            Debug.LogError(
                "BoardRoot was not found.");

            return;
        }

        BoardCameraCollision collision =
            mainCamera.GetComponent<
                BoardCameraCollision>();

        if (collision == null)
        {
            collision =
                Undo.AddComponent<
                    BoardCameraCollision>(
                        mainCamera.gameObject);
        }

        collision.EditorConfigure(
            boardRoot.transform,
            1 << layer);

        // CameraObstacle is query-only geometry for camera protection.
        // Ignore normal physical collisions with all layers.
        for (int otherLayer = 0;
             otherLayer < 32;
             otherLayer++)
        {
            Physics.IgnoreLayerCollision(
                layer,
                otherLayer,
                true);
        }

        EditorUtility.SetDirty(
            collision);

        if (mainCamera.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                mainCamera.gameObject.scene);
        }

        Selection.activeGameObject =
            mainCamera.gameObject;

        Debug.Log(
            "Camera Collision v1 installed on Main Camera. " +
            "Next: select building/tree/large prop objects and run " +
            "Atlas Board > Camera > Mark Selected as Camera Obstacle.");
    }

    [MenuItem(
        "Atlas Board/Camera/Mark Selected as Camera Obstacle")]
    public static void MarkSelectedAsCameraObstacle()
    {
        int layer =
            EnsureLayer(
                ObstacleLayerName);

        if (layer < 0)
        {
            Debug.LogError(
                "CameraObstacle layer is unavailable.");

            return;
        }

        GameObject[] selected =
            Selection.gameObjects;

        if (selected == null ||
            selected.Length == 0)
        {
            Debug.LogWarning(
                "Select one or more environment props/buildings first.");

            return;
        }

        HashSet<GameObject> processed =
            new HashSet<GameObject>();

        int rendererObjects = 0;
        int collidersAdded = 0;
        int collidersReused = 0;

        foreach (GameObject selectedRoot
                 in selected)
        {
            if (selectedRoot == null)
            {
                continue;
            }

            MeshRenderer[] renderers =
                selectedRoot
                    .GetComponentsInChildren<
                        MeshRenderer>(true);

            foreach (MeshRenderer renderer
                     in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                GameObject target =
                    renderer.gameObject;

                if (!processed.Add(
                        target))
                {
                    continue;
                }

                Undo.RecordObject(
                    target,
                    "Mark Camera Obstacle");

                target.layer =
                    layer;

                CameraCollisionObstacle marker =
                    target.GetComponent<
                        CameraCollisionObstacle>();

                if (marker == null)
                {
                    marker =
                        Undo.AddComponent<
                            CameraCollisionObstacle>(
                                target);
                }

                Collider collider =
                    target.GetComponent<
                        Collider>();

                bool addedByTool = false;

                if (collider == null)
                {
                    MeshFilter filter =
                        target.GetComponent<
                            MeshFilter>();

                    if (filter != null &&
                        filter.sharedMesh != null)
                    {
                        BoxCollider box =
                            Undo.AddComponent<
                                BoxCollider>(
                                    target);

                        Bounds bounds =
                            filter.sharedMesh.bounds;

                        box.center =
                            bounds.center;

                        box.size =
                            bounds.size;

                        collider =
                            box;

                        addedByTool = true;
                        collidersAdded++;
                    }
                }

                if (collider != null)
                {
                    Undo.RecordObject(
                        collider,
                        "Enable Camera Obstacle Collider");

                    collider.enabled =
                        true;

                    if (!addedByTool)
                    {
                        collidersReused++;
                    }
                }

                marker.EditorSetColliderAdded(
                    addedByTool);

                EditorUtility.SetDirty(
                    marker);

                rendererObjects++;
            }
        }

        Physics.SyncTransforms();

        if (rendererObjects > 0)
        {
            EditorSceneManager.MarkAllScenesDirty();
        }

        Debug.Log(
            $"Camera obstacles marked. " +
            $"Renderer objects={rendererObjects}, " +
            $"BoxColliders added={collidersAdded}, " +
            $"existing colliders reused={collidersReused}. " +
            "Use this on buildings, large trees, statues and other objects " +
            "the camera should not pass through.");
    }

    [MenuItem(
        "Atlas Board/Camera/Unmark Selected Camera Obstacle")]
    public static void UnmarkSelectedCameraObstacle()
    {
        GameObject[] selected =
            Selection.gameObjects;

        if (selected == null ||
            selected.Length == 0)
        {
            Debug.LogWarning(
                "Select one or more marked obstacle objects first.");

            return;
        }

        int changed = 0;

        foreach (GameObject selectedRoot
                 in selected)
        {
            if (selectedRoot == null)
            {
                continue;
            }

            CameraCollisionObstacle[] markers =
                selectedRoot
                    .GetComponentsInChildren<
                        CameraCollisionObstacle>(true);

            foreach (CameraCollisionObstacle marker
                     in markers)
            {
                if (marker == null)
                {
                    continue;
                }

                GameObject target =
                    marker.gameObject;

                Undo.RecordObject(
                    target,
                    "Unmark Camera Obstacle");

                target.layer = 0;

                if (marker.ColliderAddedByAtlasBoard)
                {
                    BoxCollider box =
                        target.GetComponent<
                            BoxCollider>();

                    if (box != null)
                    {
                        Undo.DestroyObjectImmediate(
                            box);
                    }
                }

                Undo.DestroyObjectImmediate(
                    marker);

                changed++;
            }
        }

        if (changed > 0)
        {
            EditorSceneManager.MarkAllScenesDirty();
        }

        Debug.Log(
            $"Camera obstacle marks removed: {changed}.");
    }

    private static int EnsureLayer(
        string layerName)
    {
        int existing =
            LayerMask.NameToLayer(
                layerName);

        if (existing >= 0)
        {
            return existing;
        }

        SerializedObject tagManager =
            new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath(
                    "ProjectSettings/TagManager.asset")[0]);

        SerializedProperty layers =
            tagManager.FindProperty(
                "layers");

        if (layers == null)
        {
            return -1;
        }

        for (int i = 8;
             i < 32;
             i++)
        {
            SerializedProperty element =
                layers.GetArrayElementAtIndex(
                    i);

            if (!string.IsNullOrEmpty(
                    element.stringValue))
            {
                continue;
            }

            element.stringValue =
                layerName;

            tagManager.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();

            return i;
        }

        return -1;
    }
}
#endif
