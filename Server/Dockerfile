FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /out .
EXPOSE 25000
EXPOSE 25001/udp
ENTRYPOINT ["dotnet", "Server.dll"]
