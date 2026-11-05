// Unity 2022 和独立 .NET 服务端都有标准 HashSet；旧平台兼容实现仅在显式启用时编译。
// 上游按 !WINDOWS 启用会在现代非 Windows 编译分支中产生同名类型冲突。
#if BEPU_LEGACY_HASHSET
using System.Collections;
using System.Collections.Generic;

namespace BEPUutilities.DataStructures
{
    /// <summary>
    /// Provides basic .NET 3.5 HashSet functionality on non-Windows platforms.
    /// </summary>
    public class HashSet<T> : IEnumerable<T>
    {
        //TODO: A proper implementation of a HashSet would be nice.

        Dictionary<T, bool> fakeSet;

        ///<summary>
        /// Constructs a new HashSet.
        ///</summary>
        public HashSet()
        {
            fakeSet = new Dictionary<T, bool>();
        }


        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return fakeSet.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return fakeSet.Keys.GetEnumerator();
        }

        Dictionary<T, bool>.KeyCollection.Enumerator GetEnumerator()
        {
            return fakeSet.Keys.GetEnumerator();
        }

        ///<summary>
        /// Adds an element to the HashSet.
        ///</summary>
        ///<param name="item">Item to add.</param>
        ///<returns>Whether or not the item could be added.</returns>
        public bool Add(T item)
        {
            if (fakeSet.ContainsKey(item))
                return false;

            fakeSet.Add(item, false);
            return true;
        }

        ///<summary>
        /// Removes an element from the HashSet.
        ///</summary>
        ///<param name="item">Item to remove.</param>
        ///<returns>Whether or not the item could be removed.</returns>
        public bool Remove(T item)
        {
            return fakeSet.Remove(item);
        }

        ///<summary>
        /// Clears the HashSet.
        ///</summary>
        public void Clear()
        {
            fakeSet.Clear();
        }

        /// <summary>
        /// Determines if the set contains the item.
        /// </summary>
        /// <param name="item">Item to check for containment.</param>
        /// <returns>Whether or not the item was contained in the set.</returns>
        public bool Contains(T item)
        {
            return fakeSet.ContainsKey(item);
        }
    }
}
#endif