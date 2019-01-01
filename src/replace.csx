#load "proxmox-config.csx"

using System.Threading;

async Task ReplaceAsync(Dictionary<string, string> config, IEnumerable<string> cmds, int timeout = 60000)
{
    using (var cts = new CancellationTokenSource(timeout))
    using (var proxmox = new Proxmox(Config.Proxmox))
    {
        await proxmox.ConnectAsync(cts.Token);
        await proxmox.Lxc.ReplaceAsync(Config.Node, config["vmid"], config, cts.Token);
        await WaitForBootAsync(proxmox, config["vmid"], config["ostemplate"], cts.Token);

        foreach (var cmd in cmds)
        {
            Console.WriteLine($"Executing: '{cmd}'");
            Console.WriteLine(await proxmox.Lxc.ExecAsync(config["vmid"], cmd, cts.Token));
        }
    }
}

async Task WaitForBootAsync(Proxmox proxmox, string vmid, string template, CancellationToken ct)
{
    if (template.Contains("alpine"))
    {
        await WaitAlpineBootAsync(proxmox, vmid, ct);
    }
    else if (template.Contains("ubuntu"))
    {

    }
    else
    {
        throw new Exception($"Container type {template} not supported.");
    }
}

async Task WaitAlpineBootAsync(Proxmox proxmox, string vmid, CancellationToken ct)
{
    while (true)
    {
        var status = await proxmox.Lxc.ExecAsync(vmid, "rc-status", ct);
        if (status.Contains("Runlevel: default") && !status.Contains("starting") && !status.Contains("stopped"))
        {
            return;
        }
        await Task.Delay(100);
    }
}