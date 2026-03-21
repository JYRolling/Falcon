using UnityEngine;

namespace Falcon.Utils
{
    /// <summary>
    /// Animator extension helpers placed in a namespace to avoid duplicate-type collisions.
    /// Use: animator.HasParameter("ParamName")
    /// </summary>
    public static class AnimatorExtensions
    {
        /// <summary>
        /// Returns true when the Animator contains a parameter with the given name.
        /// Safe to call with null animator or null/empty paramName.
        /// </summary>
        public static bool HasParameter(this Animator animator, string paramName)
        {
            if (animator == null || string.IsNullOrEmpty(paramName)) return false;
            foreach (var p in animator.parameters)
                if (p.name == paramName) return true;
            return false;
        }
    }
}