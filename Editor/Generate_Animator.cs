using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace YOTS
{
    [System.Serializable]
    public class ToggleSpec
    {
        [SerializeField]
        public string name;
        // Dependencies are evaluated before this one. They must share one or
        // more attributes with this spec.
        [SerializeField]
        public List<string> dependencies = new List<string>();
        [SerializeField]
        public List<string> meshToggles = new List<string>();
        [SerializeField]
        public List<BlendShapeSpec> blendShapes = new List<BlendShapeSpec>();

        public ToggleSpec(string name)
        {
            this.name = name;
        }

        public ToggleSpec() {}

        public IEnumerable<string> GetAffectedAttributes()
        {
            foreach (var mesh in meshToggles)
            {
                yield return $"MeshToggle:{mesh}";
            }

            foreach (var blend in blendShapes)
            {
                yield return $"BlendShape:{blend.path}/{blend.blendShape}";
            }
        }
    }

    [System.Serializable]
    public class BlendShapeSpec
    {
        [SerializeField]
        public string path;
        
        [SerializeField]
        public string blendShape;

        public BlendShapeSpec(string path, string blendShape)
        {
            this.path = path;
            this.blendShape = blendShape;
        }

        public BlendShapeSpec() {}
    }

    [System.Serializable]
    public class AnimatorConfigFile
    {
        [SerializeField]
        public List<ToggleSpec> toggles = new List<ToggleSpec>();

        [SerializeField]
        public string api_version;
    }

    [System.Serializable]
    public class GeneratedAnimationsConfig
    {
        public List<GeneratedAnimationClipConfig> animations =
            new List<GeneratedAnimationClipConfig>();
    }

    [System.Serializable]
    public class GeneratedAnimationClipConfig
    {
        public string name;
        public List<GeneratedMeshToggle> meshToggles =
            new List<GeneratedMeshToggle>();
        public List<GeneratedBlendShape> blendShapes =
            new List<GeneratedBlendShape>();
    }

    [System.Serializable]
    public class GeneratedMeshToggle
    {
        public string path;
        public float value;
    }

    [System.Serializable]
    public class GeneratedBlendShape
    {
        public string path;
        public string blendShape;
        public float value;
    }

    // These classes describe the generated JSON output for the animator configuration.
    [System.Serializable]
    public class GeneratedAnimatorConfig
    {
        public string name;
        public List<string> parameters = new List<string>();
        public List<AnimatorLayer> layers = new List<AnimatorLayer>();
        public List<GeneratedAnimationClipConfig> animations =
            new List<GeneratedAnimationClipConfig>();
    }

    [System.Serializable]
    public class AnimatorLayer
    {
        public string name;
        public AnimatorDirectBlendTree directBlendTree =
            new AnimatorDirectBlendTree();
    }

    [System.Serializable]
    public class AnimatorDirectBlendTree
    {
        public List<AnimatorDirectBlendTreeEntry> entries =
            new List<AnimatorDirectBlendTreeEntry>();
    }

    [System.Serializable]
    public class AnimatorDirectBlendTreeEntry
    {
        public string name;       // animation name
        public string parameter;  // parameter driving the animation
    }

    // Add these new classes at the namespace level
    [System.Serializable]
    public class VRCMenuConfig
    {
        public string menuName = "YOTS";
        public List<VRCMenuItemConfig> items = new List<VRCMenuItemConfig>();
    }

    [System.Serializable]
    public class VRCMenuItemConfig
    {
        public string name;
        public string parameter;
        public Texture2D icon;
    }

    // This class adds both a menu command and GUI window for animator generation
    public class GenerateAnimatorCommand : EditorWindow
    {
        private string jsonPath;
        private string animatorName = "YOTS_FX";
        private string existingParamsPath;
        private string existingMenuPath;
        private VRCExpressionParameters existingParams;
        private VRCExpressionsMenu existingMenu;

        [MenuItem("Tools/yum_food/YOTS")]
        public static void ShowWindow()
        {
            GetWindow<GenerateAnimatorCommand>("YOTS");
        }

        private void OnGUI()
        {
            GUILayout.Label("YOTS Animator Generator", EditorStyles.boldLabel);

            // Create a drag-drop field for the JSON config
            var jsonObj = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            var newJsonObj = (TextAsset)EditorGUILayout.ObjectField(
                "Config JSON",
                jsonObj,
                typeof(TextAsset),
                false
            );
            if (newJsonObj != jsonObj)
            {
                jsonPath = AssetDatabase.GetAssetPath(newJsonObj);
            }
            if (string.IsNullOrEmpty(jsonPath))
            {
                EditorGUILayout.HelpBox("Config JSON must be provided.", MessageType.Error);
            }

            animatorName = EditorGUILayout.TextField("Animator Name", animatorName);

            // Replace file path fields with Object fields for drag-and-drop
            existingParams = (VRCExpressionParameters)EditorGUILayout.ObjectField(
                "VRC Parameters",
                existingParams,
                typeof(VRCExpressionParameters),
                false
            );
            existingParamsPath = existingParams != null ? AssetDatabase.GetAssetPath(existingParams) : null;

            existingMenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(
                "VRC Menu",
                existingMenu,
                typeof(VRCExpressionsMenu),
                false
            );
            existingMenuPath = existingMenu != null ? AssetDatabase.GetAssetPath(existingMenu) : null;

            // Show error message if either field is missing
            if (existingParams == null || existingMenu == null)
            {
                EditorGUILayout.HelpBox("VRC parameters and menu must be provided.", MessageType.Error);
            }

            GUI.enabled = !string.IsNullOrEmpty(jsonPath) && existingParams != null && existingMenu != null;
            if (GUILayout.Button("Generate Animator"))
            {
                if (string.IsNullOrEmpty(jsonPath))
                {
                    EditorUtility.DisplayDialog("Error", "Please select a configuration file.", "OK");
                    return;
                }
                GenerateAnimator(jsonPath, animatorName, existingParamsPath, existingMenuPath);
            }
            GUI.enabled = true;
        }

        private static AnimationClip AssignOrCreateAnimationClip(AnimationClip newClip, string clipPath)
        {
            AnimationClip existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existingClip != null)
            {
                // Clear existing curves
                var existingBindings = AnimationUtility.GetCurveBindings(existingClip);
                foreach (var binding in existingBindings)
                {
                    AnimationUtility.SetEditorCurve(existingClip, binding, null);
                }
                // Copy new curves from our temporary clip
                var newBindings = AnimationUtility.GetCurveBindings(newClip);
                foreach (var binding in newBindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(newClip, binding);
                    AnimationUtility.SetEditorCurve(existingClip, binding, curve);
                }
                EditorUtility.SetDirty(existingClip);
                return existingClip;
            }
            else
            {
                AssetDatabase.CreateAsset(newClip, clipPath);
                return newClip;
            }
        }

        private static GeneratedAnimationsConfig GenerateAnimationConfig(List<ToggleSpec> toggleSpecs)
        {
            GeneratedAnimationsConfig genAnimConfig = new GeneratedAnimationsConfig();
            foreach (var toggle in toggleSpecs)
            {
                GeneratedAnimationClipConfig onAnim = new GeneratedAnimationClipConfig();
                onAnim.name = toggle.name + "_On";
                if (toggle.meshToggles != null)
                {
                    foreach (var mesh in toggle.meshToggles)
                    {
                        onAnim.meshToggles.Add(new GeneratedMeshToggle { path = mesh, value = 1.0f });
                    }
                }
                if (toggle.blendShapes != null)
                {
                    foreach (var bs in toggle.blendShapes)
                    {
                        onAnim.blendShapes.Add(new GeneratedBlendShape { path = bs.path, blendShape = bs.blendShape, value = 100.0f });
                    }
                }
                genAnimConfig.animations.Add(onAnim);

                GeneratedAnimationClipConfig offAnim = new GeneratedAnimationClipConfig();
                offAnim.name = toggle.name + "_Off";
                if (toggle.meshToggles != null)
                {
                    foreach (var mesh in toggle.meshToggles)
                    {
                        offAnim.meshToggles.Add(new GeneratedMeshToggle { path = mesh, value = 0.0f });
                    }
                }
                if (toggle.blendShapes != null)
                {
                    foreach (var bs in toggle.blendShapes)
                    {
                        offAnim.blendShapes.Add(new GeneratedBlendShape { path = bs.path, blendShape = bs.blendShape, value = 0.0f });
                    }
                }
                genAnimConfig.animations.Add(offAnim);
            }
            return genAnimConfig;
        }

        private static void CreateAnimationClips(GeneratedAnimationsConfig animationsConfig, string outputDir)
        {
            foreach (var clipConfig in animationsConfig.animations)
            {
                AnimationClip newClip = new AnimationClip();
                newClip.name = clipConfig.name;

                // Apply mesh toggles
                foreach (var meshToggle in clipConfig.meshToggles)
                {
                    AnimationCurve curve = new AnimationCurve(new Keyframe(0, meshToggle.value));
                    EditorCurveBinding binding = new EditorCurveBinding();
                    binding.path = meshToggle.path;
                    binding.type = typeof(GameObject);
                    binding.propertyName = "m_IsActive";
                    AnimationUtility.SetEditorCurve(newClip, binding, curve);
                }

                // Apply blend shapes
                foreach (var blendShape in clipConfig.blendShapes)
                {
                    AnimationCurve curve = AnimationCurve.Constant(0, 0, blendShape.value);
                    EditorCurveBinding binding = new EditorCurveBinding();
                    binding.path = blendShape.path;
                    binding.type = typeof(SkinnedMeshRenderer);
                    binding.propertyName = "blendShape." + blendShape.blendShape;
                    AnimationUtility.SetEditorCurve(newClip, binding, curve);
                }

                string clipPath = Path.Combine(outputDir, "Animations", $"{clipConfig.name}.anim");
                AnimationClip clip = AssignOrCreateAnimationClip(newClip, clipPath);
                Debug.Log("Created/Updated animation clip: " + clipConfig.name + " at path: " + clipPath);
            }
        }

        private static AnimatorController InitializeAnimatorController(string controllerPath)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                Debug.Log("Created new AnimatorController at: " + controllerPath);
            }
            else
            {
                Debug.Log("Reusing existing AnimatorController GUID at: " + controllerPath);
                
                // Clean up all sub-assets (BlendTrees, StateMachines) before clearing parameters and layers
                var subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(controllerPath);
                foreach (var subAsset in subAssets)
                {
                    if (subAsset is BlendTree || subAsset is AnimatorStateMachine)
                    {
                        Object.DestroyImmediate(subAsset, true);
                    }
                }

                // Clear parameters and layers
                while (controller.parameters.Length > 0)
                {
                    controller.RemoveParameter(controller.parameters[0]);
                }
                while (controller.layers.Length > 0)
                {
                    controller.RemoveLayer(0);
                }
            }
            return controller;
        }

        private static void GenerateAnimatorController(GeneratedAnimatorConfig animatorConfig, string generatedOutputDir)
        {
            string controllerPath = Path.Combine(generatedOutputDir, $"{animatorConfig.name}.controller");
            AnimatorController controller = InitializeAnimatorController(controllerPath);

            // This is always set to 1 and used to ensure that each DBT always
            // animates.
            // More info on vrc.schooL:
            //   https://vrc.school/docs/Other/DBT-Combining
            controller.AddParameter("YOTS_Weight", AnimatorControllerParameterType.Float);
            controller.parameters[0].defaultFloat = 1.0f;

            // Add parameters from the config as float parameters
            foreach (var param in animatorConfig.parameters)
            {
                if (!controller.parameters.Any(p => p.name == param))
                {
                    controller.AddParameter(param, AnimatorControllerParameterType.Float);
                }
            }

            // Process base layer first
            var baseLayer = animatorConfig.layers[0];
            var baseStateMachine = new AnimatorStateMachine();
            baseStateMachine.name = "BaseLayer_SM";
            AssetDatabase.AddObjectToAsset(baseStateMachine, controller);

            // Create the root Direct Blend Tree
            var rootBlendTree = new BlendTree();
            rootBlendTree.name = "BaseLayer_RootBlendTree";
            rootBlendTree.blendType = BlendTreeType.Direct;
            AssetDatabase.AddObjectToAsset(rootBlendTree, controller);

            // Create 1D blend trees for each parameter in the base layer
            var parameterGroups = baseLayer.directBlendTree.entries
                .GroupBy(e => e.parameter)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var group in parameterGroups)
            {
                var param = group.Key;
                var entries = group.Value;

                // Create 1D blend tree for this parameter
                var paramBlendTree = new BlendTree();
                paramBlendTree.name = $"BlendTree_{param}";
                paramBlendTree.blendType = BlendTreeType.Simple1D;
                paramBlendTree.blendParameter = param;
                AssetDatabase.AddObjectToAsset(paramBlendTree, controller);

                // Add On/Off animations to the 1D blend tree
                var children = new List<ChildMotion>();
                foreach (var entry in entries.OrderBy(e => e.name.EndsWith("_On")))
                {
                    Debug.Log("Adding child motion for: " + entry.name);
                    string clipPath = Path.Combine(generatedOutputDir, "Animations", $"{entry.name}.anim");
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip == null)
                    {
                        Debug.LogWarning("Animation clip not found at: " + clipPath);
                        continue;
                    }

                    children.Add(new ChildMotion
                    {
                        motion = clip,
                        timeScale = 1f,
                        threshold = entry.name.EndsWith("_On") ? 1f : 0f
                    });
                }
                paramBlendTree.children = children.ToArray();

                // Add this 1D blend tree to the root Direct Blend Tree
                rootBlendTree.children = rootBlendTree.children.Append(new ChildMotion
                {
                    motion = paramBlendTree,
                    timeScale = 1f,
                    directBlendParameter = "YOTS_Weight"
                }).ToArray();
            }

            // Set up base layer state
            var baseState = baseStateMachine.AddState("BaseLayer_State");
            baseState.motion = rootBlendTree;
            baseState.writeDefaultValues = true;
            baseStateMachine.defaultState = baseState;

            // Add base layer to controller
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "YOTS_BaseLayer",
                defaultWeight = 1.0f,
                stateMachine = baseStateMachine
            });

            // Process override layers (if any)
            for (int i = 1; i < animatorConfig.layers.Count; i++)
            {
                var layerConfig = animatorConfig.layers[i];
                string layerName = $"YOTS_OverrideLayer{(i-1).ToString("00")}";

                var stateMachine = new AnimatorStateMachine();
                stateMachine.name = layerName + "_SM";
                AssetDatabase.AddObjectToAsset(stateMachine, controller);

                var blendTree = new BlendTree();
                blendTree.name = layerName + "_BlendTree";
                blendTree.blendType = BlendTreeType.Direct;

                foreach (var entry in layerConfig.directBlendTree.entries)
                {
                    string clipPath = Path.Combine(generatedOutputDir, "Animations", $"{entry.name}.anim");
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip == null)
                    {
                        Debug.LogWarning("Animation clip not found at: " + clipPath);
                        continue;
                    }
                    
                    blendTree.children = blendTree.children.Append(new ChildMotion
                    {
                        motion = clip,
                        timeScale = 1f,
                        directBlendParameter = entry.parameter
                    }).ToArray();
                }
                AssetDatabase.AddObjectToAsset(blendTree, controller);

                var state = stateMachine.AddState(layerName + "_State");
                state.motion = blendTree;
                state.writeDefaultValues = true;
                stateMachine.defaultState = state;

                controller.AddLayer(new AnimatorControllerLayer
                {
                    name = layerName,
                    defaultWeight = 1.0f,
                    stateMachine = stateMachine
                });

                Debug.Log($"Added override layer: {layerName}");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("Animator Controller generation complete at: " + controllerPath);
        }

        private static Dictionary<string, int> TopologicalSortToggles(List<ToggleSpec> toggleSpecs)
        {
            // Create adjacency list
            Dictionary<string, HashSet<string>> graph = new Dictionary<string, HashSet<string>>();
            foreach (var toggle in toggleSpecs)
            {
                if (!graph.ContainsKey(toggle.name))
                {
                    graph[toggle.name] = new HashSet<string>();
                }
                foreach (var dep in toggle.dependencies)
                {
                    if (!graph.ContainsKey(dep))
                    {
                        graph[dep] = new HashSet<string>();
                    }
                    graph[dep].Add(toggle.name);
                }
            }

            // Calculate in-degrees
            Dictionary<string, int> inDegree = new Dictionary<string, int>();
            foreach (var toggle in toggleSpecs)
            {
                inDegree[toggle.name] = toggle.dependencies.Count;
            }

            // Perform topological sort with depth tracking
            Dictionary<string, int> depths = new Dictionary<string, int>();
            Queue<string> queue = new Queue<string>();

            // Add all nodes with no dependencies to queue with depth 0
            foreach (var pair in inDegree)
            {
                if (pair.Value == 0)
                {
                    queue.Enqueue(pair.Key);
                    depths[pair.Key] = 0;
                }
            }

            int processedNodes = 0;
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                processedNodes++;
                int currentDepth = depths[current];

                if (graph.ContainsKey(current))
                {
                    foreach (var neighbor in graph[current])
                    {
                        inDegree[neighbor]--;
                        if (inDegree[neighbor] == 0)
                        {
                            queue.Enqueue(neighbor);
                            depths[neighbor] = currentDepth + 1;
                        }
                    }
                }
            }

            // Check for cycles
            if (processedNodes != toggleSpecs.Count)
            {
                // Find nodes involved in the cycle for a better error message
                var cycleNodes = toggleSpecs
                    .Where(t => !depths.ContainsKey(t.name))
                    .Select(t => t.name)
                    .ToList();
                    
                throw new System.Exception($"Dependency cycle detected in toggle specifications. Nodes involved: {string.Join(", ", cycleNodes)}");
            }

            return depths;
        }

        private static GeneratedAnimatorConfig GenerateNaiveAnimatorConfig(List<ToggleSpec> toggleSpecs, string animatorName)
        {
            GeneratedAnimatorConfig genAnimatorConfig = new GeneratedAnimatorConfig();
            genAnimatorConfig.name = animatorName;

            // Generate animations
            GeneratedAnimationsConfig animConfig = GenerateAnimationConfig(toggleSpecs);
            genAnimatorConfig.animations = animConfig.animations;  // Store animations inline

            // Topologically sort the toggles according to their dependencies
            Dictionary<string, int> depths = TopologicalSortToggles(toggleSpecs);
            
            var togglesByDepth = toggleSpecs
                .GroupBy(t => depths[t.name])
                .OrderBy(g => g.Key)
                .ToList();

            // Create one layer for each set of toggles at a given topological depth
            for (int i = 0; i < togglesByDepth.Count; i++)
            {
                var depthGroup = togglesByDepth[i];
                AnimatorLayer layer = new AnimatorLayer();
                layer.name = i == 0 ? "BaseLayer" : $"OverrideLayer{(i-1).ToString("00")}";
                
                foreach (var toggle in depthGroup)
                {
                    string paramName = toggle.name;
                    if (!genAnimatorConfig.parameters.Contains(paramName))
                    {
                        genAnimatorConfig.parameters.Add(paramName);
                    }

                    layer.directBlendTree.entries.Add(new AnimatorDirectBlendTreeEntry {
                        name = toggle.name + "_On",
                        parameter = paramName
                    });

                    layer.directBlendTree.entries.Add(new AnimatorDirectBlendTreeEntry {
                        name = toggle.name + "_Off",
                        parameter = paramName
                    });
                }
                
                genAnimatorConfig.layers.Add(layer);
            }

            return genAnimatorConfig;
        }

        private static GeneratedAnimatorConfig ApplyIndependentFixToAnimatorConfig(GeneratedAnimatorConfig genAnimatorConfig)
        {
            // Group paired animations by toggle name (extracted from the animation name by removing _On/_Off).
            Dictionary<string, (GeneratedAnimationClipConfig on, GeneratedAnimationClipConfig off)> toggleAnimations =
                new Dictionary<string, (GeneratedAnimationClipConfig, GeneratedAnimationClipConfig)>();

            foreach (var anim in genAnimatorConfig.animations)
            {
                if (anim.name.EndsWith("_On"))
                {
                    string toggleName = anim.name.Substring(0, anim.name.LastIndexOf("_On"));
                    if (!toggleAnimations.ContainsKey(toggleName))
                    {
                        toggleAnimations[toggleName] = (null, null);
                    }
                    var pair = toggleAnimations[toggleName];
                    pair.on = anim;
                    toggleAnimations[toggleName] = pair;
                }
                else if (anim.name.EndsWith("_Off"))
                {
                    string toggleName = anim.name.Substring(0, anim.name.LastIndexOf("_Off"));
                    if (!toggleAnimations.ContainsKey(toggleName))
                    {
                        toggleAnimations[toggleName] = (null, null);
                    }
                    var pair = toggleAnimations[toggleName];
                    pair.off = anim;
                    toggleAnimations[toggleName] = pair;
                }
            }

            // Determine in which layer a given toggle exists.
            // (We assume that the base layer is named "BaseLayer" or is the first layer.)
            Dictionary<string, int> toggleToLayerIndex = new Dictionary<string, int>();
            for (int i = 0; i < genAnimatorConfig.layers.Count; i++)
            {
                var layer = genAnimatorConfig.layers[i];
                foreach (var entry in layer.directBlendTree.entries)
                {
                    string entryName = entry.name;
                    // Remove any existing suffix (_On, _Off, _Independent, _Dependent) to get the base toggle.
                    string toggleName = entryName;
                    if (toggleName.EndsWith("_On"))
                    {
                        toggleName = toggleName.Substring(0, toggleName.LastIndexOf("_On"));
                    }
                    else if (toggleName.EndsWith("_Off"))
                    {
                        toggleName = toggleName.Substring(0, toggleName.LastIndexOf("_Off"));
                    }
                    else if (toggleName.Contains("_Independent"))
                    {
                        toggleName = toggleName.Replace("_Independent", "");
                    }
                    else if (toggleName.Contains("_Dependent"))
                    {
                        toggleName = toggleName.Replace("_Dependent", "");
                    }

                    if (!toggleToLayerIndex.ContainsKey(toggleName))
                    {
                        toggleToLayerIndex[toggleName] = i;
                    }
                }
            }

            // Build a global mapping from each affected attribute to the set of toggles that affect it.
            // For mesh toggles the key is "MeshToggle:{path}" and for blend shapes "BlendShape:{path}/{blendShape}".
            Dictionary<string, HashSet<string>> attributeToToggles = new Dictionary<string, HashSet<string>>();
            foreach (var kvp in toggleAnimations)
            {
                string toggleName = kvp.Key;
                var pair = kvp.Value;
                if (pair.on == null) continue; // skip if missing

                HashSet<string> attributes = new HashSet<string>();
                if (pair.on.meshToggles != null)
                {
                    foreach (var mt in pair.on.meshToggles)
                    {
                        string attr = "MeshToggle:" + mt.path;
                        attributes.Add(attr);
                    }
                }
                if (pair.on.blendShapes != null)
                {
                    foreach (var bs in pair.on.blendShapes)
                    {
                        string attr = "BlendShape:" + bs.path + "/" + bs.blendShape;
                        attributes.Add(attr);
                    }
                }

                foreach (var attr in attributes)
                {
                    if (!attributeToToggles.TryGetValue(attr, out var set))
                    {
                        set = new HashSet<string>();
                        attributeToToggles[attr] = set;
                    }
                    set.Add(toggleName);
                }
            }

            // We will rebuild the animations list.
            List<GeneratedAnimationClipConfig> newAnimations = new List<GeneratedAnimationClipConfig>();

            // Assume that the base layer is named "BaseLayer"; otherwise use the first layer.
            AnimatorLayer baseLayer = genAnimatorConfig.layers.FirstOrDefault(l => l.name == "BaseLayer");
            if (baseLayer == null && genAnimatorConfig.layers.Count > 0)
            {
                baseLayer = genAnimatorConfig.layers[0];
            }

            // Process each toggle pair.
            foreach (var kvp in toggleAnimations)
            {
                string toggleName = kvp.Key;
                var pair = kvp.Value;
                // Determine the layer index in which this toggle appears.
                int layerIndex = toggleToLayerIndex.ContainsKey(toggleName) ? toggleToLayerIndex[toggleName] : 0;
                bool isBase = (layerIndex == 0);

                if (isBase)
                {
                    // Base layer toggles remain unchanged.
                    newAnimations.Add(pair.on);
                    newAnimations.Add(pair.off);
                }
                else
                {
                    // For toggles in override layers we subdivide the affected attributes.
                    List<GeneratedMeshToggle> independentMesh = new List<GeneratedMeshToggle>();
                    List<GeneratedMeshToggle> dependentMesh = new List<GeneratedMeshToggle>();

                    if (pair.on.meshToggles != null)
                    {
                        foreach (var mt in pair.on.meshToggles)
                        {
                            string attr = "MeshToggle:" + mt.path;
                            // If no other toggle affects this attribute, mark as independent.
                            if (attributeToToggles[attr].Count == 1)
                            {
                                independentMesh.Add(mt);
                            }
                            else
                            {
                                dependentMesh.Add(mt);
                            }
                        }
                    }

                    List<GeneratedBlendShape> independentBlend = new List<GeneratedBlendShape>();
                    List<GeneratedBlendShape> dependentBlend = new List<GeneratedBlendShape>();

                    if (pair.on.blendShapes != null)
                    {
                        foreach (var bs in pair.on.blendShapes)
                        {
                            string attr = "BlendShape:" + bs.path + "/" + bs.blendShape;
                            if (attributeToToggles[attr].Count == 1)
                            {
                                independentBlend.Add(bs);
                            }
                            else
                            {
                                dependentBlend.Add(bs);
                            }
                        }
                    }

                    bool hasIndependent = (independentMesh.Count > 0 || independentBlend.Count > 0);
                    bool hasDependent = (dependentMesh.Count > 0 || dependentBlend.Count > 0);

                    if (hasIndependent && hasDependent)
                    {
                        // Split into two animation pairs.
                        // Create the dependent pair (remain in this (override) layer).
                        GeneratedAnimationClipConfig dependentOn = new GeneratedAnimationClipConfig();
                        dependentOn.name = toggleName + "_Dependent_On";
                        dependentOn.meshToggles = dependentMesh;
                        dependentOn.blendShapes = dependentBlend;

                        // For the Off animation, assume zero values; so copy and set value = 0.
                        GeneratedAnimationClipConfig dependentOff = new GeneratedAnimationClipConfig();
                        dependentOff.name = toggleName + "_Dependent_Off";
                        dependentOff.meshToggles = dependentMesh
                            .Select(mt => new GeneratedMeshToggle { path = mt.path, value = 0.0f })
                            .ToList();
                        dependentOff.blendShapes = dependentBlend
                            .Select(bs => new GeneratedBlendShape { path = bs.path, blendShape = bs.blendShape, value = 0.0f })
                            .ToList();

                        // Create the independent pair (to be added in the base layer).
                        GeneratedAnimationClipConfig independentOn = new GeneratedAnimationClipConfig();
                        independentOn.name = toggleName + "_Independent_On";
                        independentOn.meshToggles = independentMesh;
                        independentOn.blendShapes = independentBlend;

                        GeneratedAnimationClipConfig independentOff = new GeneratedAnimationClipConfig();
                        independentOff.name = toggleName + "_Independent_Off";
                        independentOff.meshToggles = independentMesh
                            .Select(mt => new GeneratedMeshToggle { path = mt.path, value = 0.0f })
                            .ToList();
                        independentOff.blendShapes = independentBlend
                            .Select(bs => new GeneratedBlendShape { path = bs.path, blendShape = bs.blendShape, value = 0.0f })
                            .ToList();

                        // Add our new animations.
                        newAnimations.Add(dependentOn);
                        newAnimations.Add(dependentOff);
                        newAnimations.Add(independentOn);
                        newAnimations.Add(independentOff);

                        // Update the override layer's direct blend tree entries for this toggle.
                        AnimatorLayer overrideLayer = genAnimatorConfig.layers[layerIndex];
                        foreach (var entry in overrideLayer.directBlendTree.entries)
                        {
                            // Here we assume the entry originally matches the toggle name.
                            if (entry.name.StartsWith(toggleName) &&
                                (entry.name.EndsWith("_On") || entry.name.EndsWith("_Off")))
                            {
                                // Change the suffix to _Dependent_...
                                if (entry.name.EndsWith("_On"))
                                {
                                    entry.name = toggleName + "_Dependent_On";
                                }
                                else
                                {
                                    entry.name = toggleName + "_Dependent_Off";
                                }
                            }
                        }

                        // In the base layer, append new direct blend tree entries for the independent pair.
                        if (baseLayer != null)
                        {
                            baseLayer.directBlendTree.entries.Add(new AnimatorDirectBlendTreeEntry
                            {
                                name = toggleName + "_Independent_On",
                                parameter = toggleName
                            });
                            baseLayer.directBlendTree.entries.Add(new AnimatorDirectBlendTreeEntry
                            {
                                name = toggleName + "_Independent_Off",
                                parameter = toggleName
                            });
                        }
                    }
                    else if (hasIndependent && !hasDependent)
                    {
                        // All affected attributes are independent.
                        // Move the entire animation pair into the base layer as _Independent.
                        GeneratedAnimationClipConfig independentOn = new GeneratedAnimationClipConfig();
                        independentOn.name = toggleName + "_Independent_On";
                        independentOn.meshToggles = pair.on.meshToggles;
                        independentOn.blendShapes = pair.on.blendShapes;
                        GeneratedAnimationClipConfig independentOff = new GeneratedAnimationClipConfig();
                        independentOff.name = toggleName + "_Independent_Off";
                        independentOff.meshToggles = pair.off.meshToggles;
                        independentOff.blendShapes = pair.off.blendShapes;

                        newAnimations.Add(independentOn);
                        newAnimations.Add(independentOff);

                        // Remove the entries from the override layer.
                        AnimatorLayer overrideLayer = genAnimatorConfig.layers[layerIndex];
                        overrideLayer.directBlendTree.entries.RemoveAll(e => e.name.StartsWith(toggleName));
                        // Add new entries to the base layer.
                        if (baseLayer != null)
                        {
                            baseLayer.directBlendTree.entries.Add(new AnimatorDirectBlendTreeEntry
                            {
                                name = toggleName + "_Independent_On",
                                parameter = toggleName
                            });
                            baseLayer.directBlendTree.entries.Add(new AnimatorDirectBlendTreeEntry
                            {
                                name = toggleName + "_Independent_Off",
                                parameter = toggleName
                            });
                        }
                    }
                    else if (!hasIndependent && hasDependent)
                    {
                        // All affected attributes are shared.
                        // Simply rename the pair in the override layer with a _Dependent suffix.
                        GeneratedAnimationClipConfig dependentOn = new GeneratedAnimationClipConfig();
                        dependentOn.name = toggleName + "_Dependent_On";
                        dependentOn.meshToggles = pair.on.meshToggles;
                        dependentOn.blendShapes = pair.on.blendShapes;
                        GeneratedAnimationClipConfig dependentOff = new GeneratedAnimationClipConfig();
                        dependentOff.name = toggleName + "_Dependent_Off";
                        dependentOff.meshToggles = pair.off.meshToggles;
                        dependentOff.blendShapes = pair.off.blendShapes;

                        newAnimations.Add(dependentOn);
                        newAnimations.Add(dependentOff);

                        // Update the override layer's entries.
                        AnimatorLayer overrideLayer = genAnimatorConfig.layers[layerIndex];
                        foreach (var entry in overrideLayer.directBlendTree.entries)
                        {
                            if (entry.name.StartsWith(toggleName) &&
                                (entry.name.EndsWith("_On") || entry.name.EndsWith("_Off")))
                            {
                                if (entry.name.EndsWith("_On"))
                                {
                                    entry.name = toggleName + "_Dependent_On";
                                }
                                else
                                {
                                    entry.name = toggleName + "_Dependent_Off";
                                }
                            }
                        }
                    }
                    // In the very unlikely event that there are no affected attributes, nothing is added.
                }
            }

            // Replace the animations list in the config.
            genAnimatorConfig.animations = newAnimations;
            return genAnimatorConfig;
        }

        private static GeneratedAnimatorConfig RemoveOffAnimationsFromOverrideLayers(GeneratedAnimatorConfig config)
        {
            for (int i = 1; i < config.layers.Count; i++)
            {
                var layer = config.layers[i];
                layer.directBlendTree.entries.RemoveAll(entry =>  entry.name.EndsWith("_Off"));
            }

            return config;
        }

        private static GeneratedAnimatorConfig RemoveUnusedAnimations(GeneratedAnimatorConfig config)
        {
            // Collect all animation names referenced in blend tree entries across all layers
            HashSet<string> referencedAnimations = new HashSet<string>();
            foreach (var layer in config.layers)
            {
                foreach (var entry in layer.directBlendTree.entries)
                {
                    referencedAnimations.Add(entry.name);
                }
            }

            // Filter the animations list to keep only referenced animations
            config.animations = config.animations
                .Where(anim => referencedAnimations.Contains(anim.name))
                .ToList();

            Debug.Log($"Removed {config.animations.Count - referencedAnimations.Count} unused animations");
            return config;
        }

        private static void GenerateVRChatAssets(List<string> parameters, string generatedDir, string existingParamsPath = null, string existingMenuPath = null)
        {
            // Create VRC Parameters
            VRCExpressionParameters expressionParameters;
            if (!string.IsNullOrEmpty(existingParamsPath))
            {
                expressionParameters = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(existingParamsPath);
                if (expressionParameters == null)
                {
                    Debug.LogError($"Could not load existing parameters at path: {existingParamsPath}");
                    return;
                }
            }
            else
            {
                expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            }

            // Convert existing parameters to list for modification
            var paramList = new List<VRCExpressionParameters.Parameter>();
            if (expressionParameters.parameters != null)
            {
                // Add existing parameters that don't conflict with YOTS parameters
                paramList.AddRange(expressionParameters.parameters.Where(p => !parameters.Contains(p.name)));
            }
            
            // Add YOTS parameters
            foreach (var param in parameters)
            {
                paramList.Add(new VRCExpressionParameters.Parameter
                {
                    name = param,
                    valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = 1.0f,
                    saved = true
                });
            }

            expressionParameters.parameters = paramList.ToArray();
            
            // Create YOTS submenu
            var yotsSubmenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            foreach (var param in parameters)
            {
                yotsSubmenu.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = param,
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = param },
                    value = 1f
                });
            }

            // Handle the main menu
            VRCExpressionsMenu mainMenu;
            if (!string.IsNullOrEmpty(existingMenuPath))
            {
                mainMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(existingMenuPath);
                if (mainMenu == null)
                {
                    Debug.LogError($"Could not load existing menu at path: {existingMenuPath}");
                    return;
                }
                
                // Remove any existing YOTS submenu
                mainMenu.controls.RemoveAll(c => c.name == "YOTS");
            }
            else
            {
                mainMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            }

            // Save the YOTS submenu asset
            string submenuPath = Path.Combine(generatedDir, "YOTS_Submenu.asset");
            AssetDatabase.CreateAsset(yotsSubmenu, submenuPath);

            // Add YOTS submenu to main menu
            mainMenu.controls.Add(new VRCExpressionsMenu.Control
            {
                name = "YOTS",
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = yotsSubmenu
            });

            // Save the assets
            if (string.IsNullOrEmpty(existingParamsPath))
            {
                string paramPath = Path.Combine(generatedDir, "YOTS_Parameters.asset");
                AssetDatabase.CreateAsset(expressionParameters, paramPath);
                Debug.Log($"Generated new VRChat parameters at: {paramPath}");
            }
            else
            {
                EditorUtility.SetDirty(expressionParameters);
                Debug.Log($"Updated existing VRChat parameters at: {existingParamsPath}");
            }

            if (string.IsNullOrEmpty(existingMenuPath))
            {
                string menuPath = Path.Combine(generatedDir, "YOTS_Menu.asset");
                AssetDatabase.CreateAsset(mainMenu, menuPath);
                Debug.Log($"Generated new VRChat menu at: {menuPath}");
            }
            else
            {
                EditorUtility.SetDirty(mainMenu);
                Debug.Log($"Updated existing VRChat menu at: {existingMenuPath}");
            }

            Debug.Log($"Generated YOTS submenu at: {submenuPath}");
        }

        public static void GenerateAnimator(string configPath = null, string animatorName = "YOTS_FX", string existingParamsPath = null, string existingMenuPath = null)
        {
            Debug.Log("=== Starting Animator Generation Process ===");

            if (string.IsNullOrEmpty(configPath))
            {
                configPath = EditorUtility.OpenFilePanel("Select Animator Config JSON", Application.dataPath, "json");
                if (string.IsNullOrEmpty(configPath))
                {
                    Debug.LogError("No configuration file selected. Process aborted.");
                    return;
                }
            }
            Debug.Log("Loading configuration from: " + configPath);

            string jsonContent = File.ReadAllText(configPath);
            AnimatorConfigFile config;
            try 
            {
                config = JsonUtility.FromJson<AnimatorConfigFile>(jsonContent);
            }
            catch (System.Exception e) 
            {
                Debug.LogError($"JSON parsing failed: {e.Message}");
                return;
            }
            if (config == null) 
            {
                Debug.LogError("Configuration file is empty or invalid");
                return;
            }
            
            if (config.toggles == null) 
            {
                Debug.LogError("No toggleSpecs found in configuration");
                return;
            }
            Debug.Log($"Configuration loaded. Found {config.toggles.Count} toggles.");

            // Ensure all output directories exist
            string generatedDir = Path.Combine("Assets", "YOTS_Generated");
            string fullGeneratedDir = Path.Combine(Application.dataPath, "YOTS_Generated");
            string fullAnimationsDir = Path.Combine(fullGeneratedDir, "Animations");
            
            if (!Directory.Exists(fullGeneratedDir))
            {
                Directory.CreateDirectory(fullGeneratedDir);
                Debug.Log("Created config output directory: " + fullGeneratedDir);
            }
            if (!Directory.Exists(fullAnimationsDir))
            {
                Directory.CreateDirectory(fullAnimationsDir);
                Debug.Log("Created animations output directory: " + fullAnimationsDir);
            }

            // First we generate a naive animator config. We topologically sort
            // toggles according to their dependencies and place them into
            // layers. Everything is structured as an On/Off pair of
            // animations, even though this is only semantically correct
            // for the base layer.
            GeneratedAnimatorConfig genAnimatorConfig = GenerateNaiveAnimatorConfig(config.toggles, animatorName);
            // Next we split animations into independent and dependent parts.
            // Independent parts are melded into the base layer.
            genAnimatorConfig = ApplyIndependentFixToAnimatorConfig(genAnimatorConfig);
            // Next we restructure the override layers as simple "On"
            // animations which override the state inherited from previous layers.
            genAnimatorConfig = RemoveOffAnimationsFromOverrideLayers(genAnimatorConfig);
            // Finally, we scrub out any animations which may have been orphaned.
            genAnimatorConfig = RemoveUnusedAnimations(genAnimatorConfig);

            // Generate VRChat parameters and menu
            GenerateVRChatAssets(genAnimatorConfig.parameters, generatedDir, existingParamsPath, existingMenuPath);

            // Save the generated animator configuration JSON file. This is for
            // debuggability.
            string genAnimatorConfigPath = Path.Combine(fullGeneratedDir, "gen_animator.json");
            File.WriteAllText(genAnimatorConfigPath, JsonUtility.ToJson(genAnimatorConfig, true));
            Debug.Log("Saved generated animator config to: " + genAnimatorConfigPath);

            // Create the animation clips directly from the animator config
            CreateAnimationClips(new GeneratedAnimationsConfig { animations = genAnimatorConfig.animations }, generatedDir);

            // Generate the animator controller
            GenerateAnimatorController(genAnimatorConfig, generatedDir);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("=== Animator Generation Process Complete ===");
        }
    }
}
