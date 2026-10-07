using System;
using UnityEngine;

namespace UnityAI.Verification
{
    [Serializable]
    public sealed class VerificationAnomaly
    {
        public string Id;
        public string Type;
        public string Severity;
        public string ObjectId;
        public string ObjectPath;
        public string Message;

        public float Confidence;
        public double FirstSeen;
        public double LastSeen;

        public Vector3 Position;
        public Vector3 Velocity;

        public bool GroundDetected;
        public bool ColliderPresent;
        public bool RigidbodyPresent;

        public static VerificationAnomaly Create(
            string id,
            string type,
            string severity,
            RuntimeSnapshot snapshot,
            string message,
            float confidence)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
            return new VerificationAnomaly
            {
                Id = id,
                Type = type,
                Severity = severity,
                ObjectId = snapshot != null ? snapshot.ObjectId : string.Empty,
                ObjectPath = snapshot != null ? snapshot.ObjectPath : string.Empty,
                Message = message,
                Confidence = confidence,
                FirstSeen = now,
                LastSeen = now,
                Position = snapshot != null ? snapshot.Position : Vector3.zero,
                Velocity = snapshot != null ? snapshot.Velocity : Vector3.zero,
                ColliderPresent = snapshot != null && snapshot.HasCollider,
                RigidbodyPresent = snapshot != null && snapshot.HasRigidbody
            };
        }
    }
}
