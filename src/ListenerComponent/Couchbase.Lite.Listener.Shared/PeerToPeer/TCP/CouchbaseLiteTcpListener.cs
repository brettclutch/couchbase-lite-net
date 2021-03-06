﻿//
//  CouchbaseLiteServiceListener.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
using System;
using System.Net;

namespace Couchbase.Lite.Listener.Tcp
{
    /// <summary>
    /// An implementation of CouchbaseLiteServiceListener using TCP/IP
    /// </summary>
    public sealed class CouchbaseLiteTcpListener : CouchbaseLiteServiceListener
    {

        #region Variables 

        private readonly HttpListener _listener;
        private Manager _manager;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="manager">The manager to use for opening DBs, etc</param>
        /// <param name="port">The port to listen on</param>
        /// <remarks>
        /// If running on Windows, check <a href="https://github.com/couchbase/couchbase-lite-net/wiki/Gotchas">
        /// This document</a>
        /// </remarks>
        public CouchbaseLiteTcpListener(Manager manager, ushort port)
        {
            _manager = manager;
            _listener = new HttpListener();
            string prefix = String.Format("http://*:{0}/", port);
            _listener.Prefixes.Add(prefix);
        }

        #endregion

        #region Private Methods

        //This gets called when the listener receives a request
        private void ProcessContext(HttpListenerContext context)
        {
            _listener.GetContextAsync().ContinueWith((t) => ProcessContext(t.Result));
            _router.HandleRequest(new CouchbaseListenerTcpContext(context, _manager));
        }

        #endregion

        #region Overrides

        public override void Start()
        {
            if (_listener.IsListening) {
                return;
            }

            _listener.Start();
            _listener.GetContextAsync().ContinueWith((t) => ProcessContext(t.Result));
        }

        public override void Stop()
        {
            if (!_listener.IsListening) {
                return;
            }

            _listener.Stop();
        }

        public override void Abort()
        {
            if (!_listener.IsListening) {
                return;
            }

            _listener.Abort();
        }

        protected override void DisposeInternal()
        {
            ((IDisposable)_listener).Dispose();
        }

        #endregion

    }
}

