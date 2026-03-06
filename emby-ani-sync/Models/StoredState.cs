using System;
using emby_ani_sync.Configuration;

namespace emby_ani_sync.Models;

public class StoredState
{
    public Guid UserId { get; set; }
    public ApiName ApiName { get; set; }
}
