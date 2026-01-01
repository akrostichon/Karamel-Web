FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Karamel.Backend/Karamel.Backend.csproj", "Karamel.Backend/"]
RUN dotnet restore "Karamel.Backend/Karamel.Backend.csproj"
COPY . .
WORKDIR "/src/Karamel.Backend"
RUN dotnet build "Karamel.Backend.csproj" -c Release -o /app/build
RUN dotnet publish "Karamel.Backend.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "Karamel.Backend.dll"]
