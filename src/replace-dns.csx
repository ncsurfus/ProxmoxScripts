#load "proxmox-script.csx"

var dns = new (string Name, string Address)[]
{
    ("internet.lab" , "192.168.0.1"),
    ("dns.lab"      , "192.168.0.4"),
    ("gns3.lab"     , "192.168.0.8"),
    ("dotnet.lab"   , "192.168.0.9"),
    ("winserver.lab", "192.168.0.49")
};
var hosts = String.Join(@"\n", dns.Select(x => $"{x.Address} {x.Name}"));

var con = new LxcContainer
{
    Vmid = "100",
    Config = new Dictionary<string, string>
    {
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
    },
    Commands = new []
    {
        $"echo -e \"{hosts}\" >> /etc/hosts && cat /etc/hosts",
        "apk add dnsmasq openssh",
        "rc-update add dnsmasq",
        "rc-service dnsmasq start",
        "rc-service sshd start"
    }
};

await LxcUtils.ReplaceAsync(con, TimeSpan.FromSeconds(60));