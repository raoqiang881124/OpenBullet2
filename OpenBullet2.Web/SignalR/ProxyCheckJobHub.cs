﻿using OpenBullet2.Core.Services;
using OpenBullet2.Web.Interfaces;
using OpenBullet2.Web.Services;

namespace OpenBullet2.Web.SignalR;

/// <summary>
/// SignalR hub for a proxy check job.
/// </summary>
public class ProxyCheckJobHub : JobHub
{
    private readonly ILogger<ProxyCheckJobHub> _logger;
    private readonly ProxyCheckJobService _jobService;

    /// <summary></summary>
    public ProxyCheckJobHub(IAuthTokenService tokenService,
        ILogger<ProxyCheckJobHub> logger, ProxyCheckJobService jobService,
        OpenBulletSettingsService obSettingsService)
        : base(tokenService, logger, jobService, obSettingsService)
    {
        _logger = logger;
        _jobService = jobService;
    }
}
