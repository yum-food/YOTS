using UnityEngine;
using nadena.dev.ndmf;
using nadena.dev.ndmf.VRChat;
using nadena.dev.ndmf.localization;
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
      // Use a different pass name in play mode to indicate temporary processing.
      InPhase(BuildPhase.Transforming)
        .Run("Generate YOTS Animator", ctx => {
          // ctx is a BuildContext
          // https://ndmf.nadena.dev/api/nadena.dev.ndmf.BuildContext.html
          // Get config
          var config = ctx.AvatarRootObject.GetComponentInChildren<YOTSNDMFConfig>();
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
              config.jsonConfig.text,
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
  }
}
