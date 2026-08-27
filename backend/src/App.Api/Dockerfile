FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Directory.Packages.props (versiones centralizadas) vive en la raíz de backend/,
# fuera de src/; hay que copiarlo para que el restore resuelva las versiones.
COPY Directory.Packages.props .
COPY src/ .
RUN dotnet restore App.Api/App.Api.csproj
RUN dotnet publish App.Api/App.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "App.Api.dll"]