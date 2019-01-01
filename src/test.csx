#load "proxmox-config.csx"

using System.Threading;

Console.WriteLine("starting...");

using (var cts = new CancellationTokenSource(60000))
using (var proxmox = new Proxmox(Config.Proxmox))
{
    await proxmox.ConnectAsync(cts.Token);
    await proxmox.Lxc.StartAsync("proxmox", "104", cts.Token);
    await proxmox.Lxc.StopAsync("proxmox", "104", cts.Token);
}