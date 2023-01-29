// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx
{
    /// <summary>
    /// Responce.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    public class Responce<T> : Responce
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Responce{T}"/> class.
        /// </summary>
        public Responce()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Responce{T}"/> class.
        /// </summary>
        /// <param name="data">The data.</param>
        public Responce(T data) => Value = data;

        /// <summary>
        /// Initializes a new instance of the <see cref="Responce{T}"/> class.
        /// </summary>
        /// <param name="result">The result.</param>
        public Responce(Responce result)
        {
            if (result == null)
            {
                return;
            }

            Assignment(result);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Responce{T}"/> class.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="data">The data.</param>
        public Responce(Responce result, T data)
        {
            if (result == null)
            {
                return;
            }

            Assignment(result);
            Value = data;
        }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        public T? Value { get; set; }

        /// <summary>
        /// Sets the error information.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>Responce.</returns>
        public new Responce<T> SetErrInfo(Responce result)
        {
            if (result == null)
            {
                return this;
            }

            base.SetErrInfo(result);
            return this;
        }

        internal new Responce<T> EndTime()
        {
            base.EndTime();
            return this;
        }

        private void Assignment(Responce result)
        {
            IsSucceed = result.IsSucceed;
            InitialTime = result.InitialTime;
            Err = result.Err;
            Request = result.Request;
            Response = result.Response;
            Exception = result.Exception;
            ErrCode = result.ErrCode;
            base.EndTime();
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
