FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Karamel.Backend/Karamel.Backend.csproj", "Karamel.Backend/"]
RUN dotnet restore "Karamel.Backend/Karamel.Backend.csproj" -r linux-x64 -p:PublishReadyToRun=true
COPY Karamel.Backend/ Karamel.Backend/
WORKDIR "/src/Karamel.Backend"
RUN dotnet publish "Karamel.Backend.csproj" -c Release -r linux-x64 --self-contained false -p:PublishReadyToRun=true -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "Karamel.Backend.dll"]
