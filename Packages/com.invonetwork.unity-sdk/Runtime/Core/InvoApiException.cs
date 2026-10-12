using System;

namespace InvoSDK
{
    /// <summary>
    /// Thrown when an Invo API call returns a non-2xx status.
    /// Branch on <see cref="StatusCode"/> and <see cref="ErrorCode"/> — never on message text.
    /// The backend error envelope is { error, error_code, error_id, timestamp }.
    /// </summary>
    public class InvoApiException : Exception
    {
        /// <summary>HTTP status code. 0 when the request never reached the server.</summary>
        public long StatusCode { get; }

        /// <summary>Machine-readable code from the response body, e.g. "PHONE_SHARE_APPROVAL_REQUIRED". May be null.</summary>
        public string ErrorCode { get; }

        /// <summary>Support correlation id from the response body. Quote this to Invo support. May be null.</summary>
        public string ErrorId { get; }

        /// <summary>Seconds to wait before retrying, from a 429 response. Null when not rate limited.</summary>
        public int? RetryAfterSeconds { get; }

        /// <summary>Raw response body. May contain PII — do not log verbatim in production.</summary>
        public string Body { get; }

        /// <summary>True when the server refused the request because it is a duplicate of one already processed.</summary>
        public bool IsDuplicate => StatusCode == 409;

        /// <summary>True when the caller is being rate limited or is inside an anti-abuse lockout.</summary>
        public bool IsRateLimited => StatusCode == 429;

        /// <summary>True when the request never reached the server (offline, DNS, TLS).</summary>
        public bool IsNetworkError => StatusCode == 0;

        public InvoApiException(string message, long statusCode, string errorCode, string errorId,
                                int? retryAfterSeconds, string body)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
            ErrorId = errorId;
            RetryAfterSeconds = retryAfterSeconds;
            Body = body;
        }

        /// <summary>Short, log-safe summary. Excludes the raw body.</summary>
        public override string ToString()
        {
            string code = string.IsNullOrEmpty(ErrorCode) ? "-" : ErrorCode;
            string id = string.IsNullOrEmpty(ErrorId) ? "-" : ErrorId;
            return $"InvoApiException(status={StatusCode}, error_code={code}, error_id={id}): {Message}";
        }
    }
}
