#load "proxmox-node.csx"

static var Config = new
{
    Node = "proxmox",
    Lxc = new
    {
        PubKey = File.ReadAllText(@"~\.ssh\id_rsa.pub"),
        Password = ""
    },
    Proxmox = new ProxmoxSettings
    {
        Hostname = "192.168.0.2",
        ApiBaseUri = "https://192.168.0.2:8006",
        Username = "root",
        Realm = "pam",
        Password = ""
    }
};

