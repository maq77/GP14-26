FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# copy project + protos first (better cache)
COPY ./apps/api-dotnet/api-dotnet.csproj ./apps/api-dotnet/
COPY ./apps/protos ./apps/protos/
RUN dotnet restore ./apps/api-dotnet/api-dotnet.csproj

# copy source & publish
COPY ./apps/api-dotnet ./apps/api-dotnet
RUN dotnet publish ./apps/api-dotnet/api-dotnet.csproj -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "api-dotnet.dll"]
