// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive
#else

namespace MitsubishiRx
#endif
{
    /// <summary>Provides the Responce type.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    public class Responce<T> : Responce
    {
        /// <summary>Initializes a new instance of the Responce class.</summary>
        public Responce()
        {
        }

        /// <summary>Initializes a new instance of the Responce class.</summary>
        /// <param name="data">The data parameter.</param>
        public Responce(T data) => Value = data;

        /// <summary>Initializes a new instance of the Responce class.</summary>
        /// <param name="result">The result parameter.</param>
        public Responce(Responce result)
        {
            if (result is null)
            {
                return;
            }

            Assignment(result);
        }

        /// <summary>Initializes a new instance of the Responce class.</summary>
        /// <param name="result">The result parameter.</param>
        /// <param name="data">The data parameter.</param>
        public Responce(Responce result, T data)
        {
            if (result is null)
            {
                return;
            }

            Assignment(result);
            Value = data;
        }

        /// <summary>Gets or sets the Value property.</summary>
        public T? Value { get; set; }

        /// <summary>Executes the SetErrInfo operation.</summary>
        /// <param name="result">The result parameter.</param>
        /// <returns>The SetErrInfo operation result.</returns>
        public new Responce<T> SetErrInfo(Responce result)
        {
            if (result is null)
            {
                return this;
            }

            _ = base.SetErrInfo(result);
            return this;
        }

        /// <summary>Executes the EndTime operation.</summary>
        /// <returns>The EndTime operation result.</returns>
        internal new Responce<T> EndTime()
        {
            _ = base.EndTime();
            return this;
        }

        /// <summary>Executes the Assignment operation.</summary>
        /// <param name="result">The result parameter.</param>
        private void Assignment(Responce result)
        {
            IsSucceed = result.IsSucceed;
            InitialTime = result.InitialTime;
            Err = result.Err;
            Request = result.Request;
            Response = result.Response;
            Exception = result.Exception;
            ErrCode = result.ErrCode;
            _ = base.EndTime();
            foreach (var err in result.ErrList)
            {
                if (!ErrList.Contains(err))
                {
                    ErrList.Add(err);
                }
            }
        }
    }
}
