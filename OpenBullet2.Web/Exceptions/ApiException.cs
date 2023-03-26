﻿namespace OpenBullet2.Web.Exceptions;

/// <summary>
/// A generic API exception.
/// </summary>
public class ApiException : Exception
{
    /// <summary>
    /// The error code.
    /// </summary>
    public ErrorCode ErrorCode { get; set; }

    /// <summary>
    /// Creates an <see cref="ApiException"/> given an 
    /// <paramref name="errorCode"/> and a <paramref name="message"/>.
    /// </summary>
    public ApiException(ErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <inheritdoc/>
    public override string ToString() => Message;
}

/// <summary>
/// Error codes for managed API exceptions.
/// </summary>
public enum ErrorCode
{
    // TODO: Revise these codes before release
    /// <summary>
    /// Internal Server Error.
    /// </summary>
    INTERNAL_SERVER_ERROR = 1,

    /// <summary>
    /// Unauthorized access.
    /// </summary>
    UNAUTHORIZED = 2,

    /// <summary>
    /// Local file not found.
    /// </summary>
    FILE_NOT_FOUND = 50,

    /// <summary>
    /// Remote resource not found.
    /// </summary>
    REMOTE_RESOURCE_NOT_FOUND = 51,

    /// <summary>
    /// Remote resource fetch failed.
    /// </summary>
    REMOTE_RESOURCE_FETCH_FAILED = 52,

    /// <summary>
    /// Local file already exists.
    /// </summary>
    FILE_ALREADY_EXISTS = 60,

    /// <summary>
    /// Guest user not found.
    /// </summary>
    GUEST_NOT_FOUND = 1001,

    /// <summary>
    /// Plugin not found.
    /// </summary>
    PLUGIN_NOT_FOUND = 1002,

    /// <summary>
    /// Wordlist not found.
    /// </summary>
    WORDLIST_NOT_FOUND = 1003,

    /// <summary>
    /// Proxy group not found.
    /// </summary>
    PROXY_GROUP_NOT_FOUND = 1004,

    /// <summary>
    /// Config not found.
    /// </summary>
    CONFIG_NOT_FOUND = 1005,

    /// <summary>
    /// Endpoint not found.
    /// </summary>
    ENDPOINT_NOT_FOUND = 1006,

    /// <summary>
    /// Hit not found.
    /// </summary>
    HIT_NOT_FOUND = 1007,

    /// <summary>
    /// Triggered action not found.
    /// </summary>
    TRIGGERED_ACTION_NOT_FOUND = 1008,

    /// <summary>
    /// Job not found.
    /// </summary>
    JOB_NOT_FOUND = 1009,

    /// <summary>
    /// Endpoint already exists.
    /// </summary>
    ENDPOINT_ALREADY_EXISTS = 1106,

    /// <summary>
    /// The proxy group is being used in a job.
    /// </summary>
    PROXY_GROUP_IN_USE = 2001,

    /// <summary>
    /// The job is not idle.
    /// </summary>
    JOB_NOT_IDLE = 2002,

    /// <summary>
    /// Invalid job type.
    /// </summary>
    INVALID_JOB_TYPE = 3001,

    /// <summary>
    /// Invalid job configuration.
    /// </summary>
    INVALID_JOB_CONFIGURATION = 3002,

    /// <summary>
    /// Action not allowed for a remote config.
    /// </summary>
    REMOTE_CONFIG = 5000
}
