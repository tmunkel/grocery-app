# Grocery App

Mobile-first web app for a **shopping list** and a **home grocery inventory**. Built with Blazor Web App (interactive server, .NET 10), EF Core and PostgreSQL. Product data comes from the [product inventory service](../product-inventory-service) over REST.

## Features

- One central text box: type an EAN, use a USB/Bluetooth barcode scanner (keyboard mode, ending with Enter), or type a product name
- 📷 Camera barcode scanning on phones (native `BarcodeDetector`, with a ZXing fallback for browsers such as iOS Safari)
- Mode toggle **Shopping / Inventory**. A found product is added to the active list, or its quantity is increased if it is already there
- +/- quantity controls. Decreasing to zero removes the item
- Shopping list: tick items as bought, then **Move bought to inventory** (quantities are merged) or **Remove bought**
- Unknown EAN → **New product** dialog, prefilled from Open Food Facts (name, brand, size, category, image)
- Take or upload a product photo. The browser resizes it before upload
- Edit a product: tap an item in a list, or the ✏️ button in search results, to change its details, replace the photo or remove the image
- Delete a product: in the edit dialog, tap **🗑 Delete product** and confirm. The product is deleted from the inventory service and removed from the shopping list and inventory. Products deleted elsewhere (e.g. directly through the API) disappear from the lists the next time a list is opened
- List items keep a copy of the product's name, brand, EAN and image link, so the list still works when the inventory service is down. The copy is updated when you edit a product here, and refreshed from the service each time a list is opened
- Product images are proxied through the app (`/product-images/{id}`), so phones only need to reach the app

## Run with Docker

Start the [product inventory service](../product-inventory-service) first (`docker compose up -d --build` in that repo). Then run:

```powershell
docker compose up -d --build
```

Open <http://localhost:8080>. The app container reaches the inventory service through the host's published port (`host.docker.internal:5100`). Settings come from environment variables, which override `appsettings.json` (use `__` in place of `:`, for example `ConnectionStrings__Grocery`). The compose file supports these variables, which you can also put in a `.env` file:

| Variable | Default | Description |
|---|---|---|
| `POSTGRES_PASSWORD` | `grocery` | Database password |
| `PRODUCT_INVENTORY_URL` | `http://host.docker.internal:5100/` | Base URL of the inventory service |
| `APP_PORT` | `8080` | Host port for the app |
| `DB_PORT` | `5434` | Host port for PostgreSQL (e.g. for pgAdmin) |
| `APP_IMAGE` | `grocery-app:latest` | Image name for the app, e.g. a registry path |

### Deploy prebuilt images (no .NET SDK or source on the server)

The server needs only Docker with Compose. Build the image on a machine that has the source, then copy it to the server, either as a file or through a registry.

**As a file.** On the build machine:

```powershell
docker compose build
docker save -o grocery-app.tar grocery-app:latest
```

Copy `grocery-app.tar` and `docker-compose.yml`, plus an optional `.env`, to a folder on the server. Start the inventory service first. Then run:

```bash
docker load -i grocery-app.tar
docker compose up -d --no-build
```

**Through a registry.** Set `APP_IMAGE=ghcr.io/<you>/grocery-app:latest`, then run `docker compose build` and `docker compose push app` on the build machine. On the server, put the same `APP_IMAGE` in `.env`, then run `docker compose pull` and `docker compose up -d --no-build`.

If the inventory service runs on another host, set `PRODUCT_INVENTORY_URL`. Images are built for the build machine's CPU architecture; for an ARM server, use `docker buildx build --platform linux/arm64`. The database tables are created automatically when the app first starts.

The container serves plain HTTP. On the PC, `http://localhost` counts as a secure origin, so the camera works. Phones need HTTPS, so put a reverse proxy with a certificate in front of the app.

### Behind Traefik (HTTPS)

`docker-compose.traefik.yml` publishes the app at `https://grocery.<DOMAIN>` through a Traefik reverse proxy instead of host ports, and connects it to the inventory service directly over the shared `proxy` network (`http://inventory-api:8080/`). Enable it with these lines in `.env`:

```
COMPOSE_FILE=docker-compose.yml:docker-compose.traefik.yml
DOMAIN=example.com
```

Start the inventory service with its Traefik file first. The Traefik stack and a full walkthrough (certificates, registry, start order) live in the separate `deploy` folder; see its `README.md`.

## Run locally

Prerequisites: .NET 10 SDK, Docker, and a running [product inventory service](../product-inventory-service) on <http://localhost:5100>.

```powershell
docker compose up -d postgres             # PostgreSQL only, on localhost:5434
dotnet dev-certs https --trust            # once
dotnet run --project src/GroceryApp
```

Open <https://localhost:7200>. Migrations are applied on startup.

### Use from a phone

Camera access requires HTTPS. Run with the `lan` profile so the app listens on all interfaces:

```powershell
dotnet run --project src/GroceryApp --launch-profile lan
```

Then open `https://<your-pc-ip>:7200` on the phone and accept the development certificate warning. For permanent use, set up a proper certificate or a reverse proxy. The app has no authentication, so only expose it on a trusted network.

## Configuration (`appsettings.json`)

| Key | Default | Description |
|---|---|---|
| `ConnectionStrings:Grocery` | `Host=localhost;Port=5434;...` | PostgreSQL connection |
| `ProductInventory:BaseUrl` | `http://localhost:5100/` | Product inventory service URL |
| `Database:MigrateOnStartup` | `true` | Apply EF Core migrations on startup |

## Tests

```powershell
dotnet test
```

The tests use bUnit component tests and `ListService` tests against in-memory SQLite. They do not need Docker.
