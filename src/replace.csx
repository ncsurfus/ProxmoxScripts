#load "proxmox-config.csx"

using System.Threading;

public class LxcContainer
{
    public string Node { get; set; }
    public string Vmid { get; set; }
    public Dictionary<string, string> Config { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, string> PostConfig { get; set; } = new Dictionary<string, string>();
    public IEnumerable<string> Commands { get; set; } = Enumerable.Empty<string>();
}

public async Task ReplaceAsync(Proxmox proxmox, LxcContainer container, CancellationToken ct)
{
    var lxcConf = $"/etc/pve/lxc/{container.Vmid}.conf";
    var configs = container.Config.ToDictionary(x => x.Key, x => x.Value);
    configs["start"] = "0";
    configs["vmid"] = container.Vmid;
    var template = container.Config.TryGetValue("ostemplate", out var tle) ? tle : "";

    await proxmox.Lxc.StopAsync(container.Node, container.Vmid, ct)
        .IgnoreException(ex => ex.Message.Contains("not running"));
    await proxmox.Lxc.DeleteAsync(container.Node, container.Vmid, ct)
        .IgnoreException(ex => ex.Message.Contains("does not exist"));
    await proxmox.Lxc.CreateAsync(container.Node, configs, ct);

    foreach (var config in container.PostConfig)
    {
        await proxmox.Ssh.ExecuteCommandAsync($"echo '{config.Key}: {config.Value}' >> {lxcConf}", ct);
    }

    await proxmox.Lxc.StartAsync(container.Node, container.Vmid, ct);
    await WaitForBootAsync(proxmox, container.Vmid, template, ct);

    foreach (var cmd in container.Commands)
    {
        Console.WriteLine($"Executing: '{cmd}'");
        Console.WriteLine(await proxmox.Lxc.ExecAsync(container.Vmid, cmd, ct));
    }
}

async Task WaitForBootAsync(Proxmox proxmox, string vmid, string template, CancellationToken ct)
{
    if (template.Contains("alpine"))
    {
        await WaitAlpineBootAsync(proxmox, vmid, ct);
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

static async Task IgnoreException(this Task t, Predicate<Exception> filter)
{
    try
    {
        await t;
    }
    catch (Exception ex)
    {
        if (filter(ex))
        {
            return;
        }
        throw;
    }
}