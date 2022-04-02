# escape=`

# Based on https://github.com/Azure/azure-functions-docker/blob/dev/host/3.0/nanoserver/1909/dotnet.Dockerfile

# Installer image
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS installer-env

SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

ENV ASPNETCORE_URLS=http://+:80 `
    DOTNET_RUNNING_IN_CONTAINER=true `
    DOTNET_USE_POLLING_FILE_WATCHER=true `
    NUGET_XMLDOC_MODE=skip `
    PublishWithAspNetCoreTargetManifest=false `
    HOST_VERSION=4.3.0

RUN [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; `
    $BuildNumber = $Env:HOST_VERSION.split('.')[-1]; `
    Invoke-WebRequest -OutFile host.zip https://github.com/Azure/azure-functions-host/archive/v$Env:HOST_VERSION.zip; `
    Expand-Archive host.zip .; `
    cd azure-functions-host-$Env:HOST_VERSION; `
    /dotnet/dotnet publish /p:BuildNumber=$BuildNumber /p:CommitHash=$Env:HOST_VERSION src\WebJobs.Script.WebHost\WebJobs.Script.WebHost.csproj  -c Release --output C:\runtime


# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0

COPY --from=installer-env ["C:\\runtime", "C:\\runtime"]

ENV AzureWebJobsScriptRoot=C:\approot `
    WEBSITE_HOSTNAME=localhost:80

CMD ["dotnet", "C:\\runtime\\Microsoft.Azure.WebJobs.Script.WebHost.dll"]
