
#load "deployments.csx"
#r "nuget: YamlDotNet, 5.3.0"

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

var deserializer = new DeserializerBuilder()
    .WithNamingConvention(new CamelCaseNamingConvention())
    .IgnoreUnmatchedProperties()
    .Build();

if (File.Exists(Args[0]))
{
    var lxc = deserializer.Deserialize<LxcContainer>(File.ReadAllText(Args[0]));
    await LxcUtils.DeployAsync(lxc, TimeSpan.FromMinutes(10));
}
else if(File.Exists("deploy-" + Args[0]+ ".yml"))
{
    var lxc = deserializer.Deserialize<LxcContainer>(File.ReadAllText("deploy-" + Args[0]+ ".yml"));
    await LxcUtils.DeployAsync(lxc, TimeSpan.FromMinutes(10));
}
Console.WriteLine(Args[0] + " was not found.");