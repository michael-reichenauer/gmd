global using gmd.Utils;
global using gmd.Utils.Logging;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("gmdTest")] // Tests access
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // DI access
