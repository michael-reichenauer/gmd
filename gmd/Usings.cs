global using gmd.Utils.Logging;
global using gmd.Utils;

global using static gmd.Utils.Result;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("gmdTest")]                   // Tests access
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]  // DI access

