// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx;

internal static class ResponceExtensions
{
    public static Responce Fail(this Responce result, string error, int errorCode = 0, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.IsSucceed = false;
        result.ErrCode = errorCode;
        result.Exception = exception;
        result.Err = error;
        return result.EndTime();
    }

    public static Responce<T> Fail<T>(this Responce<T> result, string error, int errorCode = 0, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.IsSucceed = false;
        result.ErrCode = errorCode;
        result.Exception = exception;
        result.Err = error;
        return result.EndTime();
    }

    public static Responce ToBaseResponse(this Responce result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var response = new Responce();
        response.SetErrInfo(result);
        return response.EndTime();
    }
}
