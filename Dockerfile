
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["GM Toolbox.csproj", "."]
RUN dotnet restore "./GM Toolbox.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./GM Toolbox.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GM Toolbox.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Keeps the SDK and the source in /src so git-sync can pull and republish into /app on every start.
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

# The project sets <AssemblyName>GM_Toolbox</AssemblyName>, so the dll has no space in its name.
ENTRYPOINT ["git-sync", "dotnet", "GM_Toolbox.dll"]
