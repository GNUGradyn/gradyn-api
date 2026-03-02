FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["gradyn-api-2/gradyn-api-2.csproj", "gradyn-api-2/"]
RUN dotnet restore "gradyn-api-2/gradyn-api-2.csproj"
COPY . .
WORKDIR "/src/gradyn-api-2"
RUN dotnet build "./gradyn-api-2.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./gradyn-api-2.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "gradyn-api-2.dll"]
