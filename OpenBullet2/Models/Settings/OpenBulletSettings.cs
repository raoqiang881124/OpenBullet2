﻿namespace OpenBullet2.Models.Settings
{
    public class OpenBulletSettings
    {
        public GeneralSettings GeneralSettings { get; set; } = new GeneralSettings();
        public RemoteSettings RemoteSettings { get; set; } = new RemoteSettings();
    }
}
