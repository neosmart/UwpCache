VERSION = $(shell sed -r -n 's/^\[assembly: AssemblyVersion\("(.*).[0-9]+"\).*/\1/p' UwpCache/Properties/AssemblyInfo.cs)

all: UwpCache/UwpCache.${VERSION}.nupkg

UwpCache/UwpCache.${VERSION}.nupkg:
	cd UwpCache; nuget.exe pack UwpCache.csproj -Build -Symbols -Properties Configuration=Release

.PHONY: push
push: UwpCache/UwpCache.${VERSION}.nupkg
	nuget.exe push UwpCache/UwpCache.${VERSION}.nupkg -Source https://api.nuget.org/v3/index.json
	nuget.exe push UwpCache/UwpCache.${VERSION}.symbols.nupkg -Source https://nuget.smbsrc.net
