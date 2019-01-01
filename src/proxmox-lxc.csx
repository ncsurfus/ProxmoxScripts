#r "nuget: Surfus.Secure, 1.4.6"

using System.Net.Http;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

    public async Task StartAsync(string node, string vmid, CancellationToken ct)
    {
        using (var res = await _http.PostAsync($"/api2/extjs/nodes/{node}/lxc/{vmid}/status/start", null, ct))
        {
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse>(body);
            if (!result.IsSuccess)
            {
                throw new Exception("Failed to start container!\n" + body);
            }
            var upid = result.Data.Value<string>();
            await WaitForTaskAsync(node, upid, ct);
        }
    }

    public async Task StopAsync(string node, string vmid, CancellationToken ct)
    {
        using (var res = await _http.PostAsync($"/api2/extjs/nodes/{node}/lxc/{vmid}/status/stop", null, ct))
        {
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse>(body);
            if (!result.IsSuccess)
            {
                throw new Exception("Failed to stop container!\n" + body);
            }
            var upid = result.Data.Value<string>();
            await WaitForTaskAsync(node, upid, ct);
        }
    }

    public async Task DeleteAsync(string node, string vmid, CancellationToken ct)
    {
        using (var res = await _http.DeleteAsync($"/api2/extjs/nodes/{node}/lxc/{vmid}", ct))
        {
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse>(body);
            if (!result.IsSuccess)
            {
                throw new Exception("Failed to delete container!\n" + body);
            }
            var upid = result.Data.Value<string>();
            await WaitForTaskAsync(node, upid, ct);
        }
    }

    public async Task CreateAsync(string node, Dictionary<string, string> configs, CancellationToken ct)
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
            var upid = result.Data.Value<string>();
            await WaitForTaskAsync(node, upid, ct);
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

    private async Task WaitForTaskAsync(string node, string upid, CancellationToken ct)
    {
        while (true)
        {
            var task = await GetTaskAsync(node, upid, ct);
            if (task.Status == "stopped" && task.Message == "OK")
            {
                return;
            }
            if (task.Status == "stopped")
            {
                throw new Exception(task.Message);
            }
            await Task.Delay(100);
        }
    }

    private async Task<(string Status, string Message)> GetTaskAsync(string node, string upid, CancellationToken ct)
    {
        using (var res = await _http.GetAsync($"/api2/json/nodes/{node}/tasks/{upid}/status"))
        {
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ApiResponse>(body);
            if (result.Errors != null || result.Data == null)
            {
                throw new Exception("Failed to get task status!\n" + body);
            }
            var status = result.Data?["status"]?.Value<string>();
            var message = result.Data?["exitstatus"]?.Value<string>();
            return (status, message);
        }
    }

    private class ApiResponse
    {
        public string Message { get; set; } = "";
        public int? Success { get; set; }
        public bool IsSuccess => Success == 1;
        public JToken Errors { get; set; }
        public JToken Data { get; set; }
    }
}