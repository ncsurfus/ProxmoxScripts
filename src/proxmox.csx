#r "nuget: Newtonsoft.Json, 12.0.1"
#load "proxmox-container.csx"

using System.Net;
using System.Net.Http;
using System.Threading;
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

public class Proxmox : IDisposable
{
    private string _username;
    private string _realm;
    private string _password;
    private HttpClientHandler _httpHandler;

    public string Hostname { get; }
    public HttpClient Http { get; }
    public SshClient Ssh { get; }
    public ProxmoxLxc Lxc { get; }

    public Proxmox(ProxmoxSettings settings)
    {
        _httpHandler = new HttpClientHandler();
        _httpHandler.ServerCertificateCustomValidationCallback = (_, __, ___, ____) => true;

        Http = new HttpClient(_httpHandler, true);
        Http.BaseAddress = new Uri(settings.ApiBaseUri);

        Ssh = new SshClient(settings.Hostname);
        Lxc = new ProxmoxLxc(Http, Ssh);
        Hostname = settings.Hostname;
        _username = settings.Username;
        _realm = settings.Realm;
        _password = settings.Password;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        await HttpConnectAsync(ct);
        await SshConnectAsync(ct);
    }

    private async Task SshConnectAsync(CancellationToken ct)
    {
        await Ssh.ConnectAsync();
        if (!await Ssh.AuthenticateAsync(_username, _password))
        {
            throw new Exception("Authentication failed (SSH).");
        }
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
            _httpHandler.CookieContainer.Add(Http.BaseAddress, new Cookie("PVEAuthCookie", auth.Ticket));
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