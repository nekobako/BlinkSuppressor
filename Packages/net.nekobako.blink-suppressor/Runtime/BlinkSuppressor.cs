using UnityEngine;
using nadena.dev.ndmf;

namespace net.nekobako.BlinkSuppressor.Runtime
{
    [DisallowMultipleComponent]
    internal class BlinkSuppressor : MonoBehaviour, INDMFEditorOnly
    {
        [Header("Property to animate")]
        public bool SuppressBlink = false;

        [Header("Settings")]
        public float BlendShapeThreshold = Vector3.kEpsilon;
    }
}
