FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BoletimPlus.Api/BoletimPlus.Api.csproj src/BoletimPlus.Api/
RUN dotnet restore src/BoletimPlus.Api/BoletimPlus.Api.csproj
COPY src/ src/
RUN dotnet publish src/BoletimPlus.Api/BoletimPlus.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "BoletimPlus.Api.dll"]
