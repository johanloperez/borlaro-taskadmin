# Build desde la raíz del repo: docker build -f src/Borlaro.Tms.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copiar solo los csproj primero para que restore quede cacheado mientras no cambien.
COPY Borlaro.Tms.sln ./
COPY src/Borlaro.Tms.Domain/Borlaro.Tms.Domain.csproj src/Borlaro.Tms.Domain/
COPY src/Borlaro.Tms.Infrastructure/Borlaro.Tms.Infrastructure.csproj src/Borlaro.Tms.Infrastructure/
COPY src/Borlaro.Tms.Agent/Borlaro.Tms.Agent.csproj src/Borlaro.Tms.Agent/
COPY src/Borlaro.Tms.Api/Borlaro.Tms.Api.csproj src/Borlaro.Tms.Api/
RUN dotnet restore src/Borlaro.Tms.Api/Borlaro.Tms.Api.csproj

COPY src/ src/
RUN dotnet publish src/Borlaro.Tms.Api/Borlaro.Tms.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# La imagen alpine viene sin la base IANA, y en Linux `TimeZoneInfo.FindSystemTimeZoneById` la lee
# de /usr/share/zoneinfo: sin este paquete la única zona que existe es UTC y el alta de personas
# rechaza cualquier otra con «no es una zona horaria conocida». Con tzdata resuelven las ~420 zonas
# y también los alias de la base («America/Buenos_Aires» por «America/Argentina/Buenos_Aires»), que
# es lo que reportan los navegadores en Windows. El modo invariante de globalización no interviene
# acá: afecta a las culturas de ICU, no a la base de zonas.
RUN apk add --no-cache tzdata

# Usuario sin privilegios: si alguien escapa del proceso, no es root del contenedor.
RUN adduser --disabled-password --no-create-home --uid 10001 appuser

# Los directorios de datos se crean y se ceden ANTES de cambiar de usuario. Docker inicializa un
# volumen vacío copiando el contenido —y los permisos— de esta ruta: sin este paso el volumen
# nace de root, y el proceso no puede escribir ni un entregable ni un email. Falla en silencio,
# porque el fallo queda dentro del intento de notificación y no tumba nada.
RUN mkdir -p /app/uploads /app/outbox && chown -R appuser:appuser /app

USER appuser

COPY --from=build --chown=appuser:appuser /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Borlaro.Tms.Api.dll"]
