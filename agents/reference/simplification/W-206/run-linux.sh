#!/bin/sh
set -eu
cd /work
cp /repo/agents/reference/simplification/W-206/probe/Program.cs .
sed 's,REPO,/repo,g' /repo/agents/reference/simplification/W-206/probe/probe.csproj.txt > probe.csproj
printf '<Project />\n' > Directory.Build.props
dotnet build probe.csproj -p:NuGetAudit=false >&2
dotnet bin/Debug/net10.0/probe.dll /work/files
