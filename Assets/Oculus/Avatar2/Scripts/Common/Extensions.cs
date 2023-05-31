using System.Collections.Generic;

using UnityEngine;

namespace Oculus.Avatar2
{
    public static class UnityExtensions
    {
        public static T ToNullIfDestroyed<T>(this T obj) where T : UnityEngine.Object
            => obj is null || obj == null ? null : obj;

        public static T GetComponentOrNull<T>(this GameObject obj) where T : Component
            => obj.GetComponent<T>().ToNullIfDestroyed();

        public static T GetComponentOrNull<T>(this Component c) where T : Component
            => c.gameObject.GetComponentOrNull<T>();

        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component
            => obj.GetComponentOrNull<T>() ?? obj.AddComponent<T>();
    }

    public static class ListExtensions
    {
        /// <summary>
        /// Insert an item into a sorted list using BinarySearch.
        /// </summary>
        public static void AddSorted<T>(this List<T> list, T item, IComparer<T> comparer)
        {
            int index = list.BinarySearch(item, comparer);
            list.Insert(index < 0 ? ~index : index, item);
        }

        /// <summary>
        /// Removes an item in a sorted list using BinarySearch.
        /// </summary>
        public static bool RemoveSorted<T>(this List<T> list, T item, IComparer<T> comparer)
        {
            int index = list.BinarySearch(item, comparer);
            if (index < 0)
            {
                return false;
            }

            list.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Insert an item into a sorted list range using BinarySearch.
        /// </summary>
        public static void AddSorted<T>(this List<T> list, int start, int count, T item, IComparer<T> comparer)
        {
            int index = list.BinarySearch(start, count, item, comparer);
            list.Insert(index < 0 ? ~index : index, item);
        }
    }

    public static class FloatExtenstions
    {
        private const float DEFAULT_EPS = 1e-30f;

        public static bool IsApproximatelyZero(this float x, float eps = DEFAULT_EPS)
        {
            return Mathf.Abs(x) <= eps;
        }
    }
}
