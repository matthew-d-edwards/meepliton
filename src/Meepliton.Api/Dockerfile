FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/", "."]
RUN dotnet restore "Meepliton.Api/Meepliton.Api.csproj"
RUN dotnet build  "Meepliton.Api/Meepliton.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Meepliton.Api/Meepliton.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Meepliton.Api.dll"]
