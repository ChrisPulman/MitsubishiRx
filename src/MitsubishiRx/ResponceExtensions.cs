// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the ResponceExtensions type.</summary>
internal static class ResponceExtensions
{
    /// <summary>Extends response instances with status helpers.</summary>
    /// <param name="result">The response being extended.</param>
    extension(Responce result)
    {
        /// <summary>Executes the Fail operation.</summary>
        /// <param name="error">The error parameter.</param>
        /// <param name="errorCode">The errorCode parameter.</param>
        /// <param name="exception">The exception parameter.</param>
        /// <returns>The Fail operation result.</returns>
        public Responce Fail(string error, int errorCode = 0, Exception? exception = null)
        {
            ArgumentNullException.ThrowIfNull(result);
            result.IsSucceed = false;
            result.ErrCode = errorCode;
            result.Exception = exception;
            result.Err = error;
            return result.EndTime();
        }

        /// <summary>Executes the ToBaseResponse operation.</summary>
        /// <returns>The ToBaseResponse operation result.</returns>
        public Responce ToBaseResponse()
        {
            ArgumentNullException.ThrowIfNull(result);
            var response = new Responce();
            _ = response.SetErrInfo(result);
            return response.EndTime();
        }
    }

    /// <summary>Extends typed response instances with status helpers.</summary>
    /// <typeparam name="T">The response payload type.</typeparam>
    /// <param name="result">The response being extended.</param>
    extension<T>(Responce<T> result)
    {
        /// <summary>Executes the Fail operation.</summary>
        /// <param name="error">The error parameter.</param>
        /// <param name="errorCode">The errorCode parameter.</param>
        /// <param name="exception">The exception parameter.</param>
        /// <returns>The Fail operation result.</returns>
        public Responce<T> Fail(string error, int errorCode = 0, Exception? exception = null)
        {
            ArgumentNullException.ThrowIfNull(result);
            result.IsSucceed = false;
            result.ErrCode = errorCode;
            result.Exception = exception;
            result.Err = error;
            return result.EndTime();
        }
    }
}
