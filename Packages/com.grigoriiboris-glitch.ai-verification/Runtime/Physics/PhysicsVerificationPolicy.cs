using System;
using UnityEngine;

namespace UnityAI.Verification
{
    [Serializable]
    public sealed class PhysicsVerificationPolicy
    {
        public LayerMask GroundMask = ~0;
        public float GroundProbeDistance = 0.25f;
        public bool RequireCollider = true;
        public bool RequireRigidbody = true;
        public bool RejectTriggerCollider;
        public bool RequireLayerCollision = true;
        public bool DetectPenetration = true;
    }
}
