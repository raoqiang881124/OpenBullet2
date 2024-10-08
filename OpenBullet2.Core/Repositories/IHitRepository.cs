﻿using OpenBullet2.Core.Entities;
using System.Threading.Tasks;

namespace OpenBullet2.Core.Repositories;

/// <summary>
/// Stores hits.
/// </summary>
public interface IHitRepository : IRepository<HitEntity>
{
    /// <summary>
    /// Deletes all hits from the repository.
    /// </summary>
    Task PurgeAsync();

    /// <summary>
    /// Count the number of hits.
    /// </summary>
    Task<long> CountAsync();
}
