#load "replace.csx"

var config = new Dictionary<string, string>()
{
    {"start", "1"},
    {"vmid", "100"},
    {"hostname", "dns"},
    {"password", Config.Lxc.Password},
    {"ssh-public-keys", Config.Lxc.PubKey},
    {"ostemplate", "local:vztmpl/alpine-3.8-default_20180913_amd64.tar.xz"},
    {"rootfs", "local-lvm:0.025"},
    {"cores", "1"},
    {"memory", "128"},
    {"swap", "0"},
    {"net0", "bridge=vmbr0,name=eth0,ip=192.168.0.4/23,gw=192.168.0.1"},
    {"nameserver", "1.1.1.1"}
};

var dns = new (string Name, string Address)[]
{
    ("internet.lab" , "192.168.0.1"),
    ("dns.lab"      , "192.168.0.4"),
    ("gns3.lab"     , "192.168.0.8"),
    ("dotnet.lab"   , "192.168.0.9"),
    ("winserver.lab", "192.168.0.49")
};

IEnumerable<string> GetCommands()
{
    yield return "echo '# --- Begin LAB ---' >> /etc/hosts";
    foreach(var x in dns)
    {
        yield return $"echo {x.Address} {x.Name} >> /etc/hosts";
    }
    yield return "echo '# --- End LAB ---' >> /etc/hosts";
    yield return "apk add dnsmasq openssh";
    yield return "rc-update add dnsmasq";
    yield return "rc-service dnsmasq start";
    yield return "rc-service sshd start";
}

await ReplaceAsync(config, GetCommands());