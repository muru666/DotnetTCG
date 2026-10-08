FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DotnetTCG.sln ./
COPY src/DotnetTCG.Api/*.csproj src/DotnetTCG.Api/
COPY src/DotnetTCG.Application/*.csproj src/DotnetTCG.Application/
COPY src/DotnetTCG.Domain/*.csproj src/DotnetTCG.Domain/
COPY src/DotnetTCG.Infrastructure/*.csproj src/DotnetTCG.Infrastructure/
RUN dotnet restore src/DotnetTCG.Api/DotnetTCG.Api.csproj
COPY . ./
RUN dotnet publish src/DotnetTCG.Api/DotnetTCG.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "DotnetTCG.Api.dll"]
