using UnityEngine;

namespace UnityAI.Verification
{
    public static class VerificationJson
    {
        public static string ToJson(VerificationResult result)
        {
            if (result == null)
                return "{\"status\":\"error\",\"message\":\"null_result\"}";

            return JsonUtility.ToJson(result, false);
        }
    }
}
