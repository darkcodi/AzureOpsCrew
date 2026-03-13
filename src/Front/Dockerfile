# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Front/Front.csproj", "src/Front/"]
RUN dotnet restore "src/Front/Front.csproj"
COPY . .
WORKDIR "/src/src/Front"
RUN dotnet publish "Front.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM caddy:2-alpine AS final
ARG API_BASE_URL=http://localhost:42000
COPY --from=build /app/publish/wwwroot /srv
COPY --from=build /src/src/Front/Caddyfile /etc/caddy/Caddyfile
RUN sed -i "s|\"ApiBaseUrl\":.*|\"ApiBaseUrl\": \"${API_BASE_URL}\"|" /srv/appsettings.json
EXPOSE 80
