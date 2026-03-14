# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Front/Front.csproj", "src/Front/"]
RUN dotnet restore "src/Front/Front.csproj"
COPY . .
WORKDIR "/src/src/Front"
RUN dotnet publish "Front.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Production stage - serve with nginx
FROM nginx:alpine AS final
ARG API_BASE_URL=http://localhost:42000
RUN apk add --no-cache curl
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY --from=build /src/src/Front/nginx.conf /etc/nginx/conf.d/default.conf
RUN sed -i "s|\"ApiBaseUrl\":.*|\"ApiBaseUrl\": \"${API_BASE_URL}\"|" /usr/share/nginx/html/appsettings.json
EXPOSE 80

