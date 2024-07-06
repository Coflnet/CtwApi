FROM mcr.microsoft.com/dotnet/sdk:8.0 as build
WORKDIR /build/app
COPY CtwApi.csproj CtwApi.csproj
RUN dotnet restore
COPY . .
RUN dotnet test CtwApi.csproj
RUN dotnet publish -c release -o /app CtwApi.csproj

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8000

RUN useradd --uid $(shuf -i 2000-65000 -n 1) app-user
USER app-user

ENTRYPOINT ["dotnet", "CtwApi.dll", "--hostBuilder:reloadConfigOnChange=false"]
