VERSION = $(shell sed -r -n 's/^\[assembly: AssemblyVersion\("(.*).[0-9]+"\).*/\1/p' UwpCache/Properties/AssemblyInfo.cs)

all: UwpCache/UwpCache.${VERSION}.nupkg

UwpCache/UwpCache.${VERSION}.nupkg:
	nuget.exe pack UwpCache/UwpCache.csproj -Build -Symbols -Properties Configuration=Release
