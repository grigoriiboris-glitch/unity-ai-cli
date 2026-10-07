using System;
using UnityEngine;

namespace UnityAI.Verification
{
    public static class VisualEvidenceCapture
    {
        public static VisualCaptureResult Capture(
            Camera camera,
            VisualCaptureRequest request,
            string taskId = null,
            string runId = null)
        {
            if (camera == null)
                return VisualCaptureResult.Failed("Camera is required.");

            if (request == null)
                return VisualCaptureResult.Failed("Capture request is required.");

            request.Normalize();

            var width = request.Resolution;
            var height = request.Resolution;

            var renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture;

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply(false, false);

                var png = texture.EncodeToPNG();
                if (png == null || png.Length > request.MaxBytes)
                {
                    UnityEngine.Object.Destroy(texture);
                    return VisualCaptureResult.Failed("Captured image exceeds configured output limit.");
                }

                return new VisualCaptureResult
                {
                    Status = "ok",
                    Evidence = new VisualEvidence
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Source = request.Source.ToString(),
                        TargetId = request.TargetId,
                        TargetPath = request.TargetPath,
                        Width = width,
                        Height = height,
                        TaskId = taskId,
                        RunId = runId,
                        CreatedAtUtc = DateTime.UtcNow.ToString("O"),
                        Texture = texture
                    }
                };
            }
            catch (Exception ex)
            {
                return VisualCaptureResult.Failed(ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }
    }
}
