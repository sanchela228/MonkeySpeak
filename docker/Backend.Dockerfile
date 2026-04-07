FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY sources/Backend ./Backend

WORKDIR /src/Backend
RUN dotnet restore
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app .

ENTRYPOINT ["dotnet", "monkeyspeakbackend.dll"]
