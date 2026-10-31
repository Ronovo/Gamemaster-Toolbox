# The coverage report only shows what the tests reach, so failing tests or low coverage still build the image.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS coverage
WORKDIR /src
RUN dotnet tool install dotnet-reportgenerator-globaltool --version 5.5.11 --tool-path /tools
COPY . .
RUN dotnet test Tests/GM_Toolbox.Tests.csproj --collect:"XPlat Code Coverage" --results-directory /tmp/results ;     /tools/reportgenerator -reports:"/tmp/results/**/coverage.cobertura.xml" -targetdir:coverage/dotnet -reporttypes:Html ;     mkdir -p coverage/dotnet

FROM node:24-slim@sha256:c86ecead9e55a5210ac1727de38a4ca840bf03dcaf34dc9749addbf383b1920c AS js-coverage
WORKDIR /src
COPY Tests/js/package.json Tests/js/package-lock.json Tests/js/
RUN cd Tests/js && npm ci --no-audit --no-fund
COPY . .
RUN cd Tests/js && npx vitest run --coverage ; mkdir -p /src/coverage/js ; cat /src/coverage-index/istanbul.css >> /src/coverage/js/base.css

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
COPY coverage-index/index.html coverage/
COPY coverage-reports.json coverage/reports.json
COPY --from=coverage /src/coverage/dotnet coverage/dotnet
COPY --from=js-coverage /src/coverage/js coverage/js

# The database file and the uploaded sheets both live here. Without a mount they would sit
# in the container's writable layer and be destroyed along with the container.
#
# Created and chowned as root before dropping to the app user: a volume inherits the ownership
# of the image directory behind it. Left owned by root, the app could not create gmtoolbox.db
# and would fail on startup. /src is chowned too so the app user can pull and rebuild it, and
# the partial .git that .dockerignore lets through is removed so git-sync starts a clean clone.
RUN chmod +x /usr/local/bin/git-sync \
    && rm -rf /src/.git \
    && mkdir -p /app/App_Data && chown -R $APP_UID:$APP_UID /app /src
USER $APP_UID

VOLUME ["/app/App_Data"]
EXPOSE 8080

# The project sets <AssemblyName>GM_Toolbox</AssemblyName>, so the dll has no space in its name.
ENTRYPOINT ["git-sync", "dotnet", "GM_Toolbox.dll"]
