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

      // Second pass: Generate animator
      InPhase(BuildPhase.Transforming)
        .Run("Generate YOTS Animator", ctx => {
          var state = ctx.GetState<YOTSBuildState>();
          if (string.IsNullOrEmpty(state.jsonConfig)) {
            ErrorReport.ReportError(localizer, ErrorSeverity.Error, "yots.error.no_config", 
                "No YOTS config found on the avatar.");
            return;
          }
          // Get config
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
          // Get descriptor
          var descriptor = ctx.AvatarDescriptor;
          if (descriptor == null) {
            ErrorReport.ReportError(localizer, ErrorSeverity.Error, "yots.error.no_descriptor", 
                "Avatar descriptor is missing.");
            return;
          }
          RuntimeAnimatorController animator = descriptor.baseAnimationLayers[4].animatorController;
          if (animator == null) {
            ErrorReport.ReportError(localizer, ErrorSeverity.Error, "yots.error.no_animator", 
                "FX layer is missing.");
            return;
          }
          var menu = descriptor.expressionsMenu;
          var parameters = descriptor.expressionParameters;
          if (parameters == null || menu == null)
          {
            ErrorReport.ReportError(localizer, ErrorSeverity.Error, "yots.error.missing_assets", 
                "Avatar parameters or menu is missing.");
            return;
          }
          menu = UnityEngine.Object.Instantiate(menu);
          parameters = UnityEngine.Object.Instantiate(parameters);
          descriptor.expressionsMenu = menu;
          descriptor.expressionParameters = parameters;
          RuntimeAnimatorController generatedAnimator = YOTSCore.GenerateAnimator(
              state.jsonConfig,
              parameters,
              menu
          );
          if (generatedAnimator == null) {
              ErrorReport.ReportError(localizer, ErrorSeverity.Error, "yots.error.generation_failed", 
                  "Failed to generate animator controller.");
              return;
          }
          // TODO merge animators
          descriptor.baseAnimationLayers[4].animatorController = generatedAnimator;
        }
      );
    }

    private class YOTSBuildState
    {
      public string jsonConfig;
    }
  }
}

#endif  // UNITY_EDITOR
