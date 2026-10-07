using System;
using UnityEngine;

namespace UnityAI.Verification
{
    [Serializable]
    public readonly struct TemporalSample
    {
        public readonly double Time;
        public readonly Vector3 Position;
        public readonly Vector3 Velocity;
        public readonly bool Active;
        public readonly bool RendererEnabled;
        public readonly bool GroundDetected;
        public readonly bool MovementExpected;

        public TemporalSample(
            double time,
            Vector3 position,
            Vector3 velocity,
            bool active,
            bool rendererEnabled,
            bool groundDetected,
            bool movementExpected)
        {
            Time = time;
            Position = position;
            Velocity = velocity;
            Active = active;
            RendererEnabled = rendererEnabled;
            GroundDetected = groundDetected;
            MovementExpected = movementExpected;
        }
    }
}
