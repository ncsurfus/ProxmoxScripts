#load "proxmox-script.csx"

var cmds = new[]
{
    // GNS3 Requirements
    "localedef -i en_US -c -f UTF-8 -A /usr/share/locale/locale.alias en_US.UTF-8",
    "apt install -y curl gnupg",

    // GNS3
    "curl https://raw.githubusercontent.com/GNS3/gns3-server/master/scripts/remote-install.sh > gns3-remote-install.sh",
    "chmod +x gns3-remote-install.sh && ./gns3-remote-install.sh --with-iou --with-i386-repository",

    // Docker
    "mkdir -p /etc/systemd/system/containerd.service.d",
    "echo [Service] >> /etc/systemd/system/containerd.service.d/override.conf",
    "echo ExecStartPre= >> /etc/systemd/system/containerd.service.d/override.conf",
    "systemctl daemon-reload && systemctl start docker",

    // GNS3 Docker Tap
    "echo \"#!/bin/sh -e\" >> /etc/rc.local",
    "echo mkdir -p /dev/net >> /etc/rc.local",
    "echo mknod -m 666 /dev/net/tun c 10 200 >> /etc/rc.local",
    "chmod +x /etc/rc.local && /etc/rc.local",

    // Show IOU License
    "curl -L https://github.com/ncsurfus/goioulic/releases/download/v0.1.0/linux_amd64_goioulic > goioulic",
    "chmod +x goioulic && ./goioulic"
};

var con = new LxcContainer
{
    Vmid = "201",
    Config = new Dictionary<string, string>
    {
        {"hostname", "gns3-test-2"},
        {"password", Config.Lxc.Password},
        {"ssh-public-keys", Config.Lxc.PubKey},
        {"arch", "amd64"},
        {"ostemplate", "local:vztmpl/ubuntu-18.04-standard_18.04-1_amd64.tar.gz"},
        {"rootfs", "local-lvm:10"},
        {"cores", "1"},
        {"memory", "1024"},
        {"swap", "1024"},
        {"net0", "bridge=vmbr1,name=eth0,ip=192.168.0.129/23,gw=192.168.0.1"},
        {"nameserver", "192.168.0.4"},
        {"features", "nesting=1"}
    },
    PostConfig = new Dictionary<string, string>
    {
        {"lxc.apparmor.profile","unconfined"},
        {"lxc.cgroup.devices.allow","a"},
        {"lxc.cap.drop",""}
    },
    Commands = cmds
};


await LxcUtils.ReplaceAsync(con, TimeSpan.FromMinutes(10));