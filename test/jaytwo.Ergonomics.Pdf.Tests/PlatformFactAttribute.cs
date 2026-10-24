using System;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// A fact that runs only on the named platforms ("WINDOWS", "LINUX", "OSX"). Elsewhere xUnit reports it skipped, so a
// platform-specific test never passes without running.
[AttributeUsage(AttributeTargets.Method)]
public sealed class PlatformFactAttribute : FactAttribute
{
    public PlatformFactAttribute(params string[] platforms)
    {
        if (!platforms.Any(platform => RuntimeInformation.IsOSPlatform(OSPlatform.Create(platform))))
        {
            Skip = "Runs only on " + string.Join(" and ", platforms) + ".";
        }
    }
}
