// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx
{
    /// <summary>
    /// Responce.
    /// </summary>
    public class Responce
    {
        private string? _Err;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is succeed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is succeed; otherwise, <c>false</c>.
        /// </value>
        public bool IsSucceed { get; set; } = true;

        /// <summary>
        /// Gets or sets the error.
        /// </summary>
        /// <value>
        /// The error.
        /// </value>
        public string Err
        {
            get => _Err;
            set
            {
                _Err = value;
                AddErr2List();
            }
        }

        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        /// <value>
        /// The error code.
        /// </value>
        public int ErrCode { get; set; }

        /// <summary>
        /// Gets or sets the exception.
        /// </summary>
        /// <value>
        /// The exception.
        /// </value>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Gets the error list.
        /// </summary>
        /// <value>
        /// The error list.
        /// </value>
        public List<string> ErrList { get; } = new List<string>();

        /// <summary>
        /// Gets or sets the request.
        /// </summary>
        /// <value>
        /// The request.
        /// </value>
        public string? Request { get; set; }

        /// <summary>
        /// Gets or sets the response.
        /// </summary>
        /// <value>
        /// The response.
        /// </value>
        public string? Response { get; set; }

        /// <summary>
        /// Gets or sets the request2.
        /// </summary>
        /// <value>
        /// The request2.
        /// </value>
        public string? Request2 { get; set; }

        /// <summary>
        /// Gets or sets the response2.
        /// </summary>
        /// <value>
        /// The response2.
        /// </value>
        public string? Response2 { get; set; }

        /// <summary>
        /// Gets the time consuming.
        /// </summary>
        /// <value>
        /// The time consuming.
        /// </value>
        public double? TimeConsuming { get; private set; }

        /// <summary>
        /// Gets or sets the initial time.
        /// </summary>
        /// <value>
        /// The initial time.
        /// </value>
        public DateTime InitialTime { get; protected set; } = DateTime.Now;

        /// <summary>
        /// Sets the error information.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>A Responce.</returns>
        public Responce SetErrInfo(Responce result)
        {
            if (result == null)
            {
                return this;
            }

            IsSucceed = result.IsSucceed;
            Err = result.Err;
            ErrCode = result.ErrCode;
            Exception = result.Exception;
            foreach (var err in result.ErrList)
            {
                if (!ErrList.Contains(err))
                {
                    ErrList.Add(err);
                }
            }

            return this;
        }

        /// <summary>
        /// Adds the err2 list.
        /// </summary>
        public void AddErr2List()
        {
            if (!ErrList.Contains(Err))
            {
                ErrList.Add(Err);
            }
        }

        internal Responce EndTime()
        {
            TimeConsuming = (DateTime.Now - InitialTime).TotalMilliseconds;
            return this;
        }
    }
}
