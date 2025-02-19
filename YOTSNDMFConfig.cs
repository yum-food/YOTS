using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace YOTS
{
    public class YOTSNDMFConfig : MonoBehaviour
    {
        [Tooltip("The JSON configuration file.")]
        public TextAsset jsonConfig;
    }
}
