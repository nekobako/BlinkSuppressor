#if BS_VRCSDK3_AVATARS

using UnityEngine;
using VRC.SDKBase;

namespace net.nekobako.BlinkSuppressor.Runtime
{
    [DisallowMultipleComponent]
    internal class BlinkSuppressor : MonoBehaviour, IEditorOnly
    {
        [Header("Property to change with animations")]
        public bool SuppressBlink = false;

        [Header("Settings")]
        public float BlendShapeThreshold = Vector3.kEpsilon;
        public Vector3 DeltaPosition = Vector3.back * 0.05f;
    }
}

#endif
