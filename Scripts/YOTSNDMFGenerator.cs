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
  public class YOTSNDMFGenerator : Plugin<YOTSNDMFGenerator>
  {
    private readonly Localizer localizer = new Localizer("en-us", () =>
        new List<(string, Func<string, string>)> {
        ("en-us", key => key)
        });

    public override string DisplayName => "YOTS Animator Generator";

    protected override void Configure()
    {
      // First pass: Retrieve and stash configuration
      InPhase(BuildPhase.Resolving)
        .Run("Cache YOTS Config", ctx => {
          var config = ctx.AvatarRootObject.GetComponentInChildren<YOTSNDMFConfig>();
          if (config == null || config.jsonConfig == null) {
            ErrorReport.WithContextObject(ctx.AvatarRootObject, () => {
                ErrorReport.ReportException(
                    new Exception("No YOTS config found"), 
                    "Missing required YOTS configuration"
                );
            });
            return;
          }
          ctx.GetState<YOTSBuildState>().jsonConfig = config.jsonConfig.text;
        })
        // Shoutsout anatawa12/AvatarOptimizer
        .BeforePass(RemoveEditorOnlyPass.Instance);

      // Second pass: Generate and merge animator
      InPhase(BuildPhase.Transforming)
        .Run("Generate YOTS Animator", ctx => {
          var config = ctx.GetState<YOTSBuildState>();
          if (config == null) {
            ErrorReport.WithContextObject(ctx.AvatarRootObject, () => {
                ErrorReport.ReportException(
                    new Exception("No YOTS config component found"), 
                    "Missing required YOTS configuration"
                );
            });
            return;
          }
          if (config.jsonConfig == null) {
            ErrorReport.WithContextObject(ctx.AvatarRootObject, () => {
                ErrorReport.ReportException(
                    new Exception("Missing JSON config file"), 
                    "YOTS config component is missing required JSON configuration"
                );
            });
            return;
          }

          // Get menu and parameters
          var descriptor = ctx.AvatarDescriptor;
          if (descriptor == null) {
            ErrorReport.WithContextObject(ctx.AvatarRootObject, () => {
                ErrorReport.ReportException(
                    new Exception("Avatar descriptor is missing"), 
                    "Cannot find VRC Avatar Descriptor"
                );
            });
            return;
          }
          RuntimeAnimatorController originalAnimator = descriptor.baseAnimationLayers[4].animatorController;
          var menu = descriptor.expressionsMenu;
          var parameters = descriptor.expressionParameters;
          if (parameters == null || menu == null)
          {
            ErrorReport.WithContextObject(descriptor, () => {
                ErrorReport.ReportException(
                    new Exception("Missing required VRC assets"), 
                    "Avatar is missing required Expression Parameters or Menu"
                );
            });
            return;
          }
          // Create copies so the originals don't get modified
          menu = UnityEngine.Object.Instantiate(menu);
          parameters = UnityEngine.Object.Instantiate(parameters);
          descriptor.expressionsMenu = menu;
          descriptor.expressionParameters = parameters;

          // Generate the YOTS animator.
          RuntimeAnimatorController generatedAnimator = YOTSCore.GenerateAnimator(
              config.jsonConfig,
              parameters,
              menu
          );
          if (generatedAnimator == null) {
            ErrorReport.WithContextObject(ctx.AvatarRootObject, () => {
                ErrorReport.ReportException(
                    new Exception("Failed to generate animator"), 
                    "YOTS animator generation failed"
                );
            });
            return;
          }

          // If no original animator, just assign the generated one.
          if (originalAnimator == null)
          {
            descriptor.baseAnimationLayers[4].animatorController = generatedAnimator;
            return;
          }
          // Else append the generated animator to the original.
          AnimatorController originalController = originalAnimator as AnimatorController;
          AnimatorController generatedController = generatedAnimator as AnimatorController;
          MergeAnimatorControllers(localizer, originalController, generatedController);
          descriptor.baseAnimationLayers[4].animatorController = originalController;
        });
    }

    // Simply append generated params and layers to the original animator.
    private static void MergeAnimatorControllers(Localizer localizer, AnimatorController original, AnimatorController generated)
    {
      // Merge parameters from generated into original.
      foreach (var genParam in generated.parameters)
      {
        // This is an O(m*n) check but m and n should be small enough to not matter.
        if (original.parameters.Any(p => p.name == genParam.name))
        {
          ErrorReport.WithContextObject(original, () => {
              ErrorReport.ReportException(
                  new Exception($"Parameter '{genParam.name}' already exists"), 
                  "Parameter name conflict in animator"
              );
          });
          return;
        }
        original.AddParameter(genParam);
      }

      // Append each YOTS layer after the original layers.
      foreach (var genLayer in generated.layers)
      {
        // This isn't strictly an error but if someone already has layers named
        // YOTS_* that's probably not on purpose.
        if (original.layers.Any(l => l.name == genLayer.name))
        {
          ErrorReport.WithContextObject(original, () => {
              ErrorReport.ReportException(
                  new Exception($"Layer '{genLayer.name}' already exists"), 
                  "Layer name conflict in animator"
              );
          });
          return;
        }
        var newLayer = new AnimatorControllerLayer
        {
          name = genLayer.name,
          defaultWeight = genLayer.defaultWeight,
          stateMachine = genLayer.stateMachine
        };
        original.AddLayer(newLayer);
      }
    }

    private class YOTSBuildState
    {
      public string jsonConfig;
    }
  }
}

#endif  // UNITY_EDITOR
