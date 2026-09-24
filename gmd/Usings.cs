global using gmd.Utils;
global using gmd.Utils.Logging;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("gmdTest")] // Tests access
[assembly: InternalsVisibleTo("gmdE2eTest")] // End-to-end tests access, for the fixtures that run git
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // DI access
