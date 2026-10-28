# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["GM Toolbox.csproj", "."]
RUN dotnet restore "./GM Toolbox.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./GM Toolbox.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GM Toolbox.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
# It keeps the SDK and the source in /src so git-sync can pull and republish into /app on every start.
FROM build AS final
ENV REPO_URL=https://github.com/Ronovo/Gamemaster-Toolbox.git \
    SYNC_DIR=/src \
    BUILD_CMD="dotnet publish 'GM Toolbox.csproj' -c Release -o /app /p:UseAppHost=false" \
    ASPNETCORE_HTTP_PORTS=8080
COPY docker/git-sync.sh /usr/local/bin/git-sync
WORKDIR /app
COPY --from=publish /app/publish .

# The database file and the export archives both live here. Without a mount they would sit
# in the container's writable layer and be destroyed along with the container.
#
# Created and chowned as root before dropping to the app user: a volume inherits the ownership
# of the image directory behind it. Left owned by root, the app could not create gmtoolbox.db
# and would fail on startup. /src is chowned too so the app user can pull and rebuild it, and
# the partial .git that .dockerignore lets through is removed so git-sync starts a clean clone.
RUN sed -i 's/\r$//' /usr/local/bin/git-sync && chmod +x /usr/local/bin/git-sync \
    && rm -rf /src/.git \
    && mkdir -p /app/App_Data && chown -R $APP_UID:$APP_UID /app /src
USER $APP_UID

VOLUME ["/app/App_Data"]
EXPOSE 8080

# GM_Toolbox.dll, not "GM Toolbox.dll": the project sets <AssemblyName>GM_Toolbox</AssemblyName>
# because the folder name has a space in it and that broke the EF tooling.
ENTRYPOINT ["git-sync", "dotnet", "GM_Toolbox.dll"]
