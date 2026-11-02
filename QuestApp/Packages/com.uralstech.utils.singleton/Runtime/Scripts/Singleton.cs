// Copyright 2026 URAV ADVANCED LEARNING SYSTEMS PRIVATE LIMITED
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;

#nullable enable
namespace Uralstech.Utils.Singleton
{
    /// <summary>Utility class to make inheriting types singletons.</summary>
    /// <remarks>Assumes complete ownership of the parent <see cref="GameObject"/>.</remarks>
    /// <typeparam name="T">The type to be made a singleton.</typeparam>
    public abstract partial class Singleton<T> : MonoBehaviour
        where T : Singleton<T>
    {
#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
        private static T? s_instance;

        /// <summary>Is there an existing instance of type <typeparamref name="T"/>?</summary>
        public static bool HasInstance => s_instance != null;

        /// <summary>The singleton instance of type <typeparamref name="T"/>.</summary>
        public static T Instance => s_instance != null
            ? s_instance : s_instance = new GameObject(typeof(T).Name).AddComponent<T>();

        /// <remarks>
        /// If there is no singleton instance of <typeparamref name="T"/>, sets it to <see langword="this"/>,
        /// otherwise, destroys the current <see cref="GameObject"/>. All inheriting classes <i>must</i>
        /// invoke this method if overriding it.
        /// </remarks>
        protected virtual void Awake()
        {
            if (s_instance == null)
            {
                s_instance = (T)this;
                return;
            }
            
            if (!ReferenceEquals(s_instance, this))
                Destroy(gameObject);
        }
    }
}
