using System;
using UnityEngine;

namespace UnityAI.Verification
{
    [Serializable]
    public sealed class BoundsVerificationPolicy
    {
        public bool Enabled = true;
        public Vector3 Min = new(-1000f, -1000f, -1000f);
        public Vector3 Max = new(1000f, 1000f, 1000f);

        public bool Contains(Bounds bounds)
        {
            return bounds.min.x >= Min.x &&
                   bounds.max.x <= Max.x &&
                   bounds.min.y >= Min.y &&
                   bounds.max.y <= Max.y &&
                   bounds.min.z >= Min.z &&
                   bounds.max.z <= Max.z;
        }
    }
}
