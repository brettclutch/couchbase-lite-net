//
// LruCache.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using Sharpen;

namespace Couchbase.Lite.Util
{
    /// <summary>
    /// BEGIN LAYOUTLIB CHANGE
    /// This is a custom version that doesn't use the non standard LinkedHashMap#eldest.
    /// </summary>
    /// <remarks>
    /// BEGIN LAYOUTLIB CHANGE
    /// This is a custom version that doesn't use the non standard LinkedHashMap#eldest.
    /// END LAYOUTLIB CHANGE
    /// A cache that holds strong references to a limited number of values. Each time
    /// a value is accessed, it is moved to the head of a queue. When a value is
    /// added to a full cache, the value at the end of that queue is evicted and may
    /// become eligible for garbage collection.
    /// <p>If your cached values hold resources that need to be explicitly released,
    /// override
    /// <see cref="LruCache{K, V}.EntryRemoved(bool, object, object, object)">LruCache&lt;K, V&gt;.EntryRemoved(bool, object, object, object)
    ///     </see>
    /// .
    /// <p>If a cache miss should be computed on demand for the corresponding keys,
    /// override
    /// <see cref="LruCache{K, V}.Create(object)">LruCache&lt;K, V&gt;.Create(object)</see>
    /// . This simplifies the calling code, allowing it to
    /// assume a value will always be returned, even when there's a cache miss.
    /// <p>By default, the cache size is measured in the number of entries. Override
    /// <see cref="LruCache{K, V}.SizeOf(object, object)">LruCache&lt;K, V&gt;.SizeOf(object, object)
    ///     </see>
    /// to size the cache in different units. For example, this cache
    /// is limited to 4MiB of bitmaps:
    /// <pre>
    /// <code>int cacheSize = 4 * 1024 * 1024; // 4MiB</code>
    /// LruCache<String, Bitmap> bitmapCache = new LruCache<String, Bitmap>(cacheSize)
    /// protected int sizeOf(String key, Bitmap value) {
    /// return value.getByteCount();
    /// }
    /// }}</pre>
    /// <p>This class is thread-safe. Perform multiple cache operations atomically by
    /// synchronizing on the cache: <pre>
    /// <code></code>
    /// synchronized (cache)
    /// if (cache.get(key) == null) {
    /// cache.put(key, value);
    /// }
    /// }}</pre>
    /// <p>This class does not allow null to be used as a key or value. A return
    /// value of null from
    /// <see cref="LruCache{K, V}.Get(object)">LruCache&lt;K, V&gt;.Get(object)</see>
    /// ,
    /// <see cref="LruCache{K, V}.Put(object, object)">LruCache&lt;K, V&gt;.Put(object, object)
    ///     </see>
    /// or
    /// <see cref="LruCache{K, V}.Remove(object)">LruCache&lt;K, V&gt;.Remove(object)</see>
    /// is
    /// unambiguous: the key was not in the cache.
    /// <p>This class appeared in Android 3.1 (Honeycomb MR1); it's available as part
    /// of <a href="http://developer.android.com/sdk/compatibility-library.html">Android's
    /// Support Package</a> for earlier releases.
    /// </remarks>
    internal class LruCache<TKey, TValue> 
    where TKey: class 
    where TValue: class
    {
        private readonly Dictionary<TKey, TValue> _hashmap;
        private readonly LinkedList<TKey> _nodes;
        private readonly Object locker = new Object ();

        /// <summary>Size of this cache in units.</summary>
        /// <remarks>Size of this cache in units. Not necessarily the number of elements.</remarks>
        private int size;

        private int maxSize;

        private int putCount;

        private int createCount;

        private int evictionCount;

        private int hitCount;

        private int missCount;

        /// <param name="maxSize">
        /// for caches that do not override
        /// <see cref="LruCache{K, V}.SizeOf(object, object)">LruCache&lt;K, V&gt;.SizeOf(object, object)
        ///     </see>
        /// , this is
        /// the maximum number of entries in the cache. For all other caches,
        /// this is the maximum sum of the sizes of the entries in this cache.
        /// </param>
        public LruCache(int maxSize)
        {
            // COPY: Copied from android.util.LruCache
            if (maxSize <= 0)
            {
                throw new ArgumentException("maxSize <= 0");
            }
            this.maxSize = maxSize;
            this._nodes = new LinkedList<TKey>();
            this._hashmap = new Dictionary<TKey, TValue>();
        }

        /// <summary>Sets the size of the cache.</summary>
        /// <remarks>Sets the size of the cache.</remarks>
        /// <param name="maxSize">The new maximum size.</param>
        /// <hide></hide>
        public virtual void Resize(int maxSize)
        {
            if (maxSize <= 0)
            {
                throw new ArgumentException("maxSize <= 0");
            }
            lock (locker)
            {
                this.maxSize = maxSize;
            }
            Trim();
        }

        /// <summary>
        /// Returns the value for
        /// <code>key</code>
        /// if it exists in the cache or can be
        /// created by
        /// <code>#create</code>
        /// . If a value was returned, it is moved to the
        /// head of the queue. This returns null if a value is not cached and cannot
        /// be created.
        /// </summary>
        public TValue Get(TKey key)
        {
            if (key == null) {
                throw new ArgumentNullException("key");
            }

            TValue mapValue;
            lock (locker) {
                mapValue = _hashmap.Get(key);
                if (mapValue != null) {
                    hitCount++;
                    _nodes.Remove(key);
                    _nodes.AddFirst(key);
                    return mapValue;
                }
                missCount++;
            }

            TValue createdValue = Create(key);
            if (createdValue == null) {
                return default(TValue);
            }

            lock (locker) {
                createCount++;
                mapValue = _hashmap.Put(key, createdValue);
                _nodes.Remove(key);
                _nodes.AddFirst(key);
                if (mapValue != null) {
                    // There was a conflict so undo that last put
                    _hashmap[key] = mapValue;
                }
                else {
                    size += SafeSizeOf(key, createdValue);
                }
            }

            if (mapValue != null) {
                EntryRemoved(false, key, createdValue, mapValue);
                return mapValue;
            } else {
                Trim();
                return createdValue;
            }
        }

        public TValue this[TKey key] {
            get { return Get (key); }
            set { Put (key, value); }
        }

        /// <summary>
        /// Caches
        /// <code>value</code>
        /// for
        /// <code>key</code>
        /// . The value is moved to the head of
        /// the queue.
        /// </summary>
        /// <returns>
        /// the previous value mapped by
        /// <code>key</code>
        /// .
        /// </returns>
        public TValue Put(TKey key, TValue value)
        {
            if (key == null || value == null) {
                throw new ArgumentNullException("key == null || value == null");
            }

            TValue previous;
            lock (locker) {
                putCount++;
                size += SafeSizeOf(key, value);
                previous = _hashmap.Put(key, value);
                if (previous != null) {
                    size -= SafeSizeOf(key, previous);
                }
                else {
                    _nodes.AddFirst(key);
                }
            }

            if (previous != null) {
                EntryRemoved(false, key, previous, value);
            }

            Trim();
            return previous;
        }
            
        private void Trim()
        {
            while (true)
            {
                TKey key;
                TValue value;
                lock (locker) {
                    if (size < 0 || _hashmap.Count != size || _nodes.Count != size) {
                        throw new InvalidOperationException(GetType().FullName + ".sizeOf() is reporting inconsistent results!");
                    }

                    if (size <= maxSize || size == 0) {
                        break;
                    }

                    // BEGIN LAYOUTLIB CHANGE
                    // get the last item in the linked list.
                    // This is not efficient, the goal here is to minimize the changes
                    // compared to the platform version.
                    key = _nodes.Last.Value;
                    value = _hashmap[key];
                    _hashmap.Remove(key);
                    _nodes.RemoveLast();
                    // END LAYOUTLIB CHANGE
                    size -= SafeSizeOf(key, value);
                    evictionCount++;
                }

                EntryRemoved(true, key, value, default(TValue));
            }
        }

        /// <summary>
        /// Removes the entry for
        /// <code>key</code>
        /// if it exists.
        /// </summary>
        /// <returns>
        /// the previous value mapped by
        /// <code>key</code>
        /// .
        /// </returns>
        public TValue Remove(TKey key)
        {
            if (key == null) {
                throw new ArgumentNullException("key == null");
            }

            TValue previous;
            lock (locker) {
                previous = Collections.Remove(_hashmap, key);
                if (previous != null) {
                    size -= SafeSizeOf(key, previous);
                    _nodes.Remove(key);
                }
            }

            if (previous != null) {
                EntryRemoved(false, key, previous, default(TValue));
            }

            return previous;
        }

        /// <summary>Called for entries that have been evicted or removed.</summary>
        /// <remarks>
        /// Called for entries that have been evicted or removed. This method is
        /// invoked when a value is evicted to make space, removed by a call to
        /// <see cref="LruCache{K, V}.Remove(object)">LruCache&lt;K, V&gt;.Remove(object)</see>
        /// , or replaced by a call to
        /// <see cref="LruCache{K, V}.Put(object, object)">LruCache&lt;K, V&gt;.Put(object, object)
        ///     </see>
        /// . The default
        /// implementation does nothing.
        /// <p>The method is called without synchronization: other threads may
        /// access the cache while this method is executing.
        /// </remarks>
        /// <param name="evicted">
        /// true if the entry is being removed to make space, false
        /// if the removal was caused by a
        /// <see cref="LruCache{K, V}.Put(object, object)">LruCache&lt;K, V&gt;.Put(object, object)
        ///     </see>
        /// or
        /// <see cref="LruCache{K, V}.Remove(object)">LruCache&lt;K, V&gt;.Remove(object)</see>
        /// .
        /// </param>
        /// <param name="newValue">
        /// the new value for
        /// <code>key</code>
        /// , if it exists. If non-null,
        /// this removal was caused by a
        /// <see cref="LruCache{K, V}.Put(object, object)">LruCache&lt;K, V&gt;.Put(object, object)
        ///     </see>
        /// . Otherwise it was caused by
        /// an eviction or a
        /// <see cref="LruCache{K, V}.Remove(object)">LruCache&lt;K, V&gt;.Remove(object)</see>
        /// .
        /// </param>
        protected internal virtual void EntryRemoved(bool evicted, TKey key, TValue oldValue, TValue newValue)
        {
        }

        /// <summary>Called after a cache miss to compute a value for the corresponding key.</summary>
        /// <remarks>
        /// Called after a cache miss to compute a value for the corresponding key.
        /// Returns the computed value or null if no value can be computed. The
        /// default implementation returns null.
        /// <p>The method is called without synchronization: other threads may
        /// access the cache while this method is executing.
        /// <p>If a value for
        /// <code>key</code>
        /// exists in the cache when this method
        /// returns, the created value will be released with
        /// <see cref="LruCache{K, V}.EntryRemoved(bool, object, object, object)">LruCache&lt;K, V&gt;.EntryRemoved(bool, object, object, object)
        ///     </see>
        /// and discarded. This can occur when multiple threads request the same key
        /// at the same time (causing multiple values to be created), or when one
        /// thread calls
        /// <see cref="LruCache{K, V}.Put(object, object)">LruCache&lt;K, V&gt;.Put(object, object)
        ///     </see>
        /// while another is creating a value for the same
        /// key.
        /// </remarks>
        protected internal virtual TValue Create(TKey key)
        {
            return default(TValue);
        }

        private int SafeSizeOf(TKey key, TValue value)
        {
            int result = SizeOf(key, value);
            if (result < 0)
            {
                throw new InvalidOperationException("Negative size: " + key + "=" + value);
            }
            return result;
        }

        /// <summary>
        /// Returns the size of the entry for
        /// <code>key</code>
        /// and
        /// <code>value</code>
        /// in
        /// user-defined units.  The default implementation returns 1 so that size
        /// is the number of entries and max size is the maximum number of entries.
        /// <p>An entry's size must not change while it is in the cache.
        /// </summary>
        protected internal virtual int SizeOf(TKey key, TValue value)
        {
            return 1;
        }

        /// <summary>
        /// Clear the cache, calling
        /// <see cref="LruCache{K, V}.EntryRemoved(bool, object, object, object)">LruCache&lt;K, V&gt;.EntryRemoved(bool, object, object, object)
        ///     </see>
        /// on each removed entry.
        /// </summary>
        public void EvictAll()
        {
            lock(locker) {
                int oldMax = maxSize;
                maxSize = 0;
                Trim();
                maxSize = oldMax;
            }
        }

        // -1 will evict 0-sized elements
        /// <summary>
        /// For caches that do not override
        /// <see cref="LruCache{K, V}.SizeOf(object, object)">LruCache&lt;K, V&gt;.SizeOf(object, object)
        ///     </see>
        /// , this returns the number
        /// of entries in the cache. For all other caches, this returns the sum of
        /// the sizes of the entries in this cache.
        /// </summary>
        public int Size()
        {
            lock (locker)
            {
                return size;
            }
        }

        /// <summary>
        /// For caches that do not override
        /// <see cref="LruCache{K, V}.SizeOf(object, object)">LruCache&lt;K, V&gt;.SizeOf(object, object)
        ///     </see>
        /// , this returns the maximum
        /// number of entries in the cache. For all other caches, this returns the
        /// maximum sum of the sizes of the entries in this cache.
        /// </summary>
        public int MaxSize()
        {
            lock (locker)
            {
                return maxSize;
            }
        }

        /// <summary>
        /// Returns the number of times
        /// <see cref="LruCache{K, V}.Get(object)">LruCache&lt;K, V&gt;.Get(object)</see>
        /// returned a value that was
        /// already present in the cache.
        /// </summary>
        public int HitCount()
        {
            lock (locker)
            {
                return hitCount;
            }
        }

        /// <summary>
        /// Returns the number of times
        /// <see cref="LruCache{K, V}.Get(object)">LruCache&lt;K, V&gt;.Get(object)</see>
        /// returned null or required a new
        /// value to be created.
        /// </summary>
        public int MissCount()
        {
            lock (locker)
            {
                return missCount;
            }
        }

        /// <summary>
        /// Returns the number of times
        /// <see cref="LruCache{K, V}.Create(object)">LruCache&lt;K, V&gt;.Create(object)</see>
        /// returned a value.
        /// </summary>
        public int CreateCount()
        {
            lock (locker)
            {
                return createCount;
            }
        }

        /// <summary>
        /// Returns the number of times
        /// <see cref="LruCache{K, V}.Put(object, object)">LruCache&lt;K, V&gt;.Put(object, object)
        ///     </see>
        /// was called.
        /// </summary>
        public int PutCount()
        {
            lock (locker)
            {
                return putCount;
            }
        }

        /// <summary>Returns the number of values that have been evicted.</summary>
        /// <remarks>Returns the number of values that have been evicted.</remarks>
        public int EvictionCount()
        {
            lock (locker)
            {
                return evictionCount;
            }
        }

        /// <summary>
        /// Returns a copy of the current contents of the cache, ordered from least
        /// recently accessed to most recently accessed.
        /// </summary>
        /// <remarks>
        /// Returns a copy of the current contents of the cache, ordered from least
        /// recently accessed to most recently accessed.
        /// </remarks>
        public IDictionary<TKey, TValue> Snapshot()
        {
            lock (locker)
            {
                return new Dictionary<TKey, TValue>(_hashmap);
            }
        }

        public sealed override string ToString()
        {
            lock (locker) {
                int accesses = hitCount + missCount;
                int hitPercent = accesses != 0 ? (100 * hitCount / accesses) : 0;
                return string.Format ("LruCache[maxSize={0},hits={1},misses={2},hitRate={3:P}%]", maxSize, hitCount, missCount, hitPercent);
            }
        }
    }
}
