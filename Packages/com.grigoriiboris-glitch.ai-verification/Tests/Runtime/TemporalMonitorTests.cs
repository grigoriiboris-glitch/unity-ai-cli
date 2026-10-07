using NUnit.Framework;
using UnityEngine;

namespace UnityAI.Verification.Tests
{
    public sealed class TemporalMonitorTests
    {
        [Test]
        public void Teleport_IsDetected()
        {
            var monitor = new TemporalMonitor("Player", "Root/Player");

            monitor.AddSample(new TemporalSample(0d, Vector3.zero, Vector3.zero, true, true, true, false));
            monitor.AddSample(new TemporalSample(0.2d, new Vector3(10f, 0f, 0f), Vector3.zero, true, true, true, false));

            var result = monitor.Evaluate();

            Assert.That(result.Anomalies.Exists(a => a.Id == "TEMP-001"), Is.True);
        }

        [Test]
        public void FallThrough_IsDetected()
        {
            var monitor = new TemporalMonitor("Player", "Player");

            monitor.AddSample(new TemporalSample(0d, new Vector3(0f, 1f, 0f), Vector3.zero, true, true, true, false));
            monitor.AddSample(new TemporalSample(0.2d, new Vector3(0f, 0.8f, 0f), new Vector3(0f, -1f, 0f), true, true, false, false));
            monitor.AddSample(new TemporalSample(0.4d, new Vector3(0f, 0.4f, 0f), new Vector3(0f, -2f, 0f), true, true, false, false));

            var result = monitor.Evaluate();

            Assert.That(result.Anomalies.Exists(a => a.Id == "TEMP-004"), Is.True);
        }

        [Test]
        public void Disappearance_IsDetected()
        {
            var monitor = new TemporalMonitor("Player", "Player");

            monitor.AddSample(new TemporalSample(0d, Vector3.zero, Vector3.zero, true, true, false, false));
            monitor.AddSample(new TemporalSample(0.2d, Vector3.zero, Vector3.zero, false, false, false, false));

            var result = monitor.Evaluate();

            Assert.That(result.Anomalies.Exists(a => a.Id == "TEMP-002"), Is.True);
        }
    }
}
