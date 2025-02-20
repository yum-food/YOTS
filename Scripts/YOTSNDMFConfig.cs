#if UNITY_EDITOR

using UnityEngine;
using nadena.dev.ndmf;
using VRC.SDK3.Avatars.ScriptableObjects;
using UnityEditor;
using System.IO;

namespace YOTS
{
  [DisallowMultipleComponent]
  [AddComponentMenu("YOTS NDMF Config")]
  public class YOTSNDMFConfig : MonoBehaviour {
    [Tooltip("The JSON configuration file.")]
    public TextAsset jsonConfig;
    
    [TextArea(5, 80)]  // Min 5 lines, max 80 lines
    public string jsonContent;

    [SerializeField, HideInInspector]
    private TextAsset lastJsonConfig;

    void OnValidate() {
      gameObject.tag = "EditorOnly";
      
      // Only update jsonContent when jsonConfig actually changes
      if (jsonConfig != lastJsonConfig) {
        if (jsonConfig != null) {
          jsonContent = jsonConfig.text;
        }
        lastJsonConfig = jsonConfig;
      }
    }
  }

  [CustomEditor(typeof(YOTSNDMFConfig))]
  public class YOTSNDMFConfigEditor : Editor
  {
    private YOTSNDMFConfig config;

    private void OnEnable()
    {
      config = (YOTSNDMFConfig)target;
    }

    public override void OnInspectorGUI()
    {
      EditorGUI.BeginChangeCheck();

      // Draw the default inspector
      DrawDefaultInspector();

      EditorGUILayout.HelpBox(
        "You can inspect and edit the JSON config using the textbox above. " +
        "Changes are saved automatically. Changes from external editors will " +
        "appear upon tabbing back into Unity.",
        MessageType.Info);


      // If changes were made in the inspector
      if (EditorGUI.EndChangeCheck())
      {
        // Save changes immediately
        SaveJsonToFile();
      }
      // Only check for file changes if we're not currently editing
      else if (config.jsonConfig != null)
      {
        string currentContent = config.jsonConfig.text;
        if (currentContent != config.jsonContent)
        {
          config.jsonContent = currentContent;
          GUI.changed = true;
        }
      }

      // Check for Ctrl+S
      Event e = Event.current;
      if (e.type == EventType.KeyDown && e.keyCode == KeyCode.S && e.control)
      {
        e.Use();
        SaveJsonToFile();
      }
    }

    private void SaveJsonToFile()
    {
      if (config.jsonConfig == null)
      {
        Debug.LogWarning("No JSON config file assigned!");
        return;
      }

      string assetPath = AssetDatabase.GetAssetPath(config.jsonConfig);
      if (string.IsNullOrEmpty(assetPath))
      {
        Debug.LogError("Could not find asset path!");
        return;
      }

      try
      {
        // Write the modified content from our component
        File.WriteAllText(assetPath, config.jsonContent);
        
        // Force Unity to reload the file from disk
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        
        // Update the TextAsset to match our changes
        var serializedObject = new SerializedObject(config.jsonConfig);
        serializedObject.FindProperty("m_Script").stringValue = config.jsonContent;
        serializedObject.ApplyModifiedProperties();
        
        EditorUtility.SetDirty(config.jsonConfig);
        AssetDatabase.SaveAssets();
        Debug.Log($"Successfully saved JSON to {assetPath}");
      }
      catch (System.Exception ex)
      {
        Debug.LogError($"Error saving JSON: {ex.Message}");
      }
    }
  }

  // Add this new class to handle file modifications
  public class JsonFileProcessor : UnityEditor.AssetModificationProcessor
  {
    private static void OnWillSaveAssets(string[] paths)
    {
      foreach (string path in paths)
      {
        if (path.EndsWith(".json"))
        {
            // Force Unity to reimport the asset
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
      }
    }
  }
}

#endif  // UNITY_EDITOR
