using UnityEngine;

namespace Balancy.Example
{
    public static class TransformExtension
    {
        public static void RemoveChildren(this Transform container)
        {
            int n = container.childCount - 1;

            for (int i = n; i >= 0; --i)
            {
                var child = container.GetChild(i);
                if (child == null) continue;

                Object.Destroy(child.gameObject);
            }
        }
    }
}