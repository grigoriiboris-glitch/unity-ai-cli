namespace UnityAI.Verification
{
    public enum VisibilityExpectation
    {
        Required,
        Optional,
        Hidden
    }

    [System.Serializable]
    public sealed class VisibilityVerificationPolicy
    {
        public VisibilityExpectation Expected = VisibilityExpectation.Required;
    }
}
