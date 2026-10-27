using System;
using UnityEngine;

/// <summary>
/// One board-only visual applier for all maps and players. It does not clone
/// BoardGenerator or EnvironmentThemeManager, does not modify tile colliders,
/// path indices, or map economy, and never writes to sharedMaterial assets.
/// The canonical room snapshot owns the chosen board during an online match.
/// </summary>
public sealed class AtlasBoardBoardThemeVisuals : MonoBehaviour
{
    private static AtlasBoardBoardThemeVisuals instance;
    private static string activeId = "default_board";
    private string lastAppliedId = "";
    private Renderer edge;
    private Renderer center;
    private MaterialPropertyBlock block;
    private string lastLobbyId = "";
    private float nextRendererCheck;
    private float nextSnapshotCheck;
    private AtlasBoardLobbyRuntimeBridge cachedBridge;
    private int lastRevision = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnce()
    {
        if (instance != null) return;
        var obj = new GameObject("AtlasBoardBoardThemeVisuals");
        instance = obj.AddComponent<AtlasBoardBoardThemeVisuals>();
        DontDestroyOnLoad(obj);
    }

    public static string CurrentId => activeId;
    public static bool IsValid(string id) =>
        string.IsNullOrEmpty(id) || id == "default_board" ||
        id == "board_midnight_azure" || id == "board_ember_maple" ||
        id == "board_arctic_aurora";

    public static void ApplySelection(string id)
    {
        string next = IsValid(id) && !string.IsNullOrEmpty(id) ? id : "default_board";
        if (activeId == next) return;
        activeId = next;
        if (instance != null) instance.lastAppliedId = "";
    }

    private void Update()
    {
        // All clients follow the same authoritative room setting. Reconnect
        // and host-migration snapshots retain the active match's board ID.
        if (Time.unscaledTime >= nextSnapshotCheck)
        {
            nextSnapshotCheck = Time.unscaledTime + 0.5f;
            if (cachedBridge == null)
                cachedBridge = FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>();
            if (cachedBridge != null && cachedBridge.HasLobby &&
                cachedBridge.CurrentSnapshot != null)
            {
                var snap = cachedBridge.CurrentSnapshot;
                if (snap.LobbyId != lastLobbyId || snap.SettingsRevision != lastRevision)
                {
                    lastLobbyId = snap.LobbyId;
                    lastRevision = snap.SettingsRevision;
                    ApplySelection(snap.Settings != null
                        ? snap.Settings.BoardThemeId : "default_board");
                }
            }
            else if (lastLobbyId.Length > 0)
            {
                // Keep the match board if lobby bridge temporarily vanishes.
                // Returning to the menu explicitly resets through controller.
                lastLobbyId = "";
                lastRevision = -1;
            }
        }
        if ((edge == null || center == null) && Time.unscaledTime >= nextRendererCheck)
        {
            FindBoardRenderers();
            nextRendererCheck = Time.unscaledTime + 3f;
        }
        if (lastAppliedId != activeId)
        {
            ApplyPalette();
            lastAppliedId = activeId;
        }
    }

    private void FindBoardRenderers()
    {
        edge = null; center = null;
        Renderer[] all = FindObjectsByType<Renderer>();
        foreach (var rend in all)
        {
            if (rend == null || !rend.gameObject.scene.IsValid()) continue;
            string n = rend.gameObject.name;
            if (n == "BoardBase") edge = rend;
            else if (n == "CenterArea") center = rend;
        }
        lastAppliedId = "";
    }

    private void SetColor(Renderer rend, Color color)
    {
        if (rend == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        rend.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        rend.SetPropertyBlock(block);
    }

    private void Clear(Renderer rend)
    {
        if (rend != null) rend.SetPropertyBlock(null);
    }

    private void ApplyPalette()
    {
        if (activeId == "default_board")
        {
            Clear(edge); Clear(center);
            return;
        }
        Color perimeter, middle;
        switch (activeId)
        {
            case "board_midnight_azure":
                perimeter = new Color32(19, 42, 88, 255);
                middle = new Color32(90, 158, 208, 255);
                break;
            case "board_ember_maple":
                perimeter = new Color32(115, 48, 25, 255);
                middle = new Color32(228, 174, 108, 255);
                break;
            case "board_arctic_aurora":
                perimeter = new Color32(22, 83, 96, 255);
                middle = new Color32(145, 219, 211, 255);
                break;
            default:
                Clear(edge); Clear(center); return;
        }
        SetColor(edge, perimeter);
        SetColor(center, middle);
        Debug.Log("ATB_BOARD_THEME_VISUAL applied=" + activeId +
            " boardBase=" + (edge != null) + " centerArea=" + (center != null));
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
