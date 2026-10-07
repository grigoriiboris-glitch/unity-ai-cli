using System.Collections.Generic;
using UnityEngine;

namespace UnityAI.Verification
{
    public sealed class TemporalMonitor
    {
        private readonly string _objectId;
        private readonly string _objectPath;
        private readonly TemporalMonitorPolicy _policy;
        private readonly List<TemporalSample> _samples = new();

        public TemporalMonitor(string objectId, string objectPath, TemporalMonitorPolicy policy = null)
        {
            _objectId = objectId ?? string.Empty;
            _objectPath = objectPath ?? string.Empty;
            _policy = policy ?? new TemporalMonitorPolicy();
        }

        public int SampleCount => _samples.Count;

        public void AddSample(TemporalSample sample)
        {
            _samples.Add(sample);
            var maxSamples = Mathf.Max(2, _policy.MaxSamples);
            if (_samples.Count > maxSamples)
                _samples.RemoveAt(0);
        }

        public VerificationResult Evaluate(string taskId = null, string runId = null)
        {
            var result = new VerificationResult
            {
                TaskId = taskId,
                RunId = runId
            };

            if (_samples.Count < 2)
                return result;

            DetectTeleport(result);
            DetectDisappearance(result);
            DetectStuck(result);
            DetectFallThrough(result);

            return result;
        }

        public void Clear() => _samples.Clear();

        private void DetectTeleport(VerificationResult result)
        {
            for (var i = 1; i < _samples.Count; i++)
            {
                var previous = _samples[i - 1];
                var current = _samples[i];
                var distance = Vector3.Distance(previous.Position, current.Position);

                if (distance > _policy.TeleportDistanceThreshold)
                {
                    result.Add(Create(
                        "TEMP-001",
                        "teleport",
                        "error",
                        current,
                        $"Position delta {distance:0.###} exceeded threshold {_policy.TeleportDistanceThreshold:0.###}.",
                        0.98f));
                    return;
                }
            }
        }

        private void DetectDisappearance(VerificationResult result)
        {
            var wasVisible = false;
            for (var i = 0; i < _samples.Count; i++)
            {
                var sample = _samples[i];
                if (sample.Active && sample.RendererEnabled)
                {
                    wasVisible = true;
                    continue;
                }

                if (wasVisible)
                {
                    result.Add(Create(
                        "TEMP-002",
                        "disappearance",
                        "error",
                        sample,
                        "Object changed from active/renderable state to inactive or non-rendering state.",
                        0.96f));
                    return;
                }
            }
        }

        private void DetectStuck(VerificationResult result)
        {
            for (var start = 0; start < _samples.Count; start++)
            {
                var first = _samples[start];
                if (!first.MovementExpected)
                    continue;

                for (var end = start + 1; end < _samples.Count; end++)
                {
                    var last = _samples[end];
                    if (last.Time - first.Time < _policy.StuckDurationSeconds)
                        continue;

                    var displacement = Vector3.Distance(first.Position, last.Position);
                    if (displacement <= _policy.StuckDisplacementThreshold)
                    {
                        result.Add(Create(
                            "TEMP-003",
                            "stuck",
                            "error",
                            last,
                            $"Object moved only {displacement:0.###} over {last.Time - first.Time:0.###} seconds while movement was expected.",
                            0.91f));
                        return;
                    }

                    break;
                }
            }
        }

        private void DetectFallThrough(VerificationResult result)
        {
            if (!_policy.ExpectedGround)
                return;

            var groundedIndex = -1;
            for (var i = 0; i < _samples.Count; i++)
            {
                if (_samples[i].GroundDetected)
                {
                    groundedIndex = i;
                    break;
                }
            }

            if (groundedIndex < 0 || _samples.Count - groundedIndex < 3)
                return;

            var descendingSteps = 0;
            var totalDrop = 0f;
            for (var i = groundedIndex + 1; i < _samples.Count; i++)
            {
                var previous = _samples[i - 1];
                var current = _samples[i];

                if (current.Position.y < previous.Position.y)
                {
                    descendingSteps++;
                    totalDrop += previous.Position.y - current.Position.y;
                }
                else
                {
                    break;
                }

                if (descendingSteps >= 2 &&
                    totalDrop >= _policy.FallDistanceThreshold &&
                    !current.GroundDetected)
                {
                    result.Add(Create(
                        "TEMP-004",
                        "fall_through_ground",
                        "error",
                        current,
                        $"Y decreased continuously by {totalDrop:0.###} after ground contact was lost.",
                        0.97f));
                    return;
                }
            }
        }

        private VerificationAnomaly Create(
            string id,
            string type,
            string severity,
            TemporalSample sample,
            string message,
            float confidence)
        {
            return new VerificationAnomaly
            {
                Id = id,
                Type = type,
                Severity = severity,
                ObjectId = _objectId,
                ObjectPath = _objectPath,
                Message = message,
                Confidence = confidence,
                FirstSeen = sample.Time,
                LastSeen = sample.Time,
                Position = sample.Position,
                Velocity = sample.Velocity,
                GroundDetected = sample.GroundDetected
            };
        }
    }
}
