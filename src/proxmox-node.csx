#r "nuget: Newtonsoft.Json, 12.0.1"
#load "proxmox-lxc.csx"

using System.Net;
using System.Net.Http;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Surfus.Secure;

public class ProxmoxSettings
{
    public string Hostname { get; set; }
    public string ApiBaseUri { get; set; }
    public string Username { get; set; }
    public string Realm { get; set; }
    public string Password { get; set; }
}

public class ProxmoxNode : IDisposable
{
    private string _username;
    private string _realm;
    private string _password;
    private HttpClientHandler _httpHandler = new HttpClientHandler();

    public string Hostname { get; }
    public string Node { get; private set; }
    public HttpClient Http { get; private set; }
    public SshClient Ssh { get; }
    public ProxmoxLxc Lxc { get; private set; }

    private ProxmoxNode(ProxmoxSettings settings)
    {
        _httpHandler.ServerCertificateCustomValidationCallback = delegate { return true; };

        Http = new HttpClient(_httpHandler, true);
        Http.BaseAddress = new Uri(settings.ApiBaseUri);

        Ssh = new SshClient(settings.Hostname);

        Hostname = settings.Hostname;
        _username = settings.Username;
        _realm = settings.Realm;
        _password = settings.Password;
    }

    public static async Task<ProxmoxNode> ConnectAsync(ProxmoxSettings settings, CancellationToken ct)
    {
        var node = new ProxmoxNode(settings);
        try
        {
            await node.ConnectAsync(ct);
            return node;
        }
        catch
        {
            node.Dispose();
            throw;
        }
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        await SshConnectAsync(ct);
        await HttpConnectAsync(ct);
        Lxc = new ProxmoxLxc(Node, Http, Ssh);
    }

    private async Task SshConnectAsync(CancellationToken ct)
    {
        await Ssh.ConnectAsync(ct);
        if (!await Ssh.AuthenticateAsync(_username, _password, ct))
        {
            throw new Exception("Authentication failed (SSH).");
        }
        Node = (await Ssh.ExecuteCommandAsync("hostname", ct)).Trim('\n');
    }

    private async Task HttpConnectAsync(CancellationToken ct)
    {
        var reqBody = new Dictionary<string, string>
        {
            { "username", _username + "@" + _realm },
            { "password", _password }
        };
        using (var reqForm = new FormUrlEncodedContent(reqBody))
        using (var resp = await Http.PostAsync("/api2/json/access/ticket", reqForm, ct))
        {
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"Authentication failed ({resp.StatusCode}).\n" + body);
            }
            var auth = ParseAuthResponse(body);
            Http.DefaultRequestHeaders.Add("CSRFPreventionToken", auth.Token);
            var cookieTicket = new Cookie("PVEAuthCookie", auth.Ticket);
            _httpHandler.CookieContainer.Add(Http.BaseAddress, cookieTicket);
        }
    }

    private (string Token, string Ticket) ParseAuthResponse(string content)
    {
        var json = JToken.Parse(content);
        var token = json["data"]?["CSRFPreventionToken"]?.ToObject<string>();
        var ticket = json["data"]?["ticket"]?.ToObject<string>();
        if (String.IsNullOrWhiteSpace(token))
        {
            throw new Exception($"Authentication failed (invalid token).\n" + content);
        }
        if (String.IsNullOrWhiteSpace(ticket))
        {
            throw new Exception($"Authentication failed (invalid ticket).\n" + content);
        }
        return (token, ticket);
    }

    public void Dispose()
    {
        Http.Dispose();
        Ssh.Dispose();
    }
}