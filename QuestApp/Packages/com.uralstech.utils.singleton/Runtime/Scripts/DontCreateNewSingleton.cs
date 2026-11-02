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

using System.Diagnostics.CodeAnalysis;
using UnityEngine;

#nullable enable
namespace Uralstech.Utils.Singleton
{
    /// <summary>Utility class to make inheriting types singletons.</summary>
    /// <remarks>
    /// <para>Assumes complete ownership of the parent <see cref="GameObject"/>.</para>
    /// <para>
    /// Unlike <see cref="Singleton{T}"/>, this does not create a new instance of
    /// <typeparamref name="T"/> if there are no existing ones.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type to be made a singleton.</typeparam>
    public abstract partial class DontCreateNewSingleton<T> : MonoBehaviour
        where T : DontCreateNewSingleton<T>
    {
        /// <summary>Is there an existing instance of type <typeparamref name="T"/>?</summary>
        public static bool HasInstance => Instance != null;

        /// <summary>The singleton instance of type <typeparamref name="T"/>.</summary>
        /// <remarks>Check <see cref="HasInstance"/> to check if an instance actually exists.</remarks>
#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
        public static T? Instance { get; private set; }

        /// <summary>Tries to get the singleton <typeparamref name="T"/> instance.</summary>
        /// <param name="instance">The singleton instance or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if an instance exists, <see langword="false"/> otherwise.</returns>
        public static bool TryGetInstance([NotNullWhen(true)] out T? instance)
        {
            instance = Instance;
            return instance != null;
        }
        
        /// <remarks>
        /// If <see cref="Instance"/> is <see langword="null"/>, sets it to <see langword="this"/>,
        /// otherwise, destroys the current <see cref="GameObject"/>. All inheriting classes <i>must</i>
        /// invoke this method if overriding it.
        /// </remarks>
        protected virtual void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = (T)this;
        }
    }
}
