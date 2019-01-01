#r "nuget: Surfus.Secure, 1.4.6"
#r "nuget: Polly, 6.1.2"

using System.Net.Http;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using Surfus.Secure;

public class ProxmoxLxc
{
    readonly private HttpClient _http;
    private readonly SshClient _ssh;

    internal ProxmoxLxc(HttpClient http, SshClient ssh)
    {
        _http = http;
        _ssh = ssh;
    }

    public async Task ReplaceAsync(string node, string vmid, Dictionary<string, string> configs, CancellationToken ct)
    {
        var policy = Policy.Handle<Exception>().WaitAndRetryAsync(5, x => TimeSpan.FromMilliseconds(250));
        await policy.ExecuteAsync(() => StopAsync(node, vmid, ct));
        await policy.ExecuteAsync(() => DeleteAsync(node, vmid, ct));
        await policy.ExecuteAsync(() => CreateAsync(node, vmid, configs, ct));
    }

    public async Task<string> GetStatusAsync(string node, string vmid, CancellationToken ct)
    {
        using (var res = await _http.GetAsync($"/api2/extjs/nodes/{node}/lxc/{vmid}/status/current", ct))
        {
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse>(body);
            if (!result.IsSuccess && result.Message.Contains("does not exist"))
            {
                return result.Message;
            }
            if (!result.IsSuccess)
            {
                throw new Exception("Failed to get container status!\n" + body);
            }
            return JToken.Parse(body)?["data"]?["status"]?.ToObject<string>();
        }
    }

    public async Task StopAsync(string node, string vmid, CancellationToken ct)
    {
        using (var res = await _http.PostAsync($"/api2/extjs/nodes/{node}/lxc/{vmid}/status/stop", null, ct))
        {
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse>(body);
            if (!result.IsSuccess && result.Message.Contains("does not exist"))
            {
                return;
            }
            if (!result.IsSuccess && result.Message.Contains("not running"))
            {
                return;
            }
            if (!result.IsSuccess)
            {
                throw new Exception("Failed to stop container!\n" + body);
            }
        }
        while (await GetStatusAsync(node, vmid, ct) != "stopped")
        {
            await Task.Delay(100);
        }
    }

    public async Task DeleteAsync(string node, string vmid, CancellationToken ct)
    {
        using (var res = await _http.DeleteAsync($"/api2/extjs/nodes/{node}/lxc/{vmid}", ct))
        {
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse>(body);
            if (!result.IsSuccess && result.Message.Contains("does not exist"))
            {
                return;
            }
            if (!result.IsSuccess)
            {
                throw new Exception("Failed to delete container!\n" + body);
            }
        }
        while (!(await GetStatusAsync(node, vmid, ct)).Contains("does not exist"))
        {
            await Task.Delay(100);
        }
    }

    public async Task CreateAsync(string node, string vmid, Dictionary<string, string> configs, CancellationToken ct)
    {
        using (var form = new FormUrlEncodedContent(configs))
        using (var res = await _http.PostAsync($"/api2/extjs/nodes/{node}/lxc/", form, ct))
        {
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse>(body);
            if (!result.IsSuccess)
            {
                throw new Exception("Failed to create container!\n" + body);
            }
        }
        if(configs["start"] != "1")
        {
            return;
        }
        while (await GetStatusAsync(node, vmid, ct) != "running")
        {
            await Task.Delay(100);
        }
    }

    // Do we need to define node here?
    public async Task<string> ExecAsync(string vmid, string cmd, CancellationToken ct)
    {
        var exec = $"pct exec {vmid} -- /bin/sh -c '{cmd}'";
        var result = (await _ssh.ExecuteCommandAsync(exec, ct)).TrimEnd('\r').TrimEnd('\n');
        if (result.Contains("pct exec <vmid> [<extra-args>]"))
        {
            throw new Exception("Invalid Command\n" + result);
        }
        return result;
    }

    private class ApiResponse
    {
        public string Message { get; set; } = "";
        public int? Success { get; set; }
        public bool IsSuccess => Success == 1;
    }
}