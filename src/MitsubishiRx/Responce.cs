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
    public class Responce
    {
        /// <summary>Gets or sets the IsSucceed property.</summary>
        public bool IsSucceed { get; set; } = true;

        /// <summary>Gets or sets the Err property.</summary>
        public string Err
        {
            get => field ?? string.Empty;
            set
            {
                field = value;
                AddErr2List();
            }
        }

        /// <summary>Gets or sets the ErrCode property.</summary>
        public int ErrCode { get; set; }

        /// <summary>Gets or sets the Exception property.</summary>
        public Exception? Exception { get; set; }

        /// <summary>Gets or sets the ErrList property.</summary>
        public List<string> ErrList { get; } = new List<string>();

        /// <summary>Gets or sets the Request property.</summary>
        public string? Request { get; set; }

        /// <summary>Gets or sets the Response property.</summary>
        public string? Response { get; set; }

        /// <summary>Gets or sets the Request2 property.</summary>
        public string? Request2 { get; set; }

        /// <summary>Gets or sets the Response2 property.</summary>
        public string? Response2 { get; set; }

        /// <summary>Gets the TimeConsuming property.</summary>
        public double? TimeConsuming { get; private set; }

        /// <summary>Gets the InitialTime property.</summary>
        public DateTime InitialTime { get; protected set; } = DateTime.Now;

        /// <summary>Executes the SetErrInfo operation.</summary>
        /// <param name="result">The result parameter.</param>
        /// <returns>The SetErrInfo operation result.</returns>
        public Responce SetErrInfo(Responce result)
        {
            if (result is null)
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

        /// <summary>Executes the AddErr2List operation.</summary>
        public void AddErr2List()
        {
            if (ErrList.Contains(Err))
            {
                return;
            }

            ErrList.Add(Err);
        }

        /// <summary>Executes the EndTime operation.</summary>
        /// <returns>The EndTime operation result.</returns>
        internal Responce EndTime()
        {
            TimeConsuming = (DateTime.Now - InitialTime).TotalMilliseconds;
            return this;
        }
    }
}
