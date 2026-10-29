#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Appends premium map assets to the existing map dropdowns; never recreates UI or managers.</summary>
public static class AtlasBoardPhase19ABPremiumMapsInstaller
{
    [MenuItem("Atlas Board/QA & Diagnostics/Install Germany and Mexico Maps")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Exit Play Mode first."); return; }
        var germany = AssetDatabase.LoadAssetAtPath<BoardMapDefinition>("Assets/Project/Data/Maps/map_germany.asset");
        var mexico = AssetDatabase.LoadAssetAtPath<BoardMapDefinition>("Assets/Project/Data/Maps/map_mexico.asset");
        if (germany == null || mexico == null || germany.Tiles.Count != 32 || mexico.Tiles.Count != 32)
        { Debug.LogError("Premium maps missing or invalid: expected two 32-tile assets."); return; }
        int setupCount = 0, dropdownCount = 0;
        foreach (var setup in UnityEngine.Object.FindObjectsByType<MatchSetupManager>(FindObjectsInactive.Include))
        {
            if (!setup.gameObject.scene.IsValid()) continue;
            var so = new SerializedObject(setup); var prop = so.FindProperty("availableMaps");
            if (prop == null) continue;
            List<UnityEngine.Object> maps = new List<UnityEngine.Object>();
            for (int i=0; i<prop.arraySize; i++) maps.Add(prop.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (var added in new BoardMapDefinition[] { germany, mexico }) if (!maps.Contains(added)) maps.Add(added);
            prop.arraySize = maps.Count;
            for (int i=0; i<maps.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue=maps[i];
            so.ApplyModifiedProperties(); EditorUtility.SetDirty(setup); setupCount++;
        }
        var mainTypes = UnityEngine.Object.FindObjectsByType<AtlasBoardMainMenuController>(FindObjectsInactive.Include);
        foreach (var menu in mainTypes)
        {
            var so=new SerializedObject(menu); var prop=so.FindProperty("mapDropdown");
            var dd=prop != null ? prop.objectReferenceValue as TMP_Dropdown : null;
            if (dd != null && Append(dd)) dropdownCount++;
        }
        foreach (var lobby in UnityEngine.Object.FindObjectsByType<AtlasBoardPrivateLobbyUIController>(FindObjectsInactive.Include))
        {
            var so=new SerializedObject(lobby); var prop=so.FindProperty("hostSettingsDropdowns");
            if(prop!=null && prop.arraySize>0) { var dd=prop.GetArrayElementAtIndex(0).objectReferenceValue as TMP_Dropdown; if(dd!=null && Append(dd)) dropdownCount++; }
        }
        EditorSceneManager.MarkAllScenesDirty(); AssetDatabase.SaveAssets();
        Debug.Log("ATB_MAP_INSTALL status=ready added_maps=2 setup_components="+setupCount+" dropdowns="+dropdownCount+" Save scene (Ctrl+S) and verify owner entitlement before online match.");
    }
    private static bool Append(TMP_Dropdown dd)
    {
        // Do not rebuild options or change existing Turkey/Colorado/USA indices.
        // A serialized component can already exist even if an older code path
        // later reset the dropdown to only three choices. Always heal options.
        if (!AtlasBoardPremiumMapDropdownLabels.EnsureOptions(dd))
        {
            Debug.LogWarning("ATB_MAP_INSTALL dropdown has fewer than 3 base maps: " + dd.name);
            return false;
        }
        if(dd.GetComponent<AtlasBoardPremiumMapDropdownLabels>() == null)
            Undo.AddComponent<AtlasBoardPremiumMapDropdownLabels>(dd.gameObject);
        dd.RefreshShownValue(); EditorUtility.SetDirty(dd); return true;
    }
}
#endif
