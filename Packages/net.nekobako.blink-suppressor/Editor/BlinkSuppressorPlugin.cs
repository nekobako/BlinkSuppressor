#if BS_VRCSDK3_AVATARS

using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using net.nekobako.BlinkSuppressor.Editor;

[assembly: ExportsPlugin(typeof(BlinkSuppressorPlugin))]

namespace net.nekobako.BlinkSuppressor.Editor
{
    internal class BlinkSuppressorPlugin : Plugin<BlinkSuppressorPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .WithRequiredExtension(typeof(AnimatorServicesContext), s => s.Run(BlinkSuppressorPass.Instance));
        }
    }
}

#endif
