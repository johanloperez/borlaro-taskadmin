# Build desde la raíz del repo: docker build -f deploy/web.Dockerfile .
#
# El frontend se construye acá adentro y no en la máquina de quien despliega. Con el build en el
# host, alcanzaba con olvidarse un `npm run build` para que el navegador siguiera viendo la
# versión anterior mientras la API ya era nueva — y eso no da ningún error, solo síntomas raros.
FROM node:22-alpine AS build
WORKDIR /src

# Las dependencias primero, para que la capa quede cacheada mientras no cambie el lockfile.
COPY web/package.json web/package-lock.json ./
RUN npm ci

COPY web/ ./
RUN npm run build

FROM caddy:2-alpine AS runtime

# El Caddyfile va adentro de la imagen: el dominio sigue siendo configurable por la variable
# TASKADMIN_DOMAIN, que es lo único que cambia entre instalaciones.
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /src/dist /srv/web
