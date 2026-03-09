#if UNITY_EDITOR

using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.builtin;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.VRChat;
using VRC.SDK3.Avatars.ScriptableObjects;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;

[assembly: ExportsPlugin(typeof(YOTS.YOTSNDMFGenerator))]

namespace YOTS
{
  public class YOTSNDMFGenerator : Plugin<YOTSNDMFGenerator> {
    private readonly Localizer lcl = new Localizer("en-us", () =>
        new List<(string, Func<string, string>)> {
        ("en-us", key => {
          switch (key) {
            case "json_missing": return "YOTS configuration JSON file is missing";
            case "config_missing": return "YOTS config component not found on avatar";
            case "descriptor_missing": return "VRC Avatar Descriptor is missing from avatar";
            case "expressions_missing": return "Avatar is missing Expression Parameters or Expression Menu";
            case "param_exists": return "Parameter '{0}' already exists in FX animator";
            case "layer_exists": return "Layer '{0}' already exists in FX animator";
            case "config_error": return "{0}";
            default: return null;
          }
        })
        });

    public override string DisplayName => "YOTS Animator Generator";

    protected override void Configure() {
      // First pass: Retrieve and stash configuration. By the time we're in the
      // Transforming phase, we can no longer access the YOTSConfig object.
      InPhase(BuildPhase.Resolving)
        .Run("Cache YOTS Config", ctx => {
          var configs = ctx.AvatarRootObject.GetComponentsInChildren<YOTSConfig>();
          if (configs.Length == 0) {
            ctx.GetState<YOTSBuildState>().skipGeneration = true;
            Debug.Log("No YOTS config found - skipping.");
            return;
          }
          var mergedConfig = new AnimatorConfigFile();
          foreach (var config in configs) {
            if (config.jsonConfig == null) {
              ctx.GetState<YOTSBuildState>().skipGeneration = true;
              ErrorReport.ReportError(lcl, ErrorSeverity.Error, "json_missing",
                  config.gameObject);
              return;
            }
            var parsed = JsonUtility.FromJson<AnimatorConfigFile>(config.jsonConfig.text);
            mergedConfig.toggles.AddRange(parsed.toggles);
            if (parsed.api_version != null) {
              mergedConfig.api_version = parsed.api_version;
            }
          }
          ctx.GetState<YOTSBuildState>().jsonConfig = JsonUtility.ToJson(mergedConfig);
        })
        // Shoutsout anatawa12/AvatarOptimizer
        .BeforePass(RemoveEditorOnlyPass.Instance);

      // Second pass: Generate and merge animator
      InPhase(BuildPhase.Transforming)
        .Run("Generate YOTS Animator", ctx => {
          var config = ctx.GetState<YOTSBuildState>();
          if (config.skipGeneration) {
            return;
          }
          if (config == null) {
            ErrorReport.ReportError(lcl, ErrorSeverity.Error, "config_missing",
                ctx.AvatarRootObject);
            return;
          }
          if (config.jsonConfig == null) {
            ErrorReport.ReportError(lcl, ErrorSeverity.Error, "json_missing",
                ctx.AvatarRootObject);
            return;
          }

          // Get menu and parameters
          var descriptor = ctx.AvatarDescriptor;
          if (descriptor == null) {
            ErrorReport.ReportError(lcl, ErrorSeverity.Error, "descriptor_missing",
                ctx.AvatarRootObject);
            return;
          }
          RuntimeAnimatorController originalAnimator = descriptor.baseAnimationLayers[4].animatorController;
          var menu = descriptor.expressionsMenu;
          var parameters = descriptor.expressionParameters;
          if (parameters == null || menu == null) {
            ErrorReport.ReportError(lcl, ErrorSeverity.Error, "expressions_missing",
                descriptor);
            return;
          }
          // Create copies so the originals don't get modified
          menu = DeepCopyMenu(menu);
          parameters = UnityEngine.Object.Instantiate(parameters);
          descriptor.expressionsMenu = menu;
          descriptor.expressionParameters = parameters;

          // Resolve bare names to full hierarchy paths.
          var resolvedJson = ResolveNames(config.jsonConfig, ctx.AvatarRootObject.transform);

          // Generate the YOTS animator.
          RuntimeAnimatorController generatedAnimator = null;
          try {
            generatedAnimator = YOTSCore.GenerateAnimator(
                resolvedJson,
                parameters,
                menu
            );
          } catch (ArgumentException e) {
            ErrorReport.ReportError(lcl, ErrorSeverity.Error, "config_error",
                e.Message, ctx.AvatarRootObject);
            return;
          } catch (Exception e) {
            ErrorReport.ReportException(e);
            return;
          }

          // If no original animator, just assign the generated one.
          if (originalAnimator == null) {
            descriptor.baseAnimationLayers[4].animatorController = generatedAnimator;
            return;
          }
          // Else append the generated animator to the original.
          AnimatorController originalController = originalAnimator as AnimatorController;
          AnimatorController generatedController = generatedAnimator as AnimatorController;
          MergeAnimatorControllers(originalController, generatedController);
          descriptor.baseAnimationLayers[4].animatorController = generatedController;
        });
    }

    // Simply append generated params and layers to the original animator.
    private void MergeAnimatorControllers(AnimatorController from, AnimatorController to) {
      // Merge parameters from from into to.
      foreach (var genParam in from.parameters) {
        // This is an O(m*n) check but m and n should be small enough to not matter.
        if (to.parameters.Any(p => p.name == genParam.name)) {
          ErrorReport.ReportError(lcl, ErrorSeverity.Error,
              "param_exists",
              genParam.name, to);
          return;
        }
        to.AddParameter(genParam);
      }

      // Append each YOTS layer after the to layers.
      foreach (var genLayer in from.layers) {
        // This isn't strictly an error but if someone already has layers named
        // YOTS_* that's probably not on purpose.
        if (to.layers.Any(l => l.name == genLayer.name)) {
          ErrorReport.ReportError(lcl, ErrorSeverity.Error,
              "layer_exists",
              genLayer.name, to);
          return;
        }
        var newLayer = new AnimatorControllerLayer {
          name = genLayer.name,
          defaultWeight = genLayer.defaultWeight,
          stateMachine = genLayer.stateMachine
        };
        to.AddLayer(newLayer);
      }
    }

    private class YOTSBuildState {
      public string jsonConfig;
      public bool skipGeneration;
    }

    // Resolve bare names (no '/') to full hierarchy paths. Names containing
    // '/' are kept as explicit paths.
    private static string ResolveNames(string jsonConfig, Transform avatarRoot) {
      var config = JsonUtility.FromJson<AnimatorConfigFile>(jsonConfig);
      var nameToPathsMap = BuildNameToPathsMap(avatarRoot);
      foreach (var toggle in config.toggles) {
        toggle.meshToggles = ExpandNames(toggle.meshToggles, nameToPathsMap);
        toggle.inverseMeshToggles = ExpandNames(toggle.inverseMeshToggles, nameToPathsMap);
        foreach (var bs in toggle.blendShapes) {
          bs.path = ExpandName(bs.path, nameToPathsMap);
          bs.paths = ExpandNames(bs.paths, nameToPathsMap);
        }
        foreach (var st in toggle.shaderToggles) {
          st.path = ExpandName(st.path, nameToPathsMap);
          st.paths = ExpandNames(st.paths, nameToPathsMap);
        }
      }
      return JsonUtility.ToJson(config);
    }

    private static string ExpandName(string name, Dictionary<string, List<string>> nameToPathsMap) {
      if (string.IsNullOrEmpty(name) || name.Contains('/')) return name;
      if (nameToPathsMap.TryGetValue(name, out var paths) && paths.Count == 1) return paths[0];
      return name;
    }

    private static List<string> ExpandNames(List<string> names, Dictionary<string, List<string>> nameToPathsMap) {
      if (names == null) return null;
      var resolved = new List<string>();
      foreach (var name in names) {
        if (name.Contains('/')) {
          resolved.Add(name);
        } else if (nameToPathsMap.TryGetValue(name, out var paths)) {
          resolved.AddRange(paths);
        } else {
          resolved.Add(name);
        }
      }
      return resolved;
    }

    private static Dictionary<string, List<string>> BuildNameToPathsMap(Transform root) {
      var map = new Dictionary<string, List<string>>();
      CollectPaths(root, "", map);
      return map;
    }

    private static void CollectPaths(Transform current, string currentPath, Dictionary<string, List<string>> map) {
      foreach (Transform child in current) {
        string childPath = currentPath == "" ? child.name : currentPath + "/" + child.name;
        if (!map.ContainsKey(child.name))
          map[child.name] = new List<string>();
        map[child.name].Add(childPath);
        CollectPaths(child, childPath, map);
      }
    }

    private static VRCExpressionsMenu DeepCopyMenu(VRCExpressionsMenu sourceMenu) {
        var copiedMenu = UnityEngine.Object.Instantiate(sourceMenu);
        // Deep copy all submenu references
        for (int i = 0; i < copiedMenu.controls.Count; i++) {
            var control = copiedMenu.controls[i];
            if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu) {
                control.subMenu = DeepCopyMenu(control.subMenu);
            }
        }
        return copiedMenu;
    }
  }
}

#endif  // UNITY_EDITOR
