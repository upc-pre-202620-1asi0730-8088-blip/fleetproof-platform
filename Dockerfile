FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY FleetProof.Api.csproj .
RUN dotnet restore FleetProof.Api.csproj
COPY . .
RUN dotnet publish FleetProof.Api.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
USER app
ENTRYPOINT ["dotnet", "FleetProof.Api.dll"]
