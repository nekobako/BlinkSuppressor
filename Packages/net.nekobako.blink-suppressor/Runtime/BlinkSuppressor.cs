using nadena.dev.ndmf;
using UnityEngine;
using UnityEngine.Animations;

namespace net.nekobako.BlinkSuppressor.Runtime
{
    [DisallowMultipleComponent]
    internal class BlinkSuppressor : MonoBehaviour, INDMFEditorOnly
    {
        [SerializeField]
        public bool SuppressBlink = false;

        [SerializeField, NotKeyable]
        public float BlendShapeThreshold = Vector3.kEpsilon;
    }
}
