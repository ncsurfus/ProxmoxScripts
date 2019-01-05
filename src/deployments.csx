// #load "proxmox-config-sample.csx"
#load "proxmox-config.csx"

using System.Threading;

public class LxcContainer
{
    public string Vmid { get; set; } = "";
    public Dictionary<string, string> Config { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, string> PostConfig { get; set; } = new Dictionary<string, string>();
    public IEnumerable<string> Commands { get; set; } = Enumerable.Empty<string>();
}

public static class LxcUtils
{
    public static async Task DeployAsync(LxcContainer con, TimeSpan timeout, bool replace = true)
    {
        using (var cts = new CancellationTokenSource(timeout))
        using (var proxmox = await ProxmoxNode.ConnectAsync(Config.Proxmox, cts.Token))
        {
            await DeployAsync(proxmox, con, replace, cts.Token);
        }
    }

    public static async Task DeployAsync(ProxmoxNode proxmox, LxcContainer con, bool replace, CancellationToken ct)
    {
        var lxcConf = $"/etc/pve/lxc/{con.Vmid}.conf";
        var configs = new Dictionary<string, string>(con.Config);
        configs["start"] = "0";
        configs["vmid"] = con.Vmid;
        var template = con.Config.TryGetValue("ostemplate", out var tle) ? tle : "";

        if (replace)
        {
            await StopIfRunning(proxmox.Lxc, con.Vmid, ct);
            await DeleteIfExists(proxmox.Lxc, con.Vmid, ct);
        }

        await proxmox.Lxc.CreateAsync(configs, ct);

        foreach (var config in con.PostConfig)
        {
            await proxmox.Ssh.ExecuteCommandAsync($"echo '{config.Key}: {config.Value}' >> {lxcConf}", ct);
        }

        await proxmox.Lxc.StartAsync(con.Vmid, ct);
        await WaitForBootAsync(proxmox, con.Vmid, template, ct);

        foreach (var cmd in con.Commands)
        {
            Console.WriteLine($"Executing: '{cmd}'");
            var result = await proxmox.Lxc.ExecAsync(con.Vmid, cmd, ct);
            if (!String.IsNullOrWhiteSpace(result))
            {
                Console.WriteLine(result);
            }
        }
    }

    static async Task WaitForBootAsync(ProxmoxNode proxmox, string vmid, string template, CancellationToken ct)
    {
        if (template.Contains("alpine"))
        {
            await WaitAlpineBootAsync(proxmox, vmid, ct);
        }
    }

    static async Task WaitAlpineBootAsync(ProxmoxNode proxmox, string vmid, CancellationToken ct)
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

    static async Task StopIfRunning(ProxmoxLxc lxc, string vmid, CancellationToken ct)
    {
        try
        {
            await lxc.StopAsync(vmid, ct);
        }
        catch (Exception ex) when (ex.Message.Contains("not running"))
        {
            return;
        }
    }

    static async Task DeleteIfExists(ProxmoxLxc lxc, string vmid, CancellationToken ct)
    {
        try
        {
            await lxc.DeleteAsync(vmid, ct);
        }
        catch (Exception ex) when (ex.Message.Contains("does not exist"))
        {
            return;
        }
    }
}

