#if UNITY_EDITOR

using UnityEngine;
using nadena.dev.ndmf;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace YOTS
{
  [DisallowMultipleComponent]
  public class YOTSNDMFConfig : MonoBehaviour {
    [Tooltip("The JSON configuration file.")]
    public TextAsset jsonConfig;

    void OnValidate() {
      gameObject.tag = "EditorOnly";
    }
  }
}

#endif  // UNITY_EDITOR
