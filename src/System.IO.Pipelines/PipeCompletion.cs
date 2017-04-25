﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.IO.Pipelines
{
    internal struct PipeCompletionCallback
    {
        public Action<Exception, object> Callback;
        public object State;
    }

    internal struct PipeCompletion
    {
        private const int InitialCallbacksSize = 4;
        private static readonly Exception _completedNoException = new Exception();

#if COMPLETION_LOCATION_TRACKING
        private string _completionLocation;
#endif
        private Exception _exception;
        private PipeCompletionCallback[] _callbacks;
        private int _callbackCount;

        public string Location
        {
            get
            {
#if COMPLETION_LOCATION_TRACKING
                return _completionLocation;
#else
                return null;
#endif
            }
        }

        public bool IsCompleted => _exception != null;

        public bool TryComplete(Exception exception = null)
        {
#if COMPLETION_LOCATION_TRACKING
            _completionLocation = Environment.StackTrace;
#endif
            if (_exception == null)
            {
                // Set the exception object to the exception passed in or a sentinel value
                _exception = exception ?? _completedNoException;
            }
            return _callbackCount > 0;
        }

        public void AttachCallback(Action<Exception, object> callback, object state)
        {
            if (IsCompleted)
            {
                PipelinesThrowHelper.ThrowInvalidOperationException(ExceptionResource.AttachingToCompletedPipe);
            }
            if (_callbacks == null)
            {
                _callbacks = new PipeCompletionCallback[InitialCallbacksSize];
            }

            var newIndex = _callbackCount;
            _callbackCount++;

            if (_callbackCount == _callbacks.Length)
            {
                var newArray = new PipeCompletionCallback[_callbacks.Length * 2];
                Array.Copy(_callbacks, newArray, _callbacks.Length);
                _callbacks = newArray;
            }
            _callbacks[newIndex].Callback = callback;
            _callbacks[newIndex].State = state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCompletedOrThrow()
        {
            if (_exception != null)
            {
                if (_exception != _completedNoException)
                {
                    ThrowFailed();
                }
                return true;
            }
            return false;
        }

        public void InvokeCallbacks()
        {
            Debug.Assert(IsCompleted);

            if (_callbacks == null)
            {
                return;
            }

            foreach (var completionCallback in _callbacks)
            {
                if (completionCallback.Callback == null)
                {
                    // we do not allow registering null callbacks
                    // safe to assume we reached end of callback list
                    break;
                }
                completionCallback.Callback.Invoke(_exception, completionCallback.State);
            }
        }

        public void Reset()
        {
            Debug.Assert(IsCompleted);
            _callbackCount = 0;
            _exception = null;
            if (_callbacks != null)
            {
                for (int i = 0; i < _callbacks.Length; i++)
                {
                    _callbacks[i] = default(PipeCompletionCallback);
                }
            }

#if COMPLETION_LOCATION_TRACKING
            _completionLocation = null;
#endif
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowFailed()
        {
            throw _exception;
        }

        public override string ToString()
        {
            return $"{nameof(IsCompleted)}: {IsCompleted}";
        }
    }
}
