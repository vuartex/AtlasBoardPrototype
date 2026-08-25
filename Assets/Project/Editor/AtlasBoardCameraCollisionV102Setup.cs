#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardCameraCollisionV102Setup
{
    private const string ObstacleLayerName =
        "CameraObstacle";

    [MenuItem(
        "Atlas Board/Camera/Rebuild Selected Obstacles - Accurate Mesh v1.0.2")]
    public static void RebuildSelectedAccurate()
    {
        int layer =
            LayerMask.NameToLayer(
                ObstacleLayerName);

        if (layer < 0)
        {
            Debug.LogError(
                "CameraObstacle layer does not exist. " +
                "Run Install Camera Collision v1 first.");

            return;
        }

        GameObject[] selected =
            Selection.gameObjects;

        if (selected == null ||
            selected.Length == 0)
        {
            Debug.LogWarning(
                "Select the building/prop root that the camera passes through.");

            return;
        }

        HashSet<GameObject> processed =
            new HashSet<GameObject>();

        int meshCollidersAdded = 0;
        int generatedBoxesRemoved = 0;
        int generatedMeshesRemoved = 0;
        int existingCollidersKept = 0;

        foreach (GameObject selectedRoot
                 in selected)
        {
            if (selectedRoot == null)
            {
                continue;
            }

            MeshRenderer[] renderers =
                selectedRoot.GetComponentsInChildren<
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

                if (!processed.Add(target))
                {
                    continue;
                }

                MeshFilter filter =
                    target.GetComponent<
                        MeshFilter>();

                if (filter == null ||
                    filter.sharedMesh == null)
                {
                    continue;
                }

                Undo.RecordObject(
                    target,
                    "Rebuild Accurate Camera Obstacle");

                target.layer = layer;

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

                // Remove only colliders that AtlasBoard generated previously.
                if (marker.ColliderAddedByAtlasBoard)
                {
                    BoxCollider generatedBox =
                        target.GetComponent<
                            BoxCollider>();

                    if (generatedBox != null)
                    {
                        Undo.DestroyObjectImmediate(
                            generatedBox);

                        generatedBoxesRemoved++;
                    }

                    MeshCollider generatedMesh =
                        target.GetComponent<
                            MeshCollider>();

                    if (generatedMesh != null)
                    {
                        Undo.DestroyObjectImmediate(
                            generatedMesh);

                        generatedMeshesRemoved++;
                    }
                }

                Collider existingCollider =
                    target.GetComponent<
                        Collider>();

                if (existingCollider != null &&
                    !marker.ColliderAddedByAtlasBoard)
                {
                    Undo.RecordObject(
                        existingCollider,
                        "Enable Camera Obstacle Collider");

                    existingCollider.enabled =
                        true;

                    marker.EditorSetColliderAdded(
                        false,
                        CameraCollisionObstacle
                            .GeneratedColliderKind.None);

                    EditorUtility.SetDirty(
                        marker);

                    existingCollidersKept++;
                    continue;
                }

                MeshCollider meshCollider =
                    Undo.AddComponent<
                        MeshCollider>(
                            target);

                meshCollider.sharedMesh =
                    filter.sharedMesh;

                meshCollider.convex = false;
                meshCollider.isTrigger = false;
                meshCollider.enabled = true;

                marker.EditorSetColliderAdded(
                    true,
                    CameraCollisionObstacle
                        .GeneratedColliderKind.Mesh);

                EditorUtility.SetDirty(
                    marker);

                meshCollidersAdded++;
            }
        }

        Physics.SyncTransforms();

        EditorSceneManager.MarkAllScenesDirty();

        Debug.Log(
            "Accurate camera obstacles rebuilt. " +
            $"MeshColliders added={meshCollidersAdded}, " +
            $"old generated BoxColliders removed={generatedBoxesRemoved}, " +
            $"old generated MeshColliders removed={generatedMeshesRemoved}, " +
            $"manual/existing colliders preserved={existingCollidersKept}. " +
            "Save the scene, then test the same camera angle.");
    }

    [MenuItem(
        "Atlas Board/Camera/Debug/Log Main Camera Collision State")]
    public static void LogCameraCollisionState()
    {
        Camera camera =
            Camera.main ??
            Object.FindAnyObjectByType<
                Camera>();

        if (camera == null)
        {
            Debug.LogWarning(
                "No Camera found.");

            return;
        }

        BoardCameraCollision collision =
            camera.GetComponent<
                BoardCameraCollision>();

        if (collision == null)
        {
            Debug.LogWarning(
                "BoardCameraCollision is NOT attached to the camera.");

            return;
        }

        int layer =
            LayerMask.NameToLayer(
                ObstacleLayerName);

        CameraCollisionObstacle[] obstacles =
            Object.FindObjectsByType<
                CameraCollisionObstacle>(
                    FindObjectsInactive.Include);

        Debug.Log(
            "Camera Collision diagnostics: " +
            $"component enabled={collision.enabled}, " +
            $"CollisionActive={collision.CollisionActive}, " +
            $"CameraObstacle layer index={layer}, " +
            $"marked obstacle mesh objects={obstacles.Length}.");
    }
}
#endif
