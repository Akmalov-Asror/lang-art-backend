FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY LangArt.sln ./
COPY src/LangArt.Api/LangArt.Api.csproj src/LangArt.Api/
RUN dotnet restore src/LangArt.Api/LangArt.Api.csproj

COPY src/ src/
RUN dotnet publish src/LangArt.Api/LangArt.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

COPY --from=build /app/publish ./
RUN mkdir -p /app/uploads

ENTRYPOINT ["dotnet", "LangArt.Api.dll"]
