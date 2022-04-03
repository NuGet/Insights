# escape=`

# Based on https://github.com/Azure/azure-functions-docker/blob/dev/host/3.0/nanoserver/1909/dotnet.Dockerfile

# Build the host and app
FROM mcr.microsoft.com/dotnet/sdk:6.0-windowsservercore-ltsc2022 AS host-and-app

SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

ENV ASPNETCORE_URLS=http://+:80 `
    DOTNET_RUNNING_IN_CONTAINER=true `
    DOTNET_USE_POLLING_FILE_WATCHER=true `
    NUGET_XMLDOC_MODE=skip `
    PublishWithAspNetCoreTargetManifest=false `
    HOST_VERSION=4.3.0 `
    EnableZipPublish=false

COPY . C:\app

RUN [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; `
    $BuildNumber = $Env:HOST_VERSION.split('.')[-1]; `
    Invoke-WebRequest -OutFile host.zip https://github.com/Azure/azure-functions-host/archive/v$Env:HOST_VERSION.zip; `
    Expand-Archive host.zip .; `
    cd azure-functions-host-$Env:HOST_VERSION; `
    dotnet publish /p:BuildNumber=$BuildNumber /p:CommitHash=$Env:HOST_VERSION src\WebJobs.Script.WebHost\WebJobs.Script.WebHost.csproj -c Release --output C:\bin\host; `
    cd C:\app\src\Worker; `
    dotnet publish Worker.csproj --output C:\bin\worker

# Start the host
FROM mcr.microsoft.com/dotnet/aspnet:6.0-windowsservercore-ltsc2022

COPY --from=host-and-app ["C:\\bin", "C:\\bin"]

ENV WEBSITE_HOSTNAME=localhost:80 `
    AzureWebJobsScriptRoot=C:\bin\worker `
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

CMD ["dotnet", "C:\\bin\\host\\Microsoft.Azure.WebJobs.Script.WebHost.dll"]
