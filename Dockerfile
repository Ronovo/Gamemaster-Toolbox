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
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# The database file and the export archives both live here. Without a mount they would sit
# in the container's writable layer and be destroyed along with the container.
#
# Created and chowned as root before dropping back to the app user: the base stage runs as
# $APP_UID, and a volume inherits the ownership of the image directory behind it. Left owned
# by root, the app could not create gmtoolbox.db and would fail on startup.
USER root
RUN mkdir -p /app/App_Data && chown -R $APP_UID:$APP_UID /app/App_Data
USER $APP_UID

VOLUME ["/app/App_Data"]

# GM_Toolbox.dll, not "GM Toolbox.dll": the project sets <AssemblyName>GM_Toolbox</AssemblyName>
# because the folder name has a space in it and that broke the EF tooling.
ENTRYPOINT ["dotnet", "GM_Toolbox.dll"]