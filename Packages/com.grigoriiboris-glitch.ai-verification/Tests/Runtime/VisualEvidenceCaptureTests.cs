using NUnit.Framework;
using UnityEngine;

namespace UnityAI.Verification.Tests
{
    public sealed class VisualEvidenceCaptureTests
    {
        [Test]
        public void Request_NormalizesResolutionAndOutputLimit()
        {
            var request = new VisualCaptureRequest
            {
                Resolution = 1,
                MaxBytes = 1
            };

            request.Normalize();

            Assert.That(request.Resolution, Is.EqualTo(64));
            Assert.That(request.MaxBytes, Is.EqualTo(64 * 1024));
        }

        [Test]
        public void Capture_RejectsMissingCamera()
        {
            var result = VisualEvidenceCapture.Capture(
                null,
                new VisualCaptureRequest(),
                "TASK-1",
                "RUN-1");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("Camera"));
        }

        [Test]
        public void Capture_RejectsMissingRequest()
        {
            var result = VisualEvidenceCapture.Capture(
                null,
                null,
                "TASK-1",
                "RUN-1");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("request"));
        }
    }
}
