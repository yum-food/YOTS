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
    private readonly Localizer localizer = new Localizer("en-us", () => new List<(string, Func<string, string>)> 
        {
        ("en-us", key => key) // Simple pass-through for English
        });

    public override string DisplayName => "YOTS Animator Generator";

    protected override void Configure()
    {
      // First pass: Store config data
      InPhase(BuildPhase.Resolving)
        .Run("Cache YOTS Config", ctx => {
          var config = ctx.AvatarRootObject.GetComponentInChildren<YOTSNDMFConfig>();
          if (config == null || config.jsonConfig == null) {
            ErrorReport.ReportError(localizer, ErrorSeverity.Error, "yots.error.no_config", 
                "No YOTS config found on the avatar.");
            return;
          }
          ctx.GetState<YOTSBuildState>().jsonConfig = config.jsonConfig.text;
        })
        // Shoutsout anatawa12/AvatarOptimizer
        .BeforePass(RemoveEditorOnlyPass.Instance);

      // Second pass: Generate animator and merge with the original
      InPhase(BuildPhase.Transforming)
        .Run("Generate YOTS Animator", ctx => {
          var state = ctx.GetState<YOTSBuildState>();
          if (string.IsNullOrEmpty(state.jsonConfig)) {
            ErrorReport.ReportError(localizer, ErrorSeverity.Error, "yots.error.no_config", 
                "No YOTS config found on the avatar.");
            return;
          }

          var config = ctx.GetState<YOTSBuildState>();
          if (config == null) {
            ErrorReport.ReportError(localizer, ErrorSeverity.Error, "yots.error.no_config", 
                "No YOTS config component found on the avatar.");
            return;
          }
          if (config.jsonConfig == null) {
            ErrorReport.ReportError(localizer, ErrorSeverity.Error, "yots.error.no_json", 
                "YOTS config component is missing JSON config file.");
            return;
          }

          // Get menu and parameters
          var descriptor = ctx.AvatarDescriptor;
          if (descriptor == null) {
            ErrorReport.ReportError(localizer, ErrorSeverity.Error, "yots.error.no_descriptor", 
                "Avatar descriptor is missing.");
            return;
          }
          RuntimeAnimatorController originalAnimator = descriptor.baseAnimationLayers[4].animatorController;
          var menu = descriptor.expressionsMenu;
          var parameters = descriptor.expressionParameters;
          if (parameters == null || menu == null)
          {
            ErrorReport.ReportError(localizer, ErrorSeverity.Error, "yots.error.missing_assets", 
                "Avatar parameters or menu is missing.");
            return;
          }
          // TODO do we need to make copies?
          menu = UnityEngine.Object.Instantiate(menu);
          parameters = UnityEngine.Object.Instantiate(parameters);
          descriptor.expressionsMenu = menu;
          descriptor.expressionParameters = parameters;

          // Generate the YOTS animator.
          RuntimeAnimatorController generatedAnimator = YOTSCore.GenerateAnimator(
              state.jsonConfig,
              parameters,
              menu
          );
          if (generatedAnimator == null) {
              ErrorReport.ReportError(localizer, ErrorSeverity.Error, "yots.error.generation_failed", 
                  "Failed to generate animator.");
              return;
          }

          if (originalAnimator == null)
          {
            descriptor.baseAnimationLayers[4].animatorController = generatedAnimator;
            return;
          }

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
          ErrorReport.ReportError(localizer, ErrorSeverity.Error, "yots.error.parameter_conflict",
              $"Parameter '{genParam.name}' already exists in the original animator.");
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
          ErrorReport.ReportError(localizer, ErrorSeverity.Error, "yots.error.layer_conflict",
              $"Layer with name '{genLayer.name}' already exists in the original animator.");
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
