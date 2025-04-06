#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace YOTS
{
  [System.Serializable]
  public class ToggleSpec {
    // The name of the toggle. This is shown in the menu.
    [SerializeField]
    public string name;

    // The type of toggle.
    // Accepted values:
    //  "toggle" - A boolean toggle. Creates a boolean sync param.
    //  "radial" - A radial puppet. Creates a float sync param.
    [SerializeField]
    public string type = "toggle";

    // Dependencies are toggles that will be evaluated before this one. If
    // you have two toggles which animate the same thing, one must depend
    // on the other.
    [SerializeField]
    public List<string> dependencies = new List<string>();

    // The name of meshes to toggle.
    // For example, "Body" or "Shirt".
    [SerializeField]
    public List<string> meshToggles = new List<string>();

    // Blendshapes to animate.
    [SerializeField]
    public List<BlendShapeSpec> blendShapes = new List<BlendShapeSpec>();

    // Material properties to animate.
    [SerializeField]
    public List<ShaderToggleSpec> shaderToggles = new List<ShaderToggleSpec>();

    // Where to put the toggle in the menu. All toggles are placed under
    // /YOTS. So if you put "Clothes" here, it'll be placed under
    // /YOTS/Clothes.
    [SerializeField]
    public string menuPath = "/YOTS";

    // The default value of the toggle. Range from 0-1.
    // For example, if you want a gimmick to start toggled off, set this to
    // 0.0f.
    [SerializeField]
    public float defaultValue = 1.0f;

    // Whether the corresponding VRChat parameter is synced.
    [SerializeField]
    public bool synced = true;

    // Whether the corresponding VRChat parameter is saved.
    [SerializeField]
    public bool saved = true;

    // If true, no menu entry will be created for this toggle.
    // The parameter will still be created and can be controlled by other means
    // - for example, via contacts.
    [SerializeField]
    public bool disableMenuEntry = false;

    // Parent constraint weights to animate
    [SerializeField]
    public List<ParentConstraintWeight> parentConstraintWeights = new List<ParentConstraintWeight>();

    // Get the effective parameter name, generating one if not specified
    public string GetParameterName() {
      if (disableMenuEntry) {
        return name;
      }

      return menuPath.TrimEnd('/') + "/" + name;
    }
  }

  [System.Serializable]
  public class BlendShapeSpec {
    // The name of the blend shape to apply.
    // For example, "Chest_Hide" or "Boobs+".
    [SerializeField]
    public string blendShape;

    // The path to the mesh renderer to apply the blend shape to.
    // For example, "Body" or "Shirt".
    [SerializeField]
    public string path;

    // The value of the blendshape when the toggle is off. Range from 0-100.
    [SerializeField]
    public float offValue = 0.0f;

    // The value of the blendshape when the toggle is on. Range from 0-100.
    [SerializeField]
    public float onValue = 100.0f;
  }

  [System.Serializable]
  public class ShaderToggleSpec {
    [SerializeField]
    public string materialProperty;

    [SerializeField]
    public string path = "";

    [SerializeField]
    public List<string> paths = new List<string>();

    [SerializeField]
    public float offValue = 0.0f;

    [SerializeField]
    public float onValue = 1.0f;
    
    [SerializeField]
    public string rendererType = "SkinnedMeshRenderer"; // Can be "SkinnedMeshRenderer" or "MeshRenderer"
  }

  [System.Serializable]
  public class ParentConstraintWeight {
    [SerializeField]
    public string path = "";

    [SerializeField]
    public float offValue = 0.0f;

    [SerializeField]
    public float onValue = 1.0f;
  }

  [System.Serializable]
  public class AnimatorConfigFile {
    [SerializeField]
    public List<ToggleSpec> toggles = new List<ToggleSpec>();

    [SerializeField]
    public string api_version;
  }

  [System.Serializable]
  public class GeneratedAnimationsConfig {
    public List<GeneratedAnimationClipConfig> animations =
      new List<GeneratedAnimationClipConfig>();
  }

  [System.Serializable]
  public class GeneratedAnimationClipConfig {
    public string name;
    public List<GeneratedMeshToggle> meshToggles =
      new List<GeneratedMeshToggle>();
    public List<GeneratedBlendShape> blendShapes =
      new List<GeneratedBlendShape>();
    public List<GeneratedShaderToggle> shaderToggles =
      new List<GeneratedShaderToggle>();
    public List<GeneratedParentConstraint> parentConstraintWeights = new List<GeneratedParentConstraint>();
  }

  [System.Serializable]
  public class GeneratedMeshToggle {
    public string path;
    public float value;
  }

  [System.Serializable]
  public class GeneratedBlendShape {
    public string path;
    public string blendShape;
    public float value;
  }

  // Add new class for generated shader toggles
  [System.Serializable]
  public class GeneratedShaderToggle {
    public string path;
    public string materialProperty;
    public float value;
    public string rendererType = "SkinnedMeshRenderer"; // Default to SkinnedMeshRenderer for backward compatibility
  }

  // Add new class for generated parent constraints
  [System.Serializable]
  public class GeneratedParentConstraint {
    public string path;
    public float value;
  }

  // These classes describe the generated JSON output for the animator configuration.
  [System.Serializable]
  public class GeneratedAnimatorConfig {
    public List<AnimatorParameterSetting> parameters = new List<AnimatorParameterSetting>();
    public List<AnimatorLayer> layers = new List<AnimatorLayer>();
    public List<GeneratedAnimationClipConfig> animations =
      new List<GeneratedAnimationClipConfig>();
  }

  [System.Serializable]
  public class AnimatorLayer {
    public string name;
    public AnimatorDirectBlendTree directBlendTree =
      new AnimatorDirectBlendTree();
  }

  [System.Serializable]
  public class AnimatorDirectBlendTree {
    public List<AnimatorDirectBlendTreeEntry> entries =
      new List<AnimatorDirectBlendTreeEntry>();
  }

  [System.Serializable]
  public class AnimatorDirectBlendTreeEntry {
    public string name;       // animation name
    public string parameter;  // parameter driving the animation
  }

  // Add these new classes at the namespace level
  [System.Serializable]
  public class VRCMenuConfig {
    public string menuName = "YOTS";
    public List<VRCMenuItemConfig> items = new List<VRCMenuItemConfig>();
  }

  [System.Serializable]
  public class VRCMenuItemConfig {
    public string name;
    public string parameter;
    public Texture2D icon;
  }

  [System.Serializable]
  public class AnimatorParameterSetting {
    public string name;
    public float defaultValue;
  }

  public class YOTSCore {
    private static Dictionary<string, AnimationClip> animationClips = new Dictionary<string, AnimationClip>();

    private static string GetMeshToggleAttributeId(string path) {
      return "MeshToggle:" + path;
    }

    private static string GetBlendShapeAttributeId(string path, string blendShape) {
      return "BlendShape:" + path + "/" + blendShape;
    }

    private static string GetShaderToggleAttributeId(string path, string materialProperty) {
      return "ShaderToggle:" + path + "/" + materialProperty;
    }

    private static string GetParentConstraintAttributeId(string path) {
      return "ParentConstraint:" + path;
    }

    public static AnimatorController GenerateAnimator(string configJson,
        VRCExpressionParameters vrcParams, VRCExpressionsMenu vrcMenu) {
      Debug.Log("=== Starting Animator Generation Process ===");

      if (string.IsNullOrEmpty(configJson)) {
        throw new ArgumentException("No config JSON provided.");
      }

      AnimatorConfigFile config;
      config = JsonUtility.FromJson<AnimatorConfigFile>(configJson);
      if (config == null) {
        throw new ArgumentException("JSON config is empty or invalid");
      }
      if (config.toggles == null) {
        throw new ArgumentException("No toggleSpecs found in configuration");
      }
      Debug.Log($"Configuration loaded. Found {config.toggles.Count} toggles.");

      // Create abstract representation of the animator.
      GeneratedAnimatorConfig genAnimatorConfig = GenerateNaiveAnimatorConfig(config.toggles);
      genAnimatorConfig = ApplyIndependentFixToAnimatorConfig(genAnimatorConfig);
      genAnimatorConfig = RemoveOffAnimationsFromOverrideLayers(genAnimatorConfig);
      genAnimatorConfig = RemoveUnusedAnimations(genAnimatorConfig);
      // Create actual assets.
      GenerateVRChatAssets(config.toggles, vrcParams, vrcMenu);
      CreateAnimationClips(new GeneratedAnimationsConfig { animations = genAnimatorConfig.animations });
      AnimatorController controller = GenerateAnimatorController(genAnimatorConfig);

      Debug.Log("=== Animator Generation Process Complete ===");
      return controller;
    }

    private static void CreateAnimationClips(GeneratedAnimationsConfig animationsConfig) {
      foreach (var clipConfig in animationsConfig.animations) {
        AnimationClip newClip = new AnimationClip();
        newClip.name = clipConfig.name;

        // Apply mesh toggles
        foreach (var meshToggle in clipConfig.meshToggles) {
          AnimationCurve curve = new AnimationCurve(new Keyframe(0, meshToggle.value));
          EditorCurveBinding binding = new EditorCurveBinding();
          binding.path = meshToggle.path;
          binding.type = typeof(GameObject);
          binding.propertyName = "m_IsActive";
          AnimationUtility.SetEditorCurve(newClip, binding, curve);
        }

        // Apply blend shapes
        foreach (var blendShape in clipConfig.blendShapes) {
          AnimationCurve curve = AnimationCurve.Constant(0, 0, blendShape.value);
          EditorCurveBinding binding = new EditorCurveBinding();
          binding.path = blendShape.path;
          binding.type = typeof(SkinnedMeshRenderer);
          binding.propertyName = "blendShape." + blendShape.blendShape;
          AnimationUtility.SetEditorCurve(newClip, binding, curve);
        }

        // Apply shader toggles
        foreach (var shaderToggle in clipConfig.shaderToggles) {
          AnimationCurve curve = AnimationCurve.Constant(0, 0, shaderToggle.value);
          EditorCurveBinding binding = new EditorCurveBinding();
          binding.path = shaderToggle.path;
          
          // Use the specified renderer type
          if (shaderToggle.rendererType == "MeshRenderer") {
            binding.type = typeof(MeshRenderer);
          } else {
            binding.type = typeof(SkinnedMeshRenderer); // Default or when explicitly specified
          }
          
          binding.propertyName = $"material.{shaderToggle.materialProperty}";
          AnimationUtility.SetEditorCurve(newClip, binding, curve);
        }

        // Apply parent constraint weights
        foreach (var parentConstraint in clipConfig.parentConstraintWeights) {
          AnimationCurve curve = AnimationCurve.Constant(0, 0, parentConstraint.value);
          EditorCurveBinding binding = new EditorCurveBinding();
          binding.path = parentConstraint.path;
          binding.type = typeof(UnityEngine.Animations.ParentConstraint);
          binding.propertyName = "m_Weight";
          AnimationUtility.SetEditorCurve(newClip, binding, curve);
        }

        // Store in memory
        animationClips[clipConfig.name] = newClip;
        Debug.Log("Created animation clip " + clipConfig.name);
      }
    }

    private static AnimatorController GenerateAnimatorController(GeneratedAnimatorConfig animatorConfig) {
      AnimatorController controller = new AnimatorController();

      // Add weight parameter used to ensure that the blendtrees always
      // run. All layers use this. Documented on vrc.school:
      //   http://vrc.school/docs/Other/DBT-Combining#ed504c95853f4924adeffb6b125234ad
      List<AnimatorControllerParameter> parameters_list = new List<AnimatorControllerParameter>();
      var yots_weight = new AnimatorControllerParameter();
      yots_weight.name = "YOTS_Weight";
      yots_weight.type = AnimatorControllerParameterType.Float;
      yots_weight.defaultFloat = 1.0f;
      parameters_list.Add(yots_weight);
      // Add all other parameters
      foreach (var param in animatorConfig.parameters) {
        var p = new AnimatorControllerParameter();
        p.name = param.name;
        p.type = AnimatorControllerParameterType.Float;
        p.defaultFloat = param.defaultValue;
        parameters_list.Add(p);
      }
      controller.parameters = parameters_list.ToArray();

      // Add base layer. This is structured as a wide direct blendtree
      // (DBT) comprised of blendtrees animating pairs of On/Off
      // animations.
      var baseLayerConfig = animatorConfig.layers[0];
      var baseStateMachine = new AnimatorStateMachine();
      baseStateMachine.name = "YOTS_BaseLayer_SM";

      var rootBlendTree = new BlendTree();
      rootBlendTree.name = "YOTS_BaseLayer_RootBlendTree";
      rootBlendTree.blendType = BlendTreeType.Direct;

      var parameterGroups = baseLayerConfig.directBlendTree.entries
        .GroupBy(e => e.parameter)
        .ToDictionary(g => g.Key, g => g.ToList());

      // Iterate over (parameter, animationSet) pairs in the base layer.
      foreach (var group in parameterGroups) {
        var param = group.Key;
        var animations = group.Value;

        // Create a blendtree controlled by this toggle's parameter.
        var paramBlendTree = new BlendTree();
        paramBlendTree.name = $"YOTS_BlendTree_{param}";
        paramBlendTree.blendType = BlendTreeType.Simple1D;
        paramBlendTree.blendParameter = param;

        var children = new List<ChildMotion>();
        foreach (var animation in animations.OrderBy(e => e.name.EndsWith("_On"))) {
          Debug.Log("Adding child motion for: " + animation.name);
          if (!animationClips.TryGetValue(animation.name, out AnimationClip clip)) {
            throw new InvalidOperationException($"Animation clip not found in memory: {animation.name}");
          }

          children.Add(new ChildMotion{
            motion = clip,
            timeScale = 1f,
            threshold = animation.name.EndsWith("_On") ? 1f : 0f
          });
        }
        paramBlendTree.children = children.ToArray();

        // Add that blendtree to the parent direct blendtree (DBT)
        // controlled by YOTS_Weight. That YOTS_Weight parameter is
        // always set to 1, so the child blendtree always runs.
        rootBlendTree.children = rootBlendTree.children.Append(
            new ChildMotion{
              motion = paramBlendTree,
              timeScale = 1f,
              directBlendParameter = "YOTS_Weight"
            }).ToArray();
      }

      var baseState = baseStateMachine.AddState("YOTS_BaseLayer_State");
      baseState.motion = rootBlendTree;
      baseState.writeDefaultValues = true;
      baseStateMachine.defaultState = baseState;

      controller.AddLayer(new AnimatorControllerLayer{
        name = "YOTS_BaseLayer",
        defaultWeight = 1.0f,
        stateMachine = baseStateMachine
      });

      // Add override layers. These are DBTs of On animations (no Off
      // animations).
      for (int i = 1; i < animatorConfig.layers.Count; i++) {
        var layerConfig = animatorConfig.layers[i];
        string layerName = $"YOTS_OverrideLayer{(i-1).ToString("00")}";

        var stateMachine = new AnimatorStateMachine();
        stateMachine.name = layerName + "_SM";

        var blendTree = new BlendTree();
        blendTree.name = layerName + "_BlendTree";
        blendTree.blendType = BlendTreeType.Direct;

        foreach (var entry in layerConfig.directBlendTree.entries) {
          if (!animationClips.TryGetValue(entry.name, out AnimationClip clip)) {
            throw new InvalidOperationException($"Animation clip not found in memory: {entry.name}");
          }

          blendTree.children = blendTree.children.Append(new ChildMotion{
            motion = clip,
            timeScale = 1f,
            directBlendParameter = entry.parameter
          }).ToArray();
        }

        var state = stateMachine.AddState(layerName + "_State");
        state.motion = blendTree;
        state.writeDefaultValues = true;
        stateMachine.defaultState = state;

        controller.AddLayer(new AnimatorControllerLayer{
          name = layerName,
          defaultWeight = 1.0f,
          stateMachine = stateMachine
        });

        Debug.Log($"Added override layer: {layerName}");
      }

      return controller;
    }

    private static Dictionary<string, int> TopologicalSortToggles(List<ToggleSpec> toggleSpecs) {
      // Get mapping from toggle parameter name to children
      Dictionary<string, HashSet<string>> graph = new Dictionary<string, HashSet<string>>();
      foreach (var toggle in toggleSpecs) {
        string paramName = toggle.GetParameterName();
        if (!graph.ContainsKey(paramName))
          graph[paramName] = new HashSet<string>();
        foreach (var dep in toggle.dependencies) {
          // Find the toggle with this dependency name
          var depToggle = toggleSpecs.FirstOrDefault(t => t.name == dep);
          if (depToggle == null) {
            throw new System.Exception($"Toggle '{toggle.name}' has dependency '{dep}' that doesn't exist");
          }
          string depParamName = depToggle.GetParameterName();
          if (!graph.ContainsKey(depParamName))
            graph[depParamName] = new HashSet<string>();
          graph[depParamName].Add(paramName);
        }
      }

      Dictionary<string, int> inDegree = new Dictionary<string, int>();
      foreach (var toggle in toggleSpecs) {
        string paramName = toggle.GetParameterName();
        inDegree[paramName] = toggle.dependencies.Count;
      }

      Dictionary<string, int> depths = new Dictionary<string, int>();
      Queue<string> queue = new Queue<string>();

      // Identify start nodes
      foreach (var pair in inDegree) {
        if (pair.Value == 0) {
          queue.Enqueue(pair.Key);
          depths[pair.Key] = 0;
        }
      }

      int processedNodes = 0;
      while (queue.Count > 0) {
        // Pop start nodes one by one.
        string current = queue.Dequeue();
        processedNodes++;
        int currentDepth = depths[current];
        // Enqueue children and set their depth to cur depth + 1.
        foreach (var child in graph[current]) {
          inDegree[child]--;
          if (inDegree[child] == 0) {
            queue.Enqueue(child);
            depths[child] = currentDepth + 1;
          }
        }
      }

      if (processedNodes != toggleSpecs.Count) {
        var cycleNodes = toggleSpecs
          .Where(t => !depths.ContainsKey(t.GetParameterName()))
          .Select(t => t.name)
          .ToList();
        throw new System.Exception($"Dependency cycle detected in toggle specifications. Nodes involved: {string.Join(", ", cycleNodes)}");
      }

      return depths;
    }

    private static GeneratedAnimatorConfig GenerateNaiveAnimatorConfig(List<ToggleSpec> toggleSpecs) {
      GeneratedAnimatorConfig genAnimatorConfig = new GeneratedAnimatorConfig();
      // Sort toggles into layers
      Dictionary<string, int> depths = TopologicalSortToggles(toggleSpecs);
      var togglesByDepth = toggleSpecs
        .GroupBy(t => depths[t.GetParameterName()])
        .OrderBy(g => g.Key)
        .ToList();
      // Add layers
      for (int i = 0; i < togglesByDepth.Count; i++) {
        var depthGroup = togglesByDepth[i];
        AnimatorLayer layer = new AnimatorLayer();
        layer.name = i == 0 ? "YOTS_BaseLayer" : $"YOTS_OverrideLayer{(i - 1).ToString("00")}";
        foreach (var toggle in depthGroup) {
          string paramName = toggle.GetParameterName();
          if (!genAnimatorConfig.parameters.Any(p => p.name == paramName))
            genAnimatorConfig.parameters.Add(new AnimatorParameterSetting{
              name = paramName,
              defaultValue = toggle.defaultValue
            });

          layer.directBlendTree.entries.Add(new AnimatorDirectBlendTreeEntry{
            name = paramName + "_On",
            parameter = paramName
          });

          layer.directBlendTree.entries.Add(new AnimatorDirectBlendTreeEntry{
            name = paramName + "_Off",
            parameter = paramName
          });
        }
        genAnimatorConfig.layers.Add(layer);
      }
      // Add animations
      GeneratedAnimationsConfig animConfig = GenerateAnimationConfig(toggleSpecs);
      genAnimatorConfig.animations = animConfig.animations;
      return genAnimatorConfig;
    }

    private static GeneratedAnimationsConfig GenerateAnimationConfig(List<ToggleSpec> toggleSpecs) {
      GeneratedAnimationsConfig genAnimConfig = new GeneratedAnimationsConfig();
      foreach (var toggle in toggleSpecs) {
        string paramName = toggle.GetParameterName();
        
        GeneratedAnimationClipConfig onAnim = new GeneratedAnimationClipConfig();
        onAnim.name = paramName + "_On";
        if (toggle.meshToggles != null) {
          foreach (var mesh in toggle.meshToggles) {
            onAnim.meshToggles.Add(new GeneratedMeshToggle { path = mesh, value = 1.0f });
          }
        }
        if (toggle.blendShapes != null) {
          foreach (var bs in toggle.blendShapes) {
            onAnim.blendShapes.Add(new GeneratedBlendShape{
              path = bs.path,
              blendShape = bs.blendShape,
              value = bs.onValue
            });
          }
        }
        // Add shader toggles
        if (toggle.shaderToggles != null) {
          foreach (var st in toggle.shaderToggles) {
            // Validate that at least one path is provided
            if (string.IsNullOrEmpty(st.path) && (st.paths == null || st.paths.Count == 0)) {
              throw new ArgumentException($"Shader toggle in '{toggle.name}' must specify either 'path' or 'paths'");
            }

            // Handle single path
            if (!string.IsNullOrEmpty(st.path)) {
              onAnim.shaderToggles.Add(new GeneratedShaderToggle {
                path = st.path,
                materialProperty = st.materialProperty,
                value = st.onValue,
                rendererType = st.rendererType
              });
            }
            // Handle multiple paths
            if (st.paths != null) {
              foreach (var path in st.paths) {
                onAnim.shaderToggles.Add(new GeneratedShaderToggle {
                  path = path,
                  materialProperty = st.materialProperty,
                  value = st.onValue,
                  rendererType = st.rendererType
                });
              }
            }
          }
        }
        // Add parent constraint weights
        if (toggle.parentConstraintWeights != null) {
          foreach (var pc in toggle.parentConstraintWeights) {
            onAnim.parentConstraintWeights.Add(new GeneratedParentConstraint {
              path = pc.path,
              value = pc.onValue
            });
          }
        }
        genAnimConfig.animations.Add(onAnim);

        GeneratedAnimationClipConfig offAnim = new GeneratedAnimationClipConfig();
        offAnim.name = paramName + "_Off";
        if (toggle.meshToggles != null) {
          foreach (var mesh in toggle.meshToggles) {
            offAnim.meshToggles.Add(new GeneratedMeshToggle { path = mesh, value = 0.0f });
          }
        }
        if (toggle.blendShapes != null) {
          foreach (var bs in toggle.blendShapes) {
            offAnim.blendShapes.Add(new GeneratedBlendShape{
              path = bs.path,
              blendShape = bs.blendShape,
              value = bs.offValue
            });
          }
        }
        // Add shader toggles
        if (toggle.shaderToggles != null) {
          foreach (var st in toggle.shaderToggles) {
            // Validate that at least one path is provided
            if (string.IsNullOrEmpty(st.path) && (st.paths == null || st.paths.Count == 0)) {
              throw new ArgumentException($"Shader toggle in '{toggle.name}' must specify either 'path' or 'paths'");
            }

            // Handle single path
            if (!string.IsNullOrEmpty(st.path)) {
              offAnim.shaderToggles.Add(new GeneratedShaderToggle {
                path = st.path,
                materialProperty = st.materialProperty,
                value = st.offValue,
                rendererType = st.rendererType
              });
            }
            // Handle multiple paths
            if (st.paths != null) {
              foreach (var path in st.paths) {
                offAnim.shaderToggles.Add(new GeneratedShaderToggle {
                  path = path,
                  materialProperty = st.materialProperty,
                  value = st.offValue,
                  rendererType = st.rendererType
                });
              }
            }
          }
        }
        // Add parent constraint weights
        if (toggle.parentConstraintWeights != null) {
          foreach (var pc in toggle.parentConstraintWeights) {
            offAnim.parentConstraintWeights.Add(new GeneratedParentConstraint {
              path = pc.path,
              value = pc.offValue
            });
          }
        }
        genAnimConfig.animations.Add(offAnim);
      }
      return genAnimConfig;
    }

    private static GeneratedAnimatorConfig ApplyIndependentFixToAnimatorConfig(GeneratedAnimatorConfig genAnimatorConfig) {
      // TODO meshToggles do not implement offValue/onValue at the JSON level,
      // so this is redundant.
      float GetOffValueForMesh(string path, List<GeneratedMeshToggle> offList) {
        var offToggle = offList?.FirstOrDefault(mt => mt.path == path);
        return offToggle != null ? offToggle.value : 0.0f;
      }

      float GetOffValueForBlend(string path, string blendShapeName, List<GeneratedBlendShape> offList) {
        var offBlend = offList?.FirstOrDefault(bs => bs.path == path && bs.blendShape == blendShapeName);
        return offBlend != null ? offBlend.value : 0.0f;
      }

      float GetOffValueForShader(string path, string materialProperty, List<GeneratedShaderToggle> offList) {
        var offShader = offList?.FirstOrDefault(st => st.path == path && st.materialProperty == materialProperty);
        return offShader != null ? offShader.value : 0.0f;
      }

      float GetOffValueForParentConstraint(string path, List<GeneratedParentConstraint> offList) {
        var offPC = offList?.FirstOrDefault(pc => pc.path == path);
        return offPC != null ? offPC.value : 0.0f;
      }

      // Create mapping from toggle name -> (on animation, off animation)
      Dictionary<string, (GeneratedAnimationClipConfig on, GeneratedAnimationClipConfig off)> toggleAnimations =
        new Dictionary<string, (GeneratedAnimationClipConfig, GeneratedAnimationClipConfig)>();
      foreach (var anim in genAnimatorConfig.animations) {
        if (anim.name.EndsWith("_On")) {
          string toggleName = anim.name.Substring(0, anim.name.LastIndexOf("_On"));
          if (!toggleAnimations.ContainsKey(toggleName))
            toggleAnimations[toggleName] = (null, null);
          var pair = toggleAnimations[toggleName];
          pair.on = anim;
          toggleAnimations[toggleName] = pair;
        }
        else if (anim.name.EndsWith("_Off")) {
          string toggleName = anim.name.Substring(0, anim.name.LastIndexOf("_Off"));
          if (!toggleAnimations.ContainsKey(toggleName))
            toggleAnimations[toggleName] = (null, null);
          var pair = toggleAnimations[toggleName];
          pair.off = anim;
          toggleAnimations[toggleName] = pair;
        }
      }

      Dictionary<string, int> toggleToLayerIndex = new Dictionary<string, int>();
      for (int i = 0; i < genAnimatorConfig.layers.Count; i++) {
        var layer = genAnimatorConfig.layers[i];
        foreach (var entry in layer.directBlendTree.entries) {
          string entryName = entry.name;
          string toggleName = entryName;
          if (toggleName.EndsWith("_On"))
            toggleName = toggleName.Substring(0, toggleName.Length - "_On".Length);
          else if (toggleName.EndsWith("_Off"))
            toggleName = toggleName.Substring(0, toggleName.Length - "_Off".Length);
          if (!toggleToLayerIndex.ContainsKey(toggleName))
            toggleToLayerIndex[toggleName] = i;
        }
      }

      // Mapping from attribute touched by animation to the set of toggles
      // which affect it.
      Dictionary<string, HashSet<string>> attributeToToggles = new Dictionary<string, HashSet<string>>();
      foreach (var kvp in toggleAnimations) {
        string toggleName = kvp.Key;
        var pair = kvp.Value;
        if (pair.on == null) continue;

        HashSet<string> attributes = new HashSet<string>();
        if (pair.on.meshToggles != null) {
          foreach (var mt in pair.on.meshToggles) {
            string attr = GetMeshToggleAttributeId(mt.path);
            attributes.Add(attr);
          }
        }
        if (pair.on.blendShapes != null) {
          foreach (var bs in pair.on.blendShapes) {
            string attr = GetBlendShapeAttributeId(bs.path, bs.blendShape);
            attributes.Add(attr);
          }
        }
        if (pair.on.shaderToggles != null) {
          foreach (var st in pair.on.shaderToggles) {
            string attr = GetShaderToggleAttributeId(st.path, st.materialProperty);
            attributes.Add(attr);
          }
        }
        // Add parent constraint attributes
        if (pair.on.parentConstraintWeights != null) {
          foreach (var pc in pair.on.parentConstraintWeights) {
            string attr = GetParentConstraintAttributeId(pc.path);
            attributes.Add(attr);
          }
        }
        foreach (var attr in attributes) {
          if (!attributeToToggles.TryGetValue(attr, out var set)) {
            set = new HashSet<string>();
            attributeToToggles[attr] = set;
          }
          set.Add(toggleName);
        }
      }

      // TODO assert that all toggles affecting the same attribute are on
      // different layers.

      List<GeneratedAnimationClipConfig> newAnimations = new List<GeneratedAnimationClipConfig>();

      AnimatorLayer baseLayer = genAnimatorConfig.layers.FirstOrDefault(l => l.name == "BaseLayer");
      if (baseLayer == null && genAnimatorConfig.layers.Count > 0)
        baseLayer = genAnimatorConfig.layers[0];

      foreach (var kvp in toggleAnimations) {
        string toggleName = kvp.Key;
        var pair = kvp.Value;
        int layerIndex = toggleToLayerIndex[toggleName];

        if (layerIndex == 0) {
          newAnimations.Add(pair.on);
          newAnimations.Add(pair.off);
          continue;
        }

        // Work out which of the animation's mesh toggles are overrides and
        // which are independent.
        List<GeneratedMeshToggle> independentMesh = new List<GeneratedMeshToggle>();
        List<GeneratedMeshToggle> dependentMesh = new List<GeneratedMeshToggle>();
        if (pair.on.meshToggles != null) {
          foreach (var mt in pair.on.meshToggles) {
            string attr = GetMeshToggleAttributeId(mt.path);
            if (attributeToToggles[attr].Count == 1)
              independentMesh.Add(mt);
            else
              dependentMesh.Add(mt);
          }
        }

        // Work out which of the animation's blendshapes are overrides and
        // which are independent.
        List<GeneratedBlendShape> independentBlend = new List<GeneratedBlendShape>();
        List<GeneratedBlendShape> dependentBlend = new List<GeneratedBlendShape>();
        if (pair.on.blendShapes != null) {
          foreach (var bs in pair.on.blendShapes) {
            string attr = GetBlendShapeAttributeId(bs.path, bs.blendShape);
            if (attributeToToggles[attr].Count == 1)
              independentBlend.Add(bs);
            else
              dependentBlend.Add(bs);
          }
        }

        // Work out which of the animation's shader toggles are overrides and which are independent
        List<GeneratedShaderToggle> independentShader = new List<GeneratedShaderToggle>();
        List<GeneratedShaderToggle> dependentShader = new List<GeneratedShaderToggle>();
        if (pair.on.shaderToggles != null) {
          foreach (var st in pair.on.shaderToggles) {
            string attr = GetShaderToggleAttributeId(st.path, st.materialProperty);
            if (attributeToToggles[attr].Count == 1)
              independentShader.Add(st);
            else
              dependentShader.Add(st);
          }
        }

        // Handle parent constraints the same way as other animated properties
        List<GeneratedParentConstraint> independentParentConstraint = new List<GeneratedParentConstraint>();
        List<GeneratedParentConstraint> dependentParentConstraint = new List<GeneratedParentConstraint>();
        if (pair.on.parentConstraintWeights != null) {
          foreach (var pc in pair.on.parentConstraintWeights) {
            string attr = GetParentConstraintAttributeId(pc.path);
            if (attributeToToggles[attr].Count == 1)
              independentParentConstraint.Add(pc);
            else
              dependentParentConstraint.Add(pc);
          }
        }

        bool hasIndependent = (independentMesh.Count > 0 || independentBlend.Count > 0 || 
                              independentShader.Count > 0 || independentParentConstraint.Count > 0);
        bool hasDependent = (dependentMesh.Count > 0 || dependentBlend.Count > 0 || 
                            dependentShader.Count > 0 || dependentParentConstraint.Count > 0);

        if (hasIndependent && hasDependent) {
          GeneratedAnimationClipConfig dependentOn = new GeneratedAnimationClipConfig();
          dependentOn.name = toggleName + "_Dependent_On";
          dependentOn.meshToggles = dependentMesh;
          dependentOn.blendShapes = dependentBlend;
          dependentOn.shaderToggles = dependentShader;
          dependentOn.parentConstraintWeights = dependentParentConstraint;

          GeneratedAnimationClipConfig dependentOff = new GeneratedAnimationClipConfig();
          dependentOff.name = toggleName + "_Dependent_Off";
          dependentOff.meshToggles = dependentMesh
            .Select(mt => new GeneratedMeshToggle{
              path = mt.path,
              value = GetOffValueForMesh(mt.path, pair.off.meshToggles)
            })
          .ToList();
          dependentOff.blendShapes = dependentBlend
            .Select(bs => new GeneratedBlendShape{
              path = bs.path,
              blendShape = bs.blendShape,
              value = GetOffValueForBlend(bs.path, bs.blendShape, pair.off.blendShapes)
            })
          .ToList();
          dependentOff.shaderToggles = dependentShader
            .Select(st => new GeneratedShaderToggle {
              path = st.path,
              materialProperty = st.materialProperty,
              value = GetOffValueForShader(st.path, st.materialProperty, pair.off.shaderToggles),
              rendererType = st.rendererType
            })
          .ToList();
          dependentOff.parentConstraintWeights = dependentParentConstraint
            .Select(pc => new GeneratedParentConstraint {
              path = pc.path,
              value = GetOffValueForParentConstraint(pc.path, pair.off.parentConstraintWeights)
            })
          .ToList();

          GeneratedAnimationClipConfig independentOn = new GeneratedAnimationClipConfig();
          independentOn.name = toggleName + "_Independent_On";
          independentOn.meshToggles = independentMesh;
          independentOn.blendShapes = independentBlend;
          independentOn.shaderToggles = independentShader;
          independentOn.parentConstraintWeights = independentParentConstraint;

          GeneratedAnimationClipConfig independentOff = new GeneratedAnimationClipConfig();
          independentOff.name = toggleName + "_Independent_Off";
          independentOff.meshToggles = independentMesh
            .Select(mt => new GeneratedMeshToggle{
              path = mt.path,
              value = GetOffValueForMesh(mt.path, pair.off.meshToggles)
            })
          .ToList();
          independentOff.blendShapes = independentBlend
            .Select(bs => new GeneratedBlendShape{
              path = bs.path,
              blendShape = bs.blendShape,
              value = GetOffValueForBlend(bs.path, bs.blendShape, pair.off.blendShapes)
            })
          .ToList();
          independentOff.shaderToggles = independentShader
            .Select(st => new GeneratedShaderToggle {
              path = st.path,
              materialProperty = st.materialProperty,
              value = GetOffValueForShader(st.path, st.materialProperty, pair.off.shaderToggles),
              rendererType = st.rendererType
            })
          .ToList();
          independentOff.parentConstraintWeights = independentParentConstraint
            .Select(pc => new GeneratedParentConstraint {
              path = pc.path,
              value = GetOffValueForParentConstraint(pc.path, pair.off.parentConstraintWeights)
            })
          .ToList();

          newAnimations.Add(dependentOn);
          newAnimations.Add(dependentOff);
          newAnimations.Add(independentOn);
          newAnimations.Add(independentOff);

          AnimatorLayer overrideLayer = genAnimatorConfig.layers[layerIndex];
          foreach (var entry in overrideLayer.directBlendTree.entries) {
            if (entry.name.StartsWith(toggleName) &&
                (entry.name.EndsWith("_On") || entry.name.EndsWith("_Off"))) {
              entry.name = entry.name.EndsWith("_On") ? toggleName + "_Dependent_On" : toggleName + "_Dependent_Off";
            }
          }

          if (baseLayer != null) {
            baseLayer.directBlendTree.entries.Add(new AnimatorDirectBlendTreeEntry{
              name = toggleName + "_Independent_On",
              parameter = toggleName
            });
            baseLayer.directBlendTree.entries.Add(new AnimatorDirectBlendTreeEntry{
              name = toggleName + "_Independent_Off",
              parameter = toggleName
            });
          }
        } else if (hasIndependent) {
          GeneratedAnimationClipConfig independentOn = new GeneratedAnimationClipConfig();
          independentOn.name = toggleName + "_Independent_On";
          independentOn.meshToggles = pair.on.meshToggles;
          independentOn.blendShapes = pair.on.blendShapes;
          independentOn.shaderToggles = pair.on.shaderToggles;
          independentOn.parentConstraintWeights = pair.on.parentConstraintWeights;
          GeneratedAnimationClipConfig independentOff = new GeneratedAnimationClipConfig();
          independentOff.name = toggleName + "_Independent_Off";
          independentOff.meshToggles = pair.off.meshToggles;
          independentOff.blendShapes = pair.off.blendShapes;
          independentOff.shaderToggles = pair.off.shaderToggles;
          independentOff.parentConstraintWeights = pair.off.parentConstraintWeights;

          newAnimations.Add(independentOn);
          newAnimations.Add(independentOff);

          AnimatorLayer overrideLayer = genAnimatorConfig.layers[layerIndex];
          overrideLayer.directBlendTree.entries.RemoveAll(e => e.name.StartsWith(toggleName));
          if (baseLayer != null) {
            baseLayer.directBlendTree.entries.Add(new AnimatorDirectBlendTreeEntry{
              name = toggleName + "_Independent_On",
              parameter = toggleName
            });
            baseLayer.directBlendTree.entries.Add(new AnimatorDirectBlendTreeEntry{
              name = toggleName + "_Independent_Off",
              parameter = toggleName
            });
          }
        } else if (hasDependent) {
          GeneratedAnimationClipConfig dependentOn = new GeneratedAnimationClipConfig();
          dependentOn.name = toggleName + "_Dependent_On";
          dependentOn.meshToggles = pair.on.meshToggles;
          dependentOn.blendShapes = pair.on.blendShapes;
          dependentOn.shaderToggles = pair.on.shaderToggles;
          dependentOn.parentConstraintWeights = pair.on.parentConstraintWeights;
          GeneratedAnimationClipConfig dependentOff = new GeneratedAnimationClipConfig();
          dependentOff.name = toggleName + "_Dependent_Off";
          dependentOff.meshToggles = pair.off.meshToggles;
          dependentOff.blendShapes = pair.off.blendShapes;
          dependentOff.shaderToggles = pair.off.shaderToggles;
          dependentOff.parentConstraintWeights = pair.off.parentConstraintWeights;

          newAnimations.Add(dependentOn);
          newAnimations.Add(dependentOff);

          AnimatorLayer overrideLayer = genAnimatorConfig.layers[layerIndex];
          foreach (var entry in overrideLayer.directBlendTree.entries) {
            if (entry.name.StartsWith(toggleName) &&
                (entry.name.EndsWith("_On") || entry.name.EndsWith("_Off"))) {
              entry.name = entry.name.EndsWith("_On") ? toggleName + "_Dependent_On" : toggleName + "_Dependent_Off";
            }
          }
        } else {
          throw new ArgumentException($"Toggle {toggleName} seems to have no animations.");
        }
      }

      genAnimatorConfig.animations = newAnimations;
      return genAnimatorConfig;
    }

    private static GeneratedAnimatorConfig
        RemoveOffAnimationsFromOverrideLayers(GeneratedAnimatorConfig config) {
      for (int i = 1; i < config.layers.Count; i++) {
        var layer = config.layers[i];
        layer.directBlendTree.entries.RemoveAll(entry => entry.name.EndsWith("_Off"));
      }
      return config;
    }

    private static GeneratedAnimatorConfig
        RemoveUnusedAnimations(GeneratedAnimatorConfig config) {
      HashSet<string> referencedAnimations = new HashSet<string>();
      foreach (var layer in config.layers) {
        foreach (var entry in layer.directBlendTree.entries)
          referencedAnimations.Add(entry.name);
      }

      config.animations = config.animations
        .Where(anim => referencedAnimations.Contains(anim.name))
        .ToList();

      return config;
    }

    private static VRCExpressionsMenu GetOrCreateSubmenu(
        VRCExpressionsMenu parentMenu,
        string submenuName) {
      // Check if submenu already exists
      foreach (var control in parentMenu.controls) {
        if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu &&
            control.name == submenuName && control.subMenu != null) {
          return control.subMenu;
        }
      }

      // Create new submenu
      var newSubmenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
      newSubmenu.name = submenuName;
      newSubmenu.controls = new List<VRCExpressionsMenu.Control>();

      var newControl = new VRCExpressionsMenu.Control{
        name = submenuName,
        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
        subMenu = newSubmenu
      };
      parentMenu.controls.Add(newControl);

      return newSubmenu;
    }

    private static void GenerateVRChatAssets(
        List<ToggleSpec> toggleSpecs,
        VRCExpressionParameters vrcParams,
        VRCExpressionsMenu vrcMenu
        ) {
      var uniqueToggles = toggleSpecs
        .Where(t => t.GetParameterName() != "YOTS_Weight")
        .GroupBy(t => t.GetParameterName())
        .Select(g => g.First())
        .ToList();

      // Update parameters
      var paramList = new List<VRCExpressionParameters.Parameter>();
      paramList.AddRange(vrcParams.parameters.Where(p => !uniqueToggles.Any(t => t.GetParameterName() == p.name)));
      foreach (var toggle in uniqueToggles) {
        string paramName = toggle.GetParameterName();
        paramList.Add(new VRCExpressionParameters.Parameter{
          name = paramName,
          valueType = toggle.type == "radial" ? VRCExpressionParameters.ValueType.Float : VRCExpressionParameters.ValueType.Bool,
          defaultValue = toggle.defaultValue,
          saved = toggle.saved,
          networkSynced = toggle.synced
        });
      }
      vrcParams.parameters = paramList.ToArray();

      // Add toggles to menu (skipping those with disableMenuEntry=true)
      foreach (var toggle in toggleSpecs) {
        // Skip creating menu entries for toggles with disableMenuEntry=true
        if (toggle.disableMenuEntry)
          continue;

        VRCExpressionsMenu currentMenu = vrcMenu;

        // Navigate or create menu path if specified
        if (!string.IsNullOrEmpty(toggle.menuPath)) {
          string trimmedPath = toggle.menuPath.Trim('/');
          if (!string.IsNullOrEmpty(trimmedPath)) {
            var sections = trimmedPath.Split('/');
            foreach (var section in sections) {
              currentMenu = GetOrCreateSubmenu(currentMenu, section);
            }
          }
        }

        // Add toggle control - use toggle.name for display but paramName for the parameter
        string paramName = toggle.GetParameterName();
        if (toggle.type == "radial") {
          currentMenu.controls.Add(new VRCExpressionsMenu.Control{
            name = toggle.name,
            type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
            subParameters = new VRCExpressionsMenu.Control.Parameter[]{
              new VRCExpressionsMenu.Control.Parameter { name = paramName }
            }
          });
        } else {
          currentMenu.controls.Add(new VRCExpressionsMenu.Control{
            name = toggle.name,
            type = VRCExpressionsMenu.Control.ControlType.Toggle,
            parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName },
            value = 1f
          });
        }
      }
    }
  }
}

#endif  // UNITY_EDITOR
